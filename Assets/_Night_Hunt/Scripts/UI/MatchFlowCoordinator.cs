using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Services.Game;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// MatchFlowCoordinator — single authority for all WS match-lifecycle events.
    ///
    /// Ranked flow (direct, no accept step):
    ///   Backend forms group → DS allocates → WS: match_ready
    ///   → MatchLoadingOverlay.Show() + SceneLoader.LoadGame()
    ///   → WS: ds_ready → NetworkGameManager connects FishNet
    ///   → AllPlayersReadyEvent → MatchLoadingOverlay hides
    ///
    /// Custom lobby flow:
    ///   Host presses Start → match_ready arrives directly → same handler
    ///
    /// Lives on PersistentUICanvas (DontDestroyOnLoad) so it survives scene loads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchFlowCoordinator : SingletonPersistent<MatchFlowCoordinator>
    {
        // ── Runtime guard ─────────────────────────────────────────────────────

        // Prevents processing the same match_ready twice (e.g. WS reconnect replay).
        private string _lastHandledMatchId;

        // Guard: prevent double-subscription if OnEnable fires before GameWebSocketService.Awake.
        private bool _wsSubscribed;

        // ── Singleton lifecycle ───────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            // Nothing — subscriptions go in OnEnable/OnDisable to support scene reload.
        }

        private void OnEnable()
        {
            SubscribeWSEvents();
        }

        // Start() runs after all Awake() calls — safe retry if OnEnable() fired too early.
        private void Start()
        {
            SubscribeWSEvents();
        }

        private void OnDisable()
        {
            UnsubscribeWSEvents();
        }

        // ── WS event subscription ─────────────────────────────────────────────

        private void SubscribeWSEvents()
        {
            if (_wsSubscribed) return;

            var ws = GameWebSocketService.Instance;
            if (ws == null) return;

            ws.OnMatchReady     += HandleMatchReady;
            ws.OnDsReady        += HandleDsReady;
            ws.OnMatchCancelled += HandleMatchCancelled;
            ws.OnMatchEnded     += HandleMatchEnded;
            ws.OnRoomDisbanded  += HandleRoomDisbandedDuringGame;
            _wsSubscribed = true;
        }

        private void UnsubscribeWSEvents()
        {
            if (!_wsSubscribed) return;

            var ws = GameWebSocketService.Instance;
            if (ws != null)
            {
                ws.OnMatchReady     -= HandleMatchReady;
                ws.OnDsReady        -= HandleDsReady;
                ws.OnMatchCancelled -= HandleMatchCancelled;
                ws.OnMatchEnded     -= HandleMatchEnded;
                ws.OnRoomDisbanded  -= HandleRoomDisbandedDuringGame;
            }
            _wsSubscribed = false;
        }

        // ── match_ready ───────────────────────────────────────────────────────

        private void HandleMatchReady(GameWebSocketService.MatchReadyEvent e)
        {
            // Dedup: same matchId fired twice (e.g. WS reconnect)
            if (e.matchId == _lastHandledMatchId)
            {
                Debug.LogWarning($"[MFC] match_ready DUPLICATE — matchId={e.matchId} ignored (already handled).");
                return;
            }
            _lastHandledMatchId = e.matchId;

            Debug.Log($"[MFC] match_ready ▶ matchId={e.matchId} mode={e.gameMode} mapId={e.mapId} roomCode={e.roomCode} dsIp={e.dsIp} dsPort={e.dsPort}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            // Resolve scene from mapId.
            SceneId sceneId = SceneId.GameMap_01;
            if (!string.IsNullOrEmpty(e.mapId) && MapConfig.TryGetById(e.mapId, out MapEntry mapEntry))
                sceneId = mapEntry.sceneId;
            else
                Debug.LogWarning($"[MFC] match_ready — mapId='{e.mapId}' not found in MapConfig, defaulting to GameMap_01.");

            // Guard: never accidentally load the Home scene as game map.
            if (sceneId == SceneId.Home)
            {
                Debug.LogError($"[MFC] match_ready resolved sceneId=Home for mapId='{e.mapId}' — misconfiguration. Falling back to GameMap_01.");
                sceneId = SceneId.GameMap_01;
            }

            // Detect relay mode: Custom_Relay was set by game_starting, OR detect from gameMode string.
            bool isRelay = RoomState.Instance?.CurrentGameMode == NightHunt.Networking.GameMode.Custom_Relay
                || (!string.IsNullOrEmpty(e.gameMode)
                    && e.gameMode.IndexOf("custom", System.StringComparison.OrdinalIgnoreCase) >= 0);

            Debug.Log($"[MFC] match_ready — scene: mapId='{e.mapId}' → sceneId={sceneId} isRelay={isRelay} isHost={RoomState.Instance?.IsHostPlayer}. Showing overlay + loading scene.");

            // Update RoomState match info (preserves Custom_Relay mode if already set).
            RoomState.Instance?.SetMatchReady(e.matchId, e.mapId, e.gameMode);

            // Show overlay THEN load scene — overlay is on PersistentUICanvas (DontDestroyOnLoad).
            MatchLoadingOverlay.Instance?.Show(sceneId);

            if (isRelay)
            {
                // Relay: no dedicated server, no boot wait.
                // Advance overlay past DsBooting stage immediately so it shows "Connecting..."
                Debug.Log("[MFC] match_ready: relay mode — MarkDsReady immediately (no DS boot).");
                MatchLoadingOverlay.Instance?.MarkDsReady();
                // Signal NGM: relay is ready to connect as soon as scene loads.
                NightHunt.Networking.NetworkGameManager.Instance?.NotifyRelayReady();
            }

            SceneLoader.LoadGame(sceneId);
        }

        // ── ds_ready ──────────────────────────────────────────────────────────

        private void HandleDsReady(GameWebSocketService.DsReadyEvent e)
        {
            var room = RoomState.Instance;
            Debug.Log($"[MFC] ds_ready \u25ba dsIp={e.dsIp} dsPort={e.dsPort} matchId={e.matchId} mapId={e.mapId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            Debug.Log($"[MFC] ds_ready \u2014 RoomState.DsIp={room?.DsIp} RoomState.DsPort={room?.DsPort} (should match above; set by GWS)");
            // GWS.SetDedicatedServer already stored dsIp/dsPort in RoomState.
            // Notify NetworkGameManager \u2014 it will attempt FishNet connect once scene is loaded.
            NightHunt.Networking.NetworkGameManager.Instance?.NotifyDsReady();
        }

        // ── match_cancelled ───────────────────────────────────────────────────

        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent e)
        {
            Debug.Log($"[MFC] match_cancelled \u25ba reason={e.reason}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            _lastHandledMatchId = null;

            string reason = !string.IsNullOrEmpty(e.reason) ? e.reason : "Match was cancelled.";
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Matchmaking", $"Match cancelled: {reason}");
        }

        // ── match_ended (WS backend confirmation) ────────────────────────────

        private void HandleMatchEnded(GameWebSocketService.MatchEndedWsEvent e)
        {
            // ResultsView subscribes OnMatchEnded directly for ELO/coins display.
            // MatchFlowCoordinator resets flow state so the next match starts clean.
            Debug.Log($"[MFC] match_ended \u25ba matchId={e.matchId} winner={e.winnerTeamId} reason={e.endReason}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            _lastHandledMatchId = null;
        }

        // ── room_disbanded (global handler — covers game scene where CustomLobbyView is inactive) ──

        /// <summary>
        /// Handles room_disbanded arriving at any time (including during gameplay).
        /// CustomLobbyView only handles this when it is active; this persistent handler
        /// ensures RoomState is always cleared, preventing the "leave custom room" block
        /// from triggering on the next ranked queue attempt.
        /// </summary>
        private void HandleRoomDisbandedDuringGame(GameWebSocketService.RoomDisbandedEvent evt)
        {
            Debug.Log($"[MFC] room_disbanded ▶ roomId={evt.roomId} reason={evt.reason} — clearing RoomState.  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            RoomState.Instance?.ClearRoom();
        }

        // ── Public: reset (called by RoomState.ClearRoom via NetworkGameManager) ──

        /// <summary>Reset flow state for a fresh match cycle.</summary>
        public void ResetFlow()
        {
            _lastHandledMatchId = null;
        }
    }
}
