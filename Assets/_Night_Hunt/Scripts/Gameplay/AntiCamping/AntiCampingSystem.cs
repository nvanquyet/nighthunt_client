using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.AntiCamping
{
    /// <summary>
    /// Anti-camping system: Reveals players who stay in one place too long.
    /// Prevents passive/camping gameplay.
    ///
    /// On death: camping data is reset automatically via lifecycle subscription.
    /// Uses PlayerPublicRegistry for O(n) player lookup instead of FindObjectsOfType.
    /// </summary>
    public class AntiCampingSystem : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameplayConfig _gameplayConfig;
        [SerializeField] private float _healthDrainPerSecond = 5f;

        [Header("Visual")]
        [SerializeField] private GameObject revealIndicatorPrefab;

        private Dictionary<uint, CampingData> playerCampingData = new Dictionary<uint, CampingData>();
        private Dictionary<uint, bool> revealedPlayers = new Dictionary<uint, bool>();
        private Dictionary<uint, Coroutine> _drainCoroutines = new Dictionary<uint, Coroutine>();

        private readonly SyncList<uint> networkRevealedPlayers = new SyncList<uint>();

        // ── FishNet Lifecycle ─────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (RegistryService.Instance != null)
                RegistryService.Instance.OnPlayerRegistered += SubscribePlayerDeath;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (RegistryService.Instance != null)
                RegistryService.Instance.OnPlayerRegistered -= SubscribePlayerDeath;
        }

        private void SubscribePlayerDeath(NetworkPlayer np, PlayerRegistryData _)
        {
            var lifecycle = ComponentResolver.Find<CharacterLifecycleController>(np)
                .OnSelf().InChildren()
                .OrLogWarning("[AntiCampingSystem] CharacterLifecycleController not found")
                .Resolve();

            if (lifecycle != null)
                lifecycle.OnDied += () => ResetPlayerCamping((uint)np.ObjectId);
        }

        // ── Server Update ─────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsServerStarted) return;

            float updateInterval = _gameplayConfig != null ? _gameplayConfig.CampingUpdateInterval : 5f;
            if (Time.frameCount % Mathf.Max(1, Mathf.RoundToInt(updateInterval / Time.deltaTime)) == 0)
                UpdateCampingDetection();
        }

        [Server]
        private void UpdateCampingDetection()
        {
            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return;

            foreach (var player in players)
            {
                if (player == null || !player.IsSpawned || !player.IsAlive) continue;
                UpdatePlayerCamping(player);
            }
        }

        [Server]
        private void UpdatePlayerCamping(NetworkPlayer player)
        {
            uint playerId = (uint)player.ObjectId;
            Vector3 currentPosition = player.transform.position;

            if (!playerCampingData.ContainsKey(playerId))
            {
                playerCampingData[playerId] = new CampingData
                {
                    PlayerId = playerId,
                    StartPosition = currentPosition,
                    StartTime = Time.time,
                    LastPosition = currentPosition
                };
                return;
            }

            CampingData data = playerCampingData[playerId];
            float positionThreshold = _gameplayConfig != null ? _gameplayConfig.CampingPositionThreshold : 5f;
            float distanceMoved = Vector3.Distance(currentPosition, data.StartPosition);

            if (distanceMoved > positionThreshold)
            {
                data.StartPosition = currentPosition;
                data.StartTime = Time.time;
                data.LastPosition = currentPosition;

                if (revealedPlayers.ContainsKey(playerId) && revealedPlayers[playerId])
                    RemoveReveal(player);
            }
            else
            {
                data.LastPosition = currentPosition;
                float timeCamping = Time.time - data.StartTime;
                float campingTimeThreshold = _gameplayConfig != null ? _gameplayConfig.CampingTimeThreshold : 90f;

                if (timeCamping >= campingTimeThreshold)
                {
                    if (!revealedPlayers.ContainsKey(playerId) || !revealedPlayers[playerId])
                        RevealPlayer(player);
                }
            }
        }

        [Server]
        private void RevealPlayer(NetworkPlayer player)
        {
            uint playerId = (uint)player.ObjectId;
            revealedPlayers[playerId] = true;

            if (!networkRevealedPlayers.Contains(playerId))
                networkRevealedPlayers.Add(playerId);

            // Start health drain
            StopDrain(playerId);
            _drainCoroutines[playerId] = StartCoroutine(DrainHealthWhileCamping(player));

            float revealRadius = _gameplayConfig != null ? _gameplayConfig.CampingRevealRadius : 30f;
            RpcRevealPlayer(playerId, revealRadius);

            Debug.Log($"[AntiCampingSystem] Player {player.DisplayName} is camping — revealed + draining!");
        }

        [Server]
        private void RemoveReveal(NetworkPlayer player)
        {
            uint playerId = (uint)player.ObjectId;
            revealedPlayers[playerId] = false;
            StopDrain(playerId);

            if (networkRevealedPlayers.Contains(playerId))
                networkRevealedPlayers.Remove(playerId);

            RpcRemoveReveal(playerId);
        }

        private void StopDrain(uint playerId)
        {
            if (_drainCoroutines.TryGetValue(playerId, out var c) && c != null)
            {
                StopCoroutine(c);
                _drainCoroutines.Remove(playerId);
            }
        }

        [Server]
        private IEnumerator DrainHealthWhileCamping(NetworkPlayer player)
        {
            uint playerId = (uint)player.ObjectId;
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (player == null || !player.IsAlive)
                {
                    _drainCoroutines.Remove(playerId);
                    yield break;
                }

                var healthSystem = player.GetComponentInChildren<PlayerHealthSystem>();
                if (healthSystem == null)
                {
                    _drainCoroutines.Remove(playerId);
                    yield break;
                }

                var info = new DamageInfo
                {
                    Damage = _healthDrainPerSecond,
                    WeaponId = "anti_camp",
                    ShooterNetworkObjectId = -1,
                    IsHeadshot = false,
                    HitPoint = player.transform.position,
                    HitNormal = Vector3.up,
                };
                healthSystem.ApplyDamageServer(info);
            }
        }

        [ObserversRpc]
        private void RpcRevealPlayer(uint playerId, float radius)
        {
            NetworkPlayer target = GetPlayerById(playerId);
            if (target == null || revealIndicatorPrefab == null) return;

            var indicator = Instantiate(revealIndicatorPrefab, target.transform);
            indicator.name = "RevealIndicator";
        }

        [ObserversRpc]
        private void RpcRemoveReveal(uint playerId)
        {
            NetworkPlayer target = GetPlayerById(playerId);
            if (target == null) return;

            foreach (Transform child in target.transform)
            {
                if (child.name == "RevealIndicator")
                    Destroy(child.gameObject);
            }
        }

        public bool IsPlayerRevealed(uint playerId) => networkRevealedPlayers.Contains(playerId);

        [Server]
        public void ResetPlayerCamping(uint playerId)
        {
            playerCampingData.Remove(playerId);
            revealedPlayers.Remove(playerId);
            StopDrain(playerId);

            if (networkRevealedPlayers.Contains(playerId))
                networkRevealedPlayers.Remove(playerId);
        }

        private NetworkPlayer GetPlayerById(uint playerId)
        {
            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return null;
            foreach (var player in players)
            {
                if (player != null && player.ObjectId == playerId)
                    return player;
            }
            return null;
        }
    }

    [System.Serializable]
    public class CampingData
    {
        public uint PlayerId;
        public Vector3 StartPosition;
        public float StartTime;
        public Vector3 LastPosition;
    }
}
