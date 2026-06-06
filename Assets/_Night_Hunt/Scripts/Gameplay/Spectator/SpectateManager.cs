using System;
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

        public event Action<NetworkPlayer> OnCurrentPlayerChanged;

        /// <summary>Fired when spectate mode begins (player died and chose to spectate).</summary>
        public event Action OnSpectateStarted;

        /// <summary>Fired when spectate mode ends (player respawned).</summary>
        public event Action OnSpectateStopped;

        /// <summary>
        /// Fired once when the local player is registered for the first time.
        /// FogTeamVisibilityBinder (and any other component that needs the local
        /// player's team) subscribes here and refreshes its state on this callback.
        /// </summary>
        public event Action<NetworkPlayer> OnLocalPlayerSet;



        public NetworkPlayer GetCurrentPlayer()
        {
            if (isSpectating && IsLivePlayer(currentSpectatedPlayer))
                return currentSpectatedPlayer;

            if (isSpectating)
                currentSpectatedPlayer = null;

            return GetLocalPlayer();
        }

        public bool IsSpectating() => isSpectating && IsLivePlayer(currentSpectatedPlayer);

        public bool IsCurrentPlayerLocal()
        {
            if (!isSpectating)
                return true;

            return IsLivePlayer(currentSpectatedPlayer) && currentSpectatedPlayer.IsLocalPlayer;
        }

        public NetworkPlayer GetLocalPlayer()
        {
            if (!IsLivePlayer(localPlayer))
                localPlayer = null;

            return localPlayer;
        }

        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (!IsLivePlayer(player) || !player.IsLocalPlayer)
            {
                LogWarning("Attempted to set non-local player");
                return;
            }

            localPlayer = player;

            // Notify fog and any other late-init systems that need the local team.
            RaiseLocalPlayerSet(localPlayer);

            if (!isSpectating)
            {
                RaiseCurrentPlayerChanged(localPlayer);
            }

            Log($"Local player set: {DescribePlayer(player)}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Spectate,
                "LocalPlayerSet",
                $"player={DescribePlayer(player)}",
                this);
        }

        public void StartSpectating(NetworkPlayer player)
        {
            if (!IsLivePlayer(player))
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
                LogWarning($"Rejected spectate target '{DescribePlayer(player)}'");
                return;
            }

            currentSpectatedPlayer = player;
            isSpectating = true;
            RaiseCurrentPlayerChanged(player);
            Raise(OnSpectateStarted);

            Log($"Started spectating {DescribePlayer(player)}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Spectate,
                "SpectateStart",
                $"local={DescribePlayer(localPlayer)} target={DescribePlayer(player)}",
                this);
        }

        public void StopSpectating()
        {
            if (!isSpectating) return;

            isSpectating = false;
            var previousSpectated = currentSpectatedPlayer;
            currentSpectatedPlayer = null;

            RaiseCurrentPlayerChanged(GetLocalPlayer());
            Raise(OnSpectateStopped);

            Log($"Stopped spectating {DescribePlayer(previousSpectated)}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Spectate,
                "SpectateStop",
                $"local={DescribePlayer(localPlayer)} previous={DescribePlayer(previousSpectated)}",
                this);
        }

        /// <summary>
        /// ✅ OPTIMIZED: Use PlayerRegistry instead of FindObjectsOfType
        /// </summary>
        public void SwitchSpectatedPlayer(bool next = true)
        {
            if (GetLocalPlayer() == null) return;

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
                PhaseTestLog.Log(PhaseTestLogCategory.Spectate, "SpectateSwitchNoTargets", $"local={DescribePlayer(localPlayer)}", this);
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
            if (!IsLivePlayer(player) || GetLocalPlayer() == null) return false;
            if (player == localPlayer || player.IsLocalPlayer) return false;
            if (!player.IsAlive) return false;
            return player.TeamId == localPlayer.TeamId;
        }

        /// <summary>
        /// Returns true when at least one alive same-team player exists to spectate.
        /// Used by DeathScreen to decide whether to auto-start spectating on death.
        /// </summary>
        public bool HasLivingSpectateTargets()
        {
            if (GetLocalPlayer() == null || PlayerPublicRegistry.Instance == null) return false;
            var all = PlayerPublicRegistry.Instance.GetAllPlayers();
            if (all == null) return false;
            foreach (var p in all)
            {
                if (CanSpectate(p)) return true;
            }
            return false;
        }

        private static bool IsLivePlayer(NetworkPlayer player) => player != null;

        private static string DescribePlayer(NetworkPlayer player)
        {
            if (!IsLivePlayer(player))
                return "null";

            return $"{player.DisplayName} obj={player.ObjectId} team={player.TeamId} alive={player.IsAlive}";
        }

        private void RaiseCurrentPlayerChanged(NetworkPlayer player)
        {
            RaisePlayerEvent(OnCurrentPlayerChanged, player);
        }

        private void RaiseLocalPlayerSet(NetworkPlayer player)
        {
            RaisePlayerEvent(OnLocalPlayerSet, player);
        }

        private void RaisePlayerEvent(Action<NetworkPlayer> handlers, NetworkPlayer player)
        {
            if (handlers == null)
                return;

            foreach (Delegate handlerDelegate in handlers.GetInvocationList())
            {
                var handler = handlerDelegate as Action<NetworkPlayer>;
                if (handler == null)
                    continue;

                try
                {
                    handler(player);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }

        private void Raise(Action handlers)
        {
            if (handlers == null)
                return;

            foreach (Delegate handlerDelegate in handlers.GetInvocationList())
            {
                var handler = handlerDelegate as Action;
                if (handler == null)
                    continue;

                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
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
