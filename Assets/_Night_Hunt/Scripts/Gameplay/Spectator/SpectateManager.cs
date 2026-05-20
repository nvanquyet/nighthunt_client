using System.Linq;
using NightHunt.Core;
using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Diagnostics;

namespace NightHunt.Gameplay.Spectator
{
    /// <summary>
    /// Manages spectate mode and current player tracking for UI systems.
    /// UI components use this to determine which player's data to display.
    /// Follows Singleton pattern for global access.
    /// </summary>
    public class SpectateManager : Singleton<SpectateManager>
    {

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private NetworkPlayer localPlayer;
        private NetworkPlayer currentSpectatedPlayer;
        private bool isSpectating = false;

        public event System.Action<NetworkPlayer> OnCurrentPlayerChanged;

        /// <summary>Fired when spectate mode begins (player died and chose to spectate).</summary>
        public event System.Action OnSpectateStarted;

        /// <summary>Fired when spectate mode ends (player respawned).</summary>
        public event System.Action OnSpectateStopped;

        /// <summary>
        /// Fired once when the local player is registered for the first time.
        /// FogTeamVisibilityBinder (and any other component that needs the local
        /// player's team) subscribes here and refreshes its state on this callback.
        /// </summary>
        public event System.Action<NetworkPlayer> OnLocalPlayerSet;



        public NetworkPlayer GetCurrentPlayer()
        {
            if (isSpectating && currentSpectatedPlayer != null)
                return currentSpectatedPlayer;
            return localPlayer;
        }

        public bool IsSpectating() => isSpectating && currentSpectatedPlayer != null;

        public bool IsCurrentPlayerLocal()
        {
            if (!isSpectating)
                return true;

            return currentSpectatedPlayer != null && currentSpectatedPlayer.IsLocalPlayer;
        }

        public NetworkPlayer GetLocalPlayer() => localPlayer;

        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                LogWarning("Attempted to set non-local player");
                return;
            }

            localPlayer = player;

            // Notify fog and any other late-init systems that need the local team.
            OnLocalPlayerSet?.Invoke(localPlayer);

            if (!isSpectating)
            {
                OnCurrentPlayerChanged?.Invoke(localPlayer);
            }

            Log($"Local player set: {player.DisplayName}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Spectate,
                "LocalPlayerSet",
                $"player={player.DisplayName} obj={player.ObjectId} team={player.TeamId} alive={player.IsAlive}",
                this);
        }

        public void StartSpectating(NetworkPlayer player)
        {
            if (player == null)
            {
                StopSpectating();
                return;
            }

            if (player.IsLocalPlayer)
            {
                StopSpectating();
                return;
            }

            if (!CanSpectate(player))
            {
                LogWarning($"Rejected spectate target '{player.DisplayName}'");
                return;
            }

            currentSpectatedPlayer = player;
            isSpectating = true;
            OnCurrentPlayerChanged?.Invoke(player);
            OnSpectateStarted?.Invoke();

            Log($"Started spectating {player.DisplayName}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Spectate,
                "SpectateStart",
                $"local={localPlayer?.DisplayName ?? "null"} localTeam={localPlayer?.TeamId ?? -1} target={player.DisplayName} targetObj={player.ObjectId} targetTeam={player.TeamId} targetAlive={player.IsAlive}",
                this);
        }

        public void StopSpectating()
        {
            if (!isSpectating) return;

            isSpectating = false;
            var previousSpectated = currentSpectatedPlayer;
            currentSpectatedPlayer = null;

            OnCurrentPlayerChanged?.Invoke(localPlayer);
            OnSpectateStopped?.Invoke();

            Log($"Stopped spectating {previousSpectated?.DisplayName}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Spectate,
                "SpectateStop",
                $"local={localPlayer?.DisplayName ?? "null"} previous={previousSpectated?.DisplayName ?? "null"}",
                this);
        }

        /// <summary>
        /// ✅ OPTIMIZED: Use PlayerRegistry instead of FindObjectsOfType
        /// </summary>
        public void SwitchSpectatedPlayer(bool next = true)
        {
            if (localPlayer == null) return;

            // ✅ Get from Registry (O(1) access to list)
            if (PlayerPublicRegistry.Instance == null)
            {
                LogWarning("PlayerPublicRegistry not available");
                return;
            }

            // Get all same-team alive players except local player.
            var allPlayers = PlayerPublicRegistry.Instance.GetAllPlayers();
            allPlayers = System.Array.FindAll(allPlayers, CanSpectate);
            if (allPlayers.Length == 0)
            {
                PhaseTestLog.Log(PhaseTestLogCategory.Spectate, "SpectateSwitchNoTargets", $"local={localPlayer.DisplayName} team={localPlayer.TeamId}", this);
                StopSpectating();
                return;
            }

            int currentIndex = -1;
            if (isSpectating && currentSpectatedPlayer != null)
            {
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    if (allPlayers[i] == currentSpectatedPlayer)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            // Next/previous
            int direction = next ? 1 : -1;
            int newIndex = (currentIndex + direction + allPlayers.Length) % allPlayers.Length;

            StartSpectating(allPlayers[newIndex]);
        }

        public bool CanSpectate(NetworkPlayer player)
        {
            if (player == null || localPlayer == null) return false;
            if (player == localPlayer || player.IsLocalPlayer) return false;
            if (!player.IsAlive) return false;
            return player.TeamId == localPlayer.TeamId;
        }
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                UnityEngine.Debug.Log($"[SpectateManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (enableDebugLogs) UnityEngine.Debug.LogWarning($"[SpectateManager] {message}");
        }
    }
}
