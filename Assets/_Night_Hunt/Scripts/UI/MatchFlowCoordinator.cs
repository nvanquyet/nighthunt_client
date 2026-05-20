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

        // Guard: prevent double-subscription if OnEnable fires before GameEventBus.Awake.
        private bool _eventBusSubscribed;
        private Coroutine _subscribeRoutine;

        // ── Singleton lifecycle ───────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            // Nothing — subscriptions go in OnEnable/OnDisable to support scene reload.
        }

        private void OnEnable()
        {
            SubscribeWSEvents();
            if (!_eventBusSubscribed)
                _subscribeRoutine = StartCoroutine(SubscribeWhenEventBusReady());
        }

        // Start() runs after all Awake() calls — safe retry if OnEnable() fired too early.
        private void Start()
        {
            SubscribeWSEvents();
        }

        private void OnDisable()
        {
            if (_subscribeRoutine != null)
            {
                StopCoroutine(_subscribeRoutine);
                _subscribeRoutine = null;
            }

            UnsubscribeWSEvents();
        }

        // ── Domain event subscription ────────────────────────────────────────

        private void SubscribeWSEvents()
        {
            if (_eventBusSubscribed) return;

            var bus = GameEventBus.Instance;
            if (bus == null) return;

            bus.OnMatchReady     += HandleMatchReady;
            bus.OnDsReady        += HandleDsReady;
            bus.OnMatchCancelled += HandleMatchCancelled;
            bus.OnMatchEnded     += HandleMatchEnded;
            bus.OnRoomDisbanded  += HandleRoomDisbandedDuringGame;
            _eventBusSubscribed = true;
            _subscribeRoutine = null;
        }

        private void UnsubscribeWSEvents()
        {
            if (!_eventBusSubscribed) return;

            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                bus.OnMatchReady     -= HandleMatchReady;
                bus.OnDsReady        -= HandleDsReady;
                bus.OnMatchCancelled -= HandleMatchCancelled;
                bus.OnMatchEnded     -= HandleMatchEnded;
                bus.OnRoomDisbanded  -= HandleRoomDisbandedDuringGame;
            }
            _eventBusSubscribed = false;
        }

        private System.Collections.IEnumerator SubscribeWhenEventBusReady()
        {
            const float timeout = 15f;
            float elapsed = 0f;

            while (!_eventBusSubscribed && elapsed < timeout)
            {
                SubscribeWSEvents();
                if (_eventBusSubscribed)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_eventBusSubscribed)
                Debug.LogWarning("[MFC] GameEventBus was not available; match flow events are not subscribed.");
            _subscribeRoutine = null;
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

            Debug.Log($"[FLOW §1] MFC match_ready ▶ matchId={e.matchId} mode={e.gameMode} mapId={e.mapId} roomCode={e.roomCode} dsIp={e.dsIp} dsPort={e.dsPort}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            // Resolve scene from mapId.
            SceneId sceneId;
            if (string.IsNullOrEmpty(e.mapId))
            {
                // null/empty mapId: server assigned the default map — GameMap_01 is correct.
                sceneId = SceneId.GameMap_01;
                Debug.Log("[MFC] match_ready — mapId is null/empty, using default GameMap_01.");
            }
            else if (MapConfig.TryGetById(e.mapId, out MapEntry mapEntry))
            {
                sceneId = mapEntry.sceneId;
            }
            else
            {
                // Server sent a mapId the client does not recognise.
                // Most likely cause: GameConfig was not loaded before match_ready arrived
                // (LoginView fire-and-forget regression) or the client build is stale.
                // Abort to prevent loading the wrong game scene.
                Debug.LogError($"[MFC] FATAL: mapId='{e.mapId}' not found in MapConfig. " +
                               "Config may not be loaded yet. Aborting match to prevent wrong-scene load.");
                _lastHandledMatchId = null; // allow re-processing if server resends match_ready
                var errorToast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
                errorToast?.Show("Match Error", "Map data is not loaded. Please restart and try again.");
                return;
            }

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

            // Phase 3: store players from match_ready so MatchLoadingOverlay can show cards.
            if (e.players != null && e.players.Length > 0)
                RoomState.Instance?.SetMatchReadyPlayers(e.players);
            else
                Debug.LogWarning("[MFC] match_ready has no players[] — overlay will use RoomState.CurrentRoom.players fallback.");

            // If match_ready already contains the DS address, pre-populate RoomState and signal NGM.
            // This handles two cases:
            //   1. Local testing: DS is already running, ds_ready WS may never arrive.
            //   2. Production: server pre-allocates DS — ds_ready may arrive later and overwrite
            //      with identical values (idempotent), or may not arrive at all.
            // TryConnectIfReady() is safe here: it still waits for _gameSceneLoaded.
            if (!isRelay && !string.IsNullOrEmpty(e.dsIp) && e.dsPort > 0)
            {
                Debug.Log($"[MFC] match_ready contains dsIp={e.dsIp}:{e.dsPort} — pre-populating RoomState. Waiting for real ds_ready before connecting.");
                RoomState.Instance?.SetDedicatedServer(e.dsIp, (ushort)e.dsPort, e.matchId, e.mapId);
                // Do NOT call NotifyDsReady() here — the DS container is still booting.
                // SignalDsReady() will be called by HandleDsReady() when the backend
                // broadcasts ds_ready (after DS calls /api/ds/game-ready).
            }

            // Show overlay THEN load scene — overlay is on PersistentUICanvas (DontDestroyOnLoad).
            Debug.Log($"[FLOW §2] MFC → Show MatchLoadingOverlay + LoadScene={sceneId}  RoomState.players={RoomState.Instance?.CurrentRoom?.players?.Count ?? 0}");
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
            Debug.Log($"[FLOW §4] MFC ds_ready ▶ dsIp={e.dsIp} dsPort={e.dsPort} matchId={e.matchId} mapId={e.mapId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            Debug.Log($"[FLOW §4] MFC ds_ready — RoomState.DsIp={room?.DsIp} RoomState.DsPort={room?.DsPort}");
            NightHunt.Networking.NetworkGameManager.SignalDsReady();
        }

        // ── match_cancelled ───────────────────────────────────────────────────

        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent e)
        {
            Debug.Log($"[MFC] match_cancelled \u25ba reason={e.reason}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            _lastHandledMatchId = null;

            MatchFoundOverlay.Instance?.Hide();
            MatchLoadingOverlay.Instance?.Hide();
            RoomState.Instance?.ClearNetworkSession();

            string reason = !string.IsNullOrEmpty(e.reason) ? e.reason : "Match was cancelled.";
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show("Matchmaking", $"Match cancelled: {reason}");

            if (SceneLoader.HasPendingSceneLoad || SceneLoader.IsInGameplayScene)
                SceneLoader.ReturnHomeFromGameplayFlow();
        }

        // ── match_ended (WS backend confirmation) ────────────────────────────

        private void HandleMatchEnded(GameWebSocketService.MatchEndedWsEvent e)
        {
            // ResultsView subscribes OnMatchEnded directly for ELO/coins display.
            // MatchFlowCoordinator resets flow state so the next match starts clean.
            Debug.Log($"[MFC] match_ended ▶ matchId={e.matchId} winner={e.winnerTeamId} reason={e.endReason}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            _lastHandledMatchId = null;

            // Map WS player results → MatchResult structs so ResultsView can show real ELO/coins.
            // FishNet RPC only carries EloChange=0 stubs for Ranked_DS — this WS event has the truth.
            if (e.playerResults != null && e.playerResults.Length > 0)
            {
                var results = new NightHunt.Gameplay.Core.Events.MatchResult[e.playerResults.Length];
                for (int i = 0; i < e.playerResults.Length; i++)
                {
                    var r = e.playerResults[i];
                    results[i] = new NightHunt.Gameplay.Core.Events.MatchResult
                    {
                        BackendPlayerId = r.userId.ToString(),
                        DisplayName     = r.displayName ?? string.Empty,
                        TeamId          = r.teamId,
                        Kills           = r.kills,
                        Deaths          = r.deaths,
                        Score           = r.score,
                        EloChange       = r.eloChange,
                        CoinChange      = r.coinChange,
                    };
                }
                NightHunt.Gameplay.Core.Events.GameplayEventBus.Instance?.Publish(
                    new NightHunt.Gameplay.Core.Events.MatchEndedWsResultsEvent { PlayerResults = results });

                // Update local coin balance immediately so Home scene shows the new total.
                var session = NightHunt.State.SessionState.Instance;
                if (session != null)
                {
                    string localId = session.UserId.ToString();
                    foreach (var r in e.playerResults)
                        if (r.userId.ToString() == localId)
                        {
                            session.SetCoins(r.coinsTotal);
                            break;
                        }
                }
            }
        }

        // ── room_disbanded (global handler — covers game scene where PartyCustomModeView is inactive) ──

        /// <summary>
        /// Handles room_disbanded arriving at any time (including during gameplay).
        /// PartyCustomModeView only handles this when it is active; this persistent handler
        /// ensures RoomState is always cleared, preventing the "leave custom room" block
        /// from triggering on the next ranked queue attempt.
        /// </summary>
        private void HandleRoomDisbandedDuringGame(GameWebSocketService.RoomDisbandedEvent evt)
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool isGameScene = currentScene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase);
            var currentRoomState = RoomState.Instance;
            long currentRoomId = currentRoomState?.RoomId ?? 0L;

            if (evt.roomId > 0 && currentRoomId > 0 && evt.roomId != currentRoomId)
            {
                Debug.LogWarning($"[MFC] Ignoring room_disbanded for roomId={evt.roomId}; current roomId={currentRoomId}.");
                return;
            }

            if (!isGameScene)
            {
                Debug.Log($"[MFC] room_disbanded roomId={evt.roomId} reason={evt.reason} received outside game scene; PartyCustomModeView owns lobby cleanup.");
                return;
            }
            Debug.Log($"[MFC] room_disbanded ▶ roomId={evt.roomId} reason={evt.reason} — clearing RoomState.  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            RoomState.Instance?.ClearRoom();

            // Reset so the next match_ready is not incorrectly treated as a duplicate.
            _lastHandledMatchId = null;

            // If the player is in the game map scene but FishNet never connected (DS boot failed /
            // connection error), navigate back to Home so they are not permanently stranded.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool inGameScene = activeScene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase);
            bool fishNetConnected = NightHunt.Networking.NetworkGameManager.Instance != null
                                 && NightHunt.Networking.NetworkGameManager.Instance.IsClient;

            if (inGameScene && !fishNetConnected)
            {
                string reason = !string.IsNullOrEmpty(evt.reason) ? evt.reason : "unknown";
                Debug.LogWarning($"[MFC] room_disbanded in game scene while not connected (reason={reason}) — returning to Home.");
                var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
                toast?.Show("Match", $"Room was disbanded ({reason}). Returning to Home...");
                NightHunt.Core.SceneLoader.LoadHome();
            }
        }

        // ── Public: reset (called by RoomState.ClearRoom via NetworkGameManager) ──

        /// <summary>Reset flow state for a fresh match cycle.</summary>
        public void ResetFlow()
        {
            _lastHandledMatchId = null;
        }
    }
}
