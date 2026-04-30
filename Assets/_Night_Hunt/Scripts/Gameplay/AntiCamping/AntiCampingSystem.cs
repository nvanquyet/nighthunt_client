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

        [Tooltip("HP drain mỗi giây khi player bị phát hiện camping.\n" +
                 "Fallback nếu _gameplayConfig = null.\n" +
                 "Default: 5 HP/s  (= 100HP player sẽ chết sau ~20s camping)")]
        [Min(0f)]
        [SerializeField] private float _healthDrainPerSecond = 5f;
        [SerializeField] private float _activityMovementThreshold = 0.25f;

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
            float frameMovement = Vector3.Distance(currentPosition, data.LastPosition);

            if (distanceMoved > positionThreshold || frameMovement > _activityMovementThreshold)
            {
                Debug.Log($"[ANTI_CAMP_FLOW] Reset camping for {player.DisplayName}: totalMove={distanceMoved:F2}/{positionThreshold:F2}, recentMove={frameMovement:F2}/{_activityMovementThreshold:F2}.");
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

                float positionThreshold = _gameplayConfig != null ? _gameplayConfig.CampingPositionThreshold : 5f;
                if (playerCampingData.TryGetValue(playerId, out var data))
                {
                    float distanceMoved = Vector3.Distance(player.transform.position, data.StartPosition);
                    float frameMovement = Vector3.Distance(player.transform.position, data.LastPosition);
                    if (distanceMoved > positionThreshold || frameMovement > _activityMovementThreshold)
                    {
                        Debug.Log($"[ANTI_CAMP_FLOW] Stop drain for {player.DisplayName}: totalMove={distanceMoved:F2}/{positionThreshold:F2}, recentMove={frameMovement:F2}/{_activityMovementThreshold:F2}.");
                        data.StartPosition = player.transform.position;
                        data.StartTime = Time.time;
                        data.LastPosition = player.transform.position;
                        revealedPlayers[playerId] = false;
                        if (networkRevealedPlayers.Contains(playerId))
                            networkRevealedPlayers.Remove(playerId);
                        RpcRemoveReveal(playerId);
                        _drainCoroutines.Remove(playerId);
                        yield break;
                    }
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

#if UNITY_EDITOR
        // ── Editor — Default Config Reference ────────────────────────────────

        [ContextMenu("NightHunt/Tools/Create RevealIndicator Template")]
        private void Editor_CreateRevealIndicatorPrefab()
        {
            const string folder   = "Assets/_Night_Hunt/Prefabs/Gameplay";
            const string prefPath = folder + "/RevealIndicator_Template.prefab";

            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Night_Hunt/Prefabs"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Night_Hunt", "Prefabs");
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Night_Hunt/Prefabs", "Gameplay");

            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefPath) != null)
            {
                Debug.Log($"[AntiCampingSystem] RevealIndicator_Template already exists at {prefPath}");
            }
            else
            {
                // Simple sphere visual — replace with particle system in prefab edit mode.
                var root = new GameObject("RevealIndicator_Template");
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "RevealSphere";
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localScale = Vector3.one * 0.5f;
                // Remove collider — purely visual
                UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());

                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefPath);
                UnityEngine.Object.DestroyImmediate(root);
                Debug.Log($"[AntiCampingSystem] Created RevealIndicator_Template at {prefPath}. Mở prefab để gán material/particle system.");
            }

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefPath);
            if (prefab != null)
            {
                revealIndicatorPrefab = prefab;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            UnityEditor.AssetDatabase.Refresh();
        }

        [ContextMenu("NightHunt/Log AntiCamping Default Values")]
        private void Editor_LogDefaults()
        {
            if (_gameplayConfig != null)
            {
                Debug.Log(
                    $"[AntiCampingSystem] Config values (from GameplayConfig '{_gameplayConfig.name}'):\n" +
                    $"  CampingTimeThreshold   : {_gameplayConfig.CampingTimeThreshold}s  — đứng yên tối đa before bị reveal (default: 90s)\n" +
                    $"  CampingPositionThreshold: {_gameplayConfig.CampingPositionThreshold}m — bán kính 'đứng yên' (default: 5m)\n" +
                    $"  CampingRevealRadius    : {_gameplayConfig.CampingRevealRadius}m  — bán kính vùng bị lộ cho địch (default: 30m)\n" +
                    $"  CampingUpdateInterval  : {_gameplayConfig.CampingUpdateInterval}s  — chu kỳ check (default: 5s)\n" +
                    $"  _healthDrainPerSecond  : {_healthDrainPerSecond} HP/s — tốc độ drain HP khi camping (default: 5)");
            }
            else
            {
                Debug.LogWarning(
                    "[AntiCampingSystem] _gameplayConfig chưa gán! Fallback hardcoded defaults:\n" +
                    "  CampingTimeThreshold   : 90s\n" +
                    "  CampingPositionThreshold: 5m\n" +
                    "  CampingRevealRadius    : 30m\n" +
                    "  CampingUpdateInterval  : 5s\n" +
                    $"  _healthDrainPerSecond  : {_healthDrainPerSecond} HP/s\n" +
                    "Kéo GameplayConfig ScriptableObject vào field '_gameplayConfig' để dùng value thực.");
            }
        }

        [ContextMenu("NightHunt/Auto-Assign GameplayConfig")]
        private void Editor_AutoAssignGameplayConfig()
        {
            if (_gameplayConfig != null) { Debug.Log("[AntiCampingSystem] _gameplayConfig already assigned."); return; }

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameplayConfig");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameplayConfig>(path);
                if (cfg != null)
                {
                    _gameplayConfig = cfg;
                    UnityEditor.EditorUtility.SetDirty(this);
                    Debug.Log($"[AntiCampingSystem] _gameplayConfig auto-assigned: {path}");
                    return;
                }
            }
            Debug.LogWarning("[AntiCampingSystem] No GameplayConfig asset found in project.");
        }
#endif
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
