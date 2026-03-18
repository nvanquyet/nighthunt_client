using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Networking;
using NightHunt.Gameplay.Character;
using NightHunt.GameplaySystems.Core.Configs;
using System.Collections.Generic;

namespace NightHunt.Gameplay.AntiCamping
{
    /// <summary>
    /// Anti-camping system: Reveals players who stay in one place too long
    /// Prevents passive/camping gameplay
    /// </summary>
    public class AntiCampingSystem : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameplayConfig _gameplayConfig;

        [Header("Visual")]
        [SerializeField] private GameObject revealIndicatorPrefab;

        // Tracking data
        private Dictionary<uint, CampingData> playerCampingData = new Dictionary<uint, CampingData>();
        private Dictionary<uint, bool> revealedPlayers = new Dictionary<uint, bool>();

        // Synchronized revealed players — SyncList ensures late-joining clients
        // receive the current set without needing a manual state-dump RPC.
        private readonly SyncList<uint> networkRevealedPlayers = new SyncList<uint>();

        public override void OnStartServer()
        {
            base.OnStartServer();
        }

        private void Update()
        {
            if (!IsServer) return;

            // Update camping detection periodically
            float updateInterval = _gameplayConfig != null ? _gameplayConfig.CampingUpdateInterval : 5f;
            if (Time.frameCount % Mathf.RoundToInt(updateInterval / Time.deltaTime) == 0)
            {
                UpdateCampingDetection();
            }
        }

        /// <summary>
        /// Server: Update camping detection for all players
        /// </summary>
        [Server]
        private void UpdateCampingDetection()
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();

            foreach (var player in players)
            {
                if (player == null || !player.IsSpawned) continue;

                UpdatePlayerCamping(player);
            }
        }

        /// <summary>
        /// Server: Update camping status for a player
        /// </summary>
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

            // Check if player moved significantly
            float distanceMoved = Vector3.Distance(currentPosition, data.StartPosition);
            float positionThreshold = _gameplayConfig != null ? _gameplayConfig.CampingPositionThreshold : 5f;
            if (distanceMoved > positionThreshold)
            {
                // Player moved, reset camping data
                data.StartPosition = currentPosition;
                data.StartTime = Time.time;
                data.LastPosition = currentPosition;

                // Remove reveal if was revealed
                if (revealedPlayers.ContainsKey(playerId) && revealedPlayers[playerId])
                {
                    RemoveReveal(player);
                }
            }
            else
            {
                // Player hasn't moved much
                data.LastPosition = currentPosition;
                float timeCamping = Time.time - data.StartTime;

                // Check if camping threshold exceeded
                float campingTimeThreshold = _gameplayConfig != null ? _gameplayConfig.CampingTimeThreshold : 90f;
                if (timeCamping >= campingTimeThreshold)
                {
                    if (!revealedPlayers.ContainsKey(playerId) || !revealedPlayers[playerId])
                    {
                        RevealPlayer(player);
                    }
                }
            }
        }

        /// <summary>
        /// Server: Reveal camping player
        /// </summary>
        [Server]
        private void RevealPlayer(NetworkPlayer player)
        {
            uint playerId = (uint)player.ObjectId;
            revealedPlayers[playerId] = true;

            if (!networkRevealedPlayers.Contains(playerId))
            {
                networkRevealedPlayers.Add(playerId);
            }

            // Apply reveal effect
            float revealRadius = _gameplayConfig != null ? _gameplayConfig.CampingRevealRadius : 30f;
            RpcRevealPlayer(playerId, revealRadius);

            Debug.Log($"[AntiCampingSystem] Player {player.DisplayName} is camping and has been revealed!");
        }

        /// <summary>
        /// Server: Remove reveal from player
        /// </summary>
        [Server]
        private void RemoveReveal(NetworkPlayer player)
        {
            uint playerId = (uint)player.ObjectId;
            revealedPlayers[playerId] = false;

            if (networkRevealedPlayers.Contains(playerId))
            {
                networkRevealedPlayers.Remove(playerId);
            }

            RpcRemoveReveal(playerId);
        }

        /// <summary>
        /// Client: Reveal player
        /// </summary>
        [ObserversRpc]
        private void RpcRevealPlayer(uint playerId, float radius)
        {
            NetworkPlayer player = GetPlayerById(playerId);
            if (player != null)
            {
                // Show reveal indicator
                // Would integrate with vision system to show player position
            }
        }

        /// <summary>
        /// Client: Remove reveal
        /// </summary>
        [ObserversRpc]
        private void RpcRemoveReveal(uint playerId)
        {
            NetworkPlayer player = GetPlayerById(playerId);
            if (player != null)
            {
                // Hide reveal indicator
            }
        }

        /// <summary>
        /// Check if player is revealed
        /// </summary>
        public bool IsPlayerRevealed(uint playerId)
        {
            return networkRevealedPlayers.Contains(playerId);
        }

        /// <summary>
        /// Get player by network ID
        /// </summary>
        private NetworkPlayer GetPlayerById(uint playerId)
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.ObjectId == playerId)
                {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// Reset camping data for player (e.g., after death)
        /// </summary>
        [Server]
        public void ResetPlayerCamping(uint playerId)
        {
            if (playerCampingData.ContainsKey(playerId))
            {
                playerCampingData.Remove(playerId);
            }

            if (revealedPlayers.ContainsKey(playerId))
            {
                revealedPlayers.Remove(playerId);
            }

            if (networkRevealedPlayers.Contains(playerId))
            {
                networkRevealedPlayers.Remove(playerId);
            }
        }
    }

    /// <summary>
    /// Camping data for a player
    /// </summary>
    [System.Serializable]
    public class CampingData
    {
        public uint PlayerId;
        public Vector3 StartPosition;
        public float StartTime;
        public Vector3 LastPosition;
    }
}

