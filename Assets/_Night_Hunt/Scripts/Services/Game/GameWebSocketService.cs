using System;
using System.Collections.Generic;
using NativeWebSocket;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Services.Backend;
using NightHunt.Data.DTOs;
using NightHunt.Networking;
using NightHunt.State;
using NightHunt.Utils;
using UnityEngine;

namespace NightHunt.Services.Game
{
    /// <summary>
    /// GameWebSocketService - Unified WebSocket service for all game events
    /// Connected after login/auto-login, kept alive throughout the session
    /// Handles: session events (force_logout, session_expired) and room events (room_updated, player_joined, etc.)
    /// </summary>
    public class GameWebSocketService : SingletonPersistent<GameWebSocketService>
    {

        [Header("WebSocket Settings (from BackendConfig)")]
        [Tooltip("Optional local override of wsPath; leave empty to use BackendConfig.wsPath.")]
        [SerializeField] private string wsPathOverride = "";

        // ── Main-thread dispatch queue ─────────────────────────────────────────
        /// <summary>
        /// NativeWebSocket fires OnOpen / OnError / OnClose from a background thread pool thread.
        /// Any Unity API call made from those callbacks (gameObject, transform, Debug.Log via
        /// ConditionalLogger, etc.) will throw "can only be called from the main thread".
        /// Actions enqueued here are drained by Update() on the main thread.
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action>
            _mainThreadActions = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

        private WebSocket webSocket;
        private bool isConnected = false;
        private bool isConnecting = false;
        private string accessToken;
        private string connectionToken = null; // track current connect attempt
        private bool allowAutoReconnect = true;
        private int reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 5;
        /// <summary>UTC time when the current connection was last confirmed open. Used to determine
        /// whether a close was a "stable" disconnect (reset counter) or an immediate failure (keep counting).</summary>
        private DateTime _connectedSince = DateTime.MinValue;
        /// <summary>Cancels any pending backoff Task.Delay reconnect when Connect() is called explicitly (prevents double-connect race).</summary>
        private CancellationTokenSource _reconnectCts;
        /// <summary>Cancels the '60 s stable → reset counter' timer when the connection drops before that window.</summary>
        private CancellationTokenSource _stableResetCts;
        private const float PING_INTERVAL = 15f;         // Send ping every 15 s to prevent server stale-eviction
        private float _pingTimer = PING_INTERVAL;

        public bool IsWsConnected => isConnected;
        public bool IsConnecting => isConnecting;

        // Session Events
        public event Action OnForceLogout;
        public event Action OnSessionExpired;
        
        // Room Events
        public event Action<RoomResponse> OnRoomUpdated;
        public event Action<PlayerJoinedEvent> OnPlayerJoined;
        public event Action<PlayerLeftEvent> OnPlayerLeft;
        public event Action<PlayerReadyEvent> OnPlayerReady;
        public event Action<TeamChangedEvent> OnTeamChanged;
        public event Action<RoomStatusChangedEvent> OnRoomStatusChanged;
        public event Action<SwapRequestEvent> OnSwapRequest;
        public event Action<SwapRequestStatusEvent> OnSwapRequestStatus;
        public event Action<GameStartingEvent> OnGameStarting;

        // Matchmaking Events
        public event Action<MatchFoundEvent>     OnMatchFound;
        public event Action<MatchReadyEvent>     OnMatchReady;
        public event Action<MatchCancelledEvent> OnMatchCancelled;
        
        // Friend Events
        public event Action<FriendStatusChangedEvent> OnFriendStatusChanged;
        public event Action<FriendRequestEvent> OnFriendRequestReceived;
        public event Action<FriendRequestAcceptedEvent> OnFriendRequestAccepted;
        public event Action<FriendRequestDeclinedEvent>   OnFriendRequestDeclined;
        public event Action<FriendRequestCancelledEvent> OnFriendRequestCancelled;
        public event Action<FriendRemovedEvent> OnFriendRemoved;
        
        // Party Events
        public event Action<PartyInvitationEvent> OnPartyInvitationReceived;
        public event Action<PartyMemberJoinedEvent> OnPartyMemberJoined;
        public event Action<PartyMemberLeftEvent> OnPartyMemberLeft;
        public event Action<PartyMemberKickedEvent> OnPartyMemberKicked;
        public event Action<PartyDisbandedEvent>    OnPartyDisbanded;
        public event Action<PartyHostChangedEvent>  OnPartyHostChanged;
        public event Action<PartyStatusChangedEvent> OnPartyStatusChanged;
        
        // Connection Events
        public event Action OnDisconnected;
        /// <summary>Fired every time the WebSocket successfully opens (initial connect AND reconnects).</summary>
        public event Action OnConnected;
        /// <summary>Fired when auto-reconnect gives up after MAX_RECONNECT_ATTEMPTS.</summary>
        public event Action OnReconnectFailed;
        public event Action<string> OnError;



        private void Update()
        {
            // Drain actions dispatched from background WS threads (OnOpen / OnError / OnClose)
            while (_mainThreadActions.TryDequeue(out var mainAction))
            {
                try { mainAction(); }
                catch (Exception ex) { Debug.LogError($"[GameWebSocketService] Main-thread action threw: {ex.Message}"); }
            }

            // Dispatch WebSocket message queue
            #if !UNITY_WEBGL || UNITY_EDITOR
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                // Heartbeat ping — keeps server from evicting stale sessions (server timeout = 30 s)
                _pingTimer -= Time.unscaledDeltaTime;
                if (_pingTimer <= 0f)
                {
                    _pingTimer = PING_INTERVAL;
                    _ = webSocket.SendText("{\"type\":\"ping\"}");
                }

                // Process all queued WS messages this frame.
                // NativeWebSocket.DispatchMessageQueue() dispatches ALL pending messages at once;
                // per-message throttling is not supported by this library.
                // TODO: Switch to a WebSocket library with per-message dispatch if burst traffic causes frame drops.
                try
                {
                    webSocket.DispatchMessageQueue();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameWebSocketService] Error dispatching messages: {ex.Message}");
                }
            }
            #endif
        }

        protected override void OnDestroy()
        {
            Disconnect();
            base.OnDestroy();
        }

        /// <summary>
        /// Connect to Game WebSocket (called after login/auto-login)
        /// </summary>
        public async Task<bool> Connect()
        {
            if (isConnecting || isConnected)
            {
                ConditionalLogger.LogWarning("GameWebSocketService", "Already connecting or connected");
                return isConnected;
            }

            // Cancel any pending backoff reconnect so it doesn't fire a second concurrent Connect()
            _reconnectCts?.Cancel();
            _reconnectCts = null;

            if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
            {
                ConditionalLogger.LogWarning("GameWebSocketService", "Cannot connect - user not authenticated");
                return false;
            }

            accessToken = SessionState.Instance.AccessToken;
            isConnecting = true;
            connectionToken = Guid.NewGuid().ToString();
            string thisToken = connectionToken;

            try
            {
                string wsUrl = BuildWebSocketUrl();
                string wsPath = ResolveWsPath();
                wsUrl = $"{wsUrl}{(wsUrl.EndsWith(wsPath) ? "" : wsPath)}?token={Uri.EscapeDataString(accessToken)}";
                ConditionalLogger.Log("GameWebSocketService", "Connecting to Game WebSocket...");
                ConditionalLogger.Log("GameWebSocketService", $"WebSocket URL: {wsUrl}");

                await ConnectWebSocket(wsUrl, thisToken);
                
                if (isConnected && thisToken == connectionToken)
                {
                    ConditionalLogger.Log("GameWebSocketService", "Game WebSocket connected successfully");
                    // NOTE: reconnectAttempts is intentionally NOT reset here.
                    // It is reset inside OnClose only when the connection was stable (>= 10 s).
                    // Resetting here caused the infinite-loop: connect→close→reconnectAttempts became 0 immediately.
                    return true;
                }
                else
                {
                    ConditionalLogger.LogWarning("GameWebSocketService", "Failed to connect Game WebSocket");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("GameWebSocketService", $"Error connecting: {ex.Message}", ex);
                isConnecting = false;
                return false;
            }
        }

        private async Task ConnectWebSocket(string url, string token)
        {
            try
            {
                // Close any existing connection first
                if (webSocket != null && webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await webSocket.Close();
                    }
                    catch (Exception ex)
                    {
                        ConditionalLogger.LogWarning("GameWebSocketService", $"Error closing existing WebSocket: {ex.Message}");
                    }
                }

                // Create WebSocket instance
                webSocket = new WebSocket(url);

                // Set up event handlers.
                // IMPORTANT: NativeWebSocket fires OnOpen / OnError / OnClose from a background
                // thread pool thread on non-WebGL platforms. We must NOT touch any Unity object
                // directly here — enqueue to _mainThreadActions and handle in Update() instead.

                webSocket.OnOpen += () =>
                {
                    // Capture for closure — connectionToken may change by next frame.
                    string capturedToken = token;
                    _mainThreadActions.Enqueue(() =>
                    {
                        if (capturedToken != connectionToken) return;
                        ConditionalLogger.Log("GameWebSocketService", "WebSocket opened");
                        isConnected = true;
                        isConnecting = false;
                        _connectedSince = DateTime.UtcNow;
                        OnConnected?.Invoke();

                        // Start a 60 s stability timer — if the connection survives this long,
                        // the session is genuinely re-established and the retry counter is reset.
                        // The timer is cancelled in OnClose if the connection drops before 60 s.
                        _stableResetCts?.Cancel();
                        _stableResetCts = new CancellationTokenSource();
                        var stableCts = _stableResetCts;
                        _ = Task.Delay(60000, stableCts.Token).ContinueWith(t =>
                        {
                            if (!t.IsCanceled)
                                _mainThreadActions.Enqueue(() =>
                                {
                                    reconnectAttempts = 0;
                                    ConditionalLogger.Log("GameWebSocketService", "Connection stable for 60 s — reconnect counter reset.");
                                });
                        });
                    });
                };

                webSocket.OnMessage += (bytes) =>
                {
                    if (token != connectionToken) return;
                    string message = Encoding.UTF8.GetString(bytes);
                    HandleMessage(message);
                };

                webSocket.OnError += (error) =>
                {
                    string capturedToken = token;
                    string capturedError = error;
                    _mainThreadActions.Enqueue(() =>
                    {
                        if (capturedToken != connectionToken) return;
                        ConditionalLogger.LogError("GameWebSocketService", $"WebSocket error: {capturedError}");
                        isConnected = false;
                        isConnecting = false;
                        OnError?.Invoke(capturedError);
                    });
                };

                webSocket.OnClose += (code) =>
                {
                    string capturedToken = token;
                    WebSocketCloseCode capturedCode = code;
                    _mainThreadActions.Enqueue(() =>
                    {
                        if (capturedToken != connectionToken) return;

                        // Cancel the stability timer — connection dropped before the grace period.
                        _stableResetCts?.Cancel();

                        bool wasStable = _connectedSince != DateTime.MinValue &&
                                         (DateTime.UtcNow - _connectedSince).TotalSeconds >= 10.0;
                        _connectedSince = DateTime.MinValue;

                        ConditionalLogger.Log("GameWebSocketService",
                            $"WebSocket closed: {capturedCode} (stableConnection={wasStable})");
                        isConnected  = false;
                        isConnecting = false;
                        OnDisconnected?.Invoke();

                        if (!allowAutoReconnect) return;
                        if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated) return;

                        // Normal (1000) = intentional server-side close (e.g. server restart or session eviction).
                        // Still reconnect, but use a longer initial delay to avoid spamming a restarting server.
                        int baseDelayMs = (capturedCode == WebSocketCloseCode.Normal) ? 5000 : 1000;

                        // NOTE: reconnectAttempts is NO LONGER reset here on wasStable.
                        // Resetting here caused an infinite loop: every stable drop reset the counter to 0,
                        // so reconnectAttempts was always 1 and the MAX_RECONNECT_ATTEMPTS cap was never reached.
                        // The counter is now reset only after 60 s of continuous stability (see OnOpen timer).

                        reconnectAttempts++;
                        if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS)
                        {
                            ConditionalLogger.LogError("GameWebSocketService",
                                $"Reconnect attempts exceeded limit ({MAX_RECONNECT_ATTEMPTS}). Stopping auto-reconnect.");
                            OnReconnectFailed?.Invoke();
                            return;
                        }

                        // Exponential backoff: baseDelay * 2^(attempt-1), capped at 60 s
                        int delayMs = Mathf.Min(baseDelayMs * (1 << (reconnectAttempts - 1)), 60000);
                        ConditionalLogger.Log("GameWebSocketService",
                            $"Reconnecting in {delayMs}ms... (attempt {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}, closeCode={capturedCode})");

                        _reconnectCts?.Cancel();
                        _reconnectCts = new CancellationTokenSource();
                        var cts = _reconnectCts;
                        _ = Task.Delay(delayMs, cts.Token).ContinueWith(t =>
                        {
                            if (!t.IsCanceled && allowAutoReconnect
                                && SessionState.Instance != null
                                && SessionState.Instance.IsAuthenticated)
                                // Post reconnect back to main thread so the new Connect() async
                                // chain inherits Unity's SynchronizationContext.
                                _mainThreadActions.Enqueue(() => _ = Connect());
                        });
                    });
                };

                // Connect with timeout
                var connectTask = Task.Run(async () => await webSocket.Connect());
                var startTime = DateTime.Now;
                var timeout = TimeSpan.FromSeconds(8); // give more time to establish

                _ = connectTask; // Fire and forget - we'll check isConnected instead

                // Poll for connection success
                while (!isConnected && (DateTime.Now - startTime) < timeout)
                {
                    await Task.Delay(50);
                }

                if (!isConnected)
                {
                    ConditionalLogger.LogError("GameWebSocketService", "WebSocket connection timeout");
                    try
                    {
                        if (webSocket != null && webSocket.State != WebSocketState.Closed)
                        {
                            await webSocket.Close();
                        }
                    }
                    catch { }
                    throw new TimeoutException("WebSocket connection timeout");
                }
            }
            catch (TimeoutException)
            {
                ConditionalLogger.LogWarning("GameWebSocketService", "WebSocket connection timeout");
                isConnected = false;
                isConnecting = false;
                throw;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("GameWebSocketService", $"Error in ConnectWebSocket: {ex.Message}", ex);
                isConnected = false;
                isConnecting = false;
                throw;
            }
        }

        private string BuildWebSocketUrl()
        {
            // Priority: override -> BackendConfig -> fallback
            string baseUrl = null;

            if (GameManager.Instance != null && GameManager.Instance.BackendClient is BackendHttpClient bhc && bhc.Config != null)
            {
                var cfg = bhc.Config;
                if (!string.IsNullOrEmpty(cfg.overrideWsBaseUrl))
                {
                    baseUrl = cfg.overrideWsBaseUrl.TrimEnd('/');
                }
                else
                {
                    bool useSecure = cfg.ShouldUseSecureConnection();

                    string host = cfg.apiHost.TrimEnd('/');
                    string scheme = useSecure ? "wss://" : "ws://";
                    baseUrl = scheme + host;

                    Debug.Log($"[GameWebSocketService] Building WebSocket URL: {scheme}{host} (Environment: {cfg.environment}, Secure: {useSecure})");
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                // Fallback should stay secure by default. Production builds must not silently route to
                // a dev-only insecure WebSocket endpoint.
                baseUrl = "wss://localhost:8443";
                Debug.LogWarning("[GameWebSocketService] Using fallback WebSocket URL: wss://localhost:8443");
            }

            baseUrl = baseUrl.TrimEnd('/');
            return baseUrl;
        }

        private string ResolveWsPath()
        {
            if (!string.IsNullOrEmpty(wsPathOverride))
            {
                return wsPathOverride;
            }

            if (GameManager.Instance != null && GameManager.Instance.BackendClient is BackendHttpClient bhc && bhc.Config != null)
            {
                var cfg = bhc.Config;
                if (!string.IsNullOrEmpty(cfg.wsPath))
                {
                    return cfg.wsPath;
                }
            }

            return "/api/ws/game"; // context-path /api + /ws/game
        }

        private void HandleMessage(string message)
        {
            try
            {
                var messageData = JsonUtility.FromJson<WebSocketMessage>(message);
                if (messageData == null || string.IsNullOrEmpty(messageData.type))
                {
                    Debug.LogWarning($"[GameWebSocketService] Invalid message format: {message}");
                    return;
                }

                Debug.Log($"[GameWebSocketService] Received message type: {messageData.type}");

                switch (messageData.type)
                {
                    // Session Events
                    case "force_logout":
                        var forceLogoutData = JsonUtility.FromJson<ForceLogoutEvent>(messageData.data);
                        HandleForceLogout(forceLogoutData);
                        break;

                    case "session_expired":
                        var sessionExpiredData = JsonUtility.FromJson<SessionExpiredEvent>(messageData.data);
                        HandleSessionExpired(sessionExpiredData);
                        break;

                    case "connected":
                        Debug.Log("[GameWebSocketService] Connection confirmed by server");
                        break;

                    // Room Events
                    case "room_updated":
                        var roomUpdate = JsonUtility.FromJson<RoomResponse>(messageData.data);
                        if (roomUpdate != null)
                        {
                            OnRoomUpdated?.Invoke(roomUpdate);
                            // Update RoomState
                            if (RoomState.Instance != null)
                            {
                                RoomState.Instance.SetRoom(roomUpdate);
                            }
                        }
                        break;

                    case "player_joined":
                        var playerJoined = JsonUtility.FromJson<PlayerJoinedEvent>(messageData.data);
                        if (playerJoined != null)
                            OnPlayerJoined?.Invoke(playerJoined);
                        break;

                    case "player_left":
                        var playerLeft = JsonUtility.FromJson<PlayerLeftEvent>(messageData.data);
                        if (playerLeft != null)
                            OnPlayerLeft?.Invoke(playerLeft);
                        break;

                    case "player_ready":
                        var playerReady = JsonUtility.FromJson<PlayerReadyEvent>(messageData.data);
                        if (playerReady != null)
                            OnPlayerReady?.Invoke(playerReady);
                        break;

                    case "team_changed":
                        var teamChanged = JsonUtility.FromJson<TeamChangedEvent>(messageData.data);
                        if (teamChanged != null)
                        {
                            // Map newTeam/newSlot to team/slot for compatibility
                            if (teamChanged.newTeam != 0 || teamChanged.newSlot != 0)
                            {
                                teamChanged.team = teamChanged.newTeam;
                                teamChanged.slot = teamChanged.newSlot;
                            }
                            OnTeamChanged?.Invoke(teamChanged);
                        }
                        break;

                    case "room_status_changed":
                        var roomStatusChanged = JsonUtility.FromJson<RoomStatusChangedEvent>(messageData.data);
                        if (roomStatusChanged != null)
                        {
                            // NOTE: Relay session info (relayHost/relayPort) comes from the
                            // "game_starting" event which fires BEFORE room_status_changed.
                            // RoomState.SetRelaySession() is called there so MatchNetworkConnector
                            // can connect when the loading scene opens.
                            OnRoomStatusChanged?.Invoke(roomStatusChanged);
                        }
                        break;

                    case "swap_request":
                        var swapRequest = JsonUtility.FromJson<SwapRequestEvent>(messageData.data);
                        if (swapRequest != null)
                        {
                            // Map requesterId to fromUserId for compatibility
                            if (swapRequest.requesterId != 0 && swapRequest.fromUserId == 0)
                            {
                                swapRequest.fromUserId = swapRequest.requesterId;
                            }
                            OnSwapRequest?.Invoke(swapRequest);
                        }
                        break;

                    case "swap_request_status":
                        var swapRequestStatus = JsonUtility.FromJson<SwapRequestStatusEvent>(messageData.data);
                        if (swapRequestStatus != null)
                            OnSwapRequestStatus?.Invoke(swapRequestStatus);
                        break;

                    // swap_accepted fires when the TARGET accepts — server broadcasts
                    // the updated RoomResponse to all room members.
                    // Note: swap_request_status ACCEPTED is never sent by the server;
                    // only REJECTED is sent. We synthesize an ACCEPTED status event so
                    // the requester's waiting modal (HandleSwapRequestStatus) closes.
                    case "swap_accepted":
                        var swapAcceptedRoom = JsonUtility.FromJson<RoomResponse>(messageData.data);
                        if (swapAcceptedRoom != null)
                        {
                            if (RoomState.Instance != null) RoomState.Instance.SetRoom(swapAcceptedRoom);
                            OnRoomUpdated?.Invoke(swapAcceptedRoom);
                            // Synthesize ACCEPTED status so requester can close its waiting modal.
                            // requestId=0 means "any pending swap was accepted".
                            OnSwapRequestStatus?.Invoke(new SwapRequestStatusEvent
                            {
                                requestId = 0L,
                                status    = "ACCEPTED",
                                room      = swapAcceptedRoom
                            });
                        }
                        break;

                    // game_starting fires before room_status_changed IN_GAME.
                    // Custom-relay mode: stores relayHost/relayPort in RoomState so
                    // MatchNetworkConnector can connect when the loading scene opens.
                    case "game_starting":
                        var gameStarting = JsonUtility.FromJson<GameStartingEvent>(messageData.data);
                        if (gameStarting != null)
                        {
                            if (!string.IsNullOrEmpty(gameStarting.relayHost) && gameStarting.relayPort > 0
                                && RoomState.Instance != null)
                            {
                                long localUid = NightHunt.State.SessionState.Instance?.UserId ?? 0L;
                                bool isRelayHost = gameStarting.room?.ownerId == localUid;
                                string sid = gameStarting.room?.matchId ?? "";
                                RoomState.Instance.SetRelaySession(
                                    sid,
                                    gameStarting.relayHost,
                                    (ushort)gameStarting.relayPort,
                                    isRelayHost);
                                Debug.Log($"[GameWebSocketService] game_starting: relay={gameStarting.relayHost}:{gameStarting.relayPort} isHost={isRelayHost}");
                            }
                            OnGameStarting?.Invoke(gameStarting);
                        }
                        break;

                    // Matchmaking Events
                    case "match_found":
                        var matchFound = JsonUtility.FromJson<MatchFoundEvent>(messageData.data);
                        if (matchFound != null)
                            OnMatchFound?.Invoke(matchFound);
                        break;

                    case "match_ready":
                        var matchReady = JsonUtility.FromJson<MatchReadyEvent>(messageData.data);
                        if (matchReady != null)
                        {
                            // Store DS info in RoomState immediately
                            if (RoomState.Instance != null && !string.IsNullOrEmpty(matchReady.dsIp))
                                RoomState.Instance.SetDedicatedServer(
                                    matchReady.dsIp, (ushort)matchReady.dsPort, matchReady.matchId, matchReady.mapId);
                            OnMatchReady?.Invoke(matchReady);
                        }
                        break;

                    case "match_cancelled":
                        var matchCancelled = JsonUtility.FromJson<MatchCancelledEvent>(messageData.data);
                        if (matchCancelled != null)
                            OnMatchCancelled?.Invoke(matchCancelled);
                        break;

                    // ────────────────────────────────────────────────────────────
                    // Friend Events
                    // ────────────────────────────────────────────────────────────
                    case "friend_status_changed":
                        var friendStatus = JsonUtility.FromJson<FriendStatusChangedEvent>(messageData.data);
                        if (friendStatus != null)
                        {
                            friendStatus.status = friendStatus.newStatus; // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Friend status changed: {friendStatus.userId} -> {friendStatus.newStatus}");
                            OnFriendStatusChanged?.Invoke(friendStatus);
                            // CACHE: Invalidate friends list (friend online/offline status changed)
                            APICache.Invalidate(APICache.KEY_FRIENDS_LIST);
                        }
                        break;

                    case "friend_request_received":
                        var friendRequest = JsonUtility.FromJson<FriendRequestEvent>(messageData.data);
                        if (friendRequest != null)
                        {
                            friendRequest.fromUserId   = friendRequest.requesterUserId;   // compat alias
                            friendRequest.fromUsername = friendRequest.requesterUsername; // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Friend request from: {friendRequest.fromUsername}");
                            OnFriendRequestReceived?.Invoke(friendRequest);
                            // CACHE: Invalidate friend requests (new request received)
                            APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_INCOMING);
                        }
                        break;

                    case "friend_request_accepted":
                        var friendAccepted = JsonUtility.FromJson<FriendRequestAcceptedEvent>(messageData.data);
                        if (friendAccepted != null)
                        {
                            friendAccepted.friendId = friendAccepted.requesterUserId;   // compat alias
                            friendAccepted.userId   = friendAccepted.addresseeUserId;   // compat alias
                            friendAccepted.username = friendAccepted.addresseeUsername; // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Friend request accepted by: {friendAccepted.addresseeUsername}");
                            OnFriendRequestAccepted?.Invoke(friendAccepted);
                            // CACHE: Invalidate both friends list and requests (new friend added)
                            APICache.InvalidateFriends();
                        }
                        break;

                    case "friend_request_declined":
                        var friendDeclined = JsonUtility.FromJson<FriendRequestDeclinedEvent>(messageData.data);
                        if (friendDeclined != null)
                        {
                            friendDeclined.fromUserId = friendDeclined.requesterUserId; // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Friend request declined by: {friendDeclined.requesterUserId}");
                            OnFriendRequestDeclined?.Invoke(friendDeclined);
                            // CACHE: Invalidate friend requests (request declined)
                            APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_OUTGOING);
                        }
                        break;

                    case "friend_request_cancelled":
                        var friendCancelled = JsonUtility.FromJson<FriendRequestCancelledEvent>(messageData.data);
                        if (friendCancelled != null)
                        {
                            friendCancelled.fromUserId = friendCancelled.requesterUserId;
                            ConditionalLogger.Log("GameWebSocketService", $"Friend request cancelled by: {friendCancelled.requesterUserId}");
                            OnFriendRequestCancelled?.Invoke(friendCancelled);
                            // CACHE: Invalidate incoming requests (sender cancelled)
                            APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_INCOMING);
                        }
                        break;

                    case "friend_removed":
                        var friendRemoved = JsonUtility.FromJson<FriendRemovedEvent>(messageData.data);
                        if (friendRemoved != null)
                        {
                            friendRemoved.friendId  = friendRemoved.friendUserId; // compat alias
                            friendRemoved.removedBy = friendRemoved.userId;       // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Friend removed: ID {friendRemoved.friendUserId}");
                            OnFriendRemoved?.Invoke(friendRemoved);
                            // CACHE: Invalidate friends list (friend removed)
                            APICache.Invalidate(APICache.KEY_FRIENDS_LIST);
                        }
                        break;

                    // ────────────────────────────────────────────────────────────
                    // Party Events
                    // ────────────────────────────────────────────────────────────
                    case "party_invitation_received":
                        var partyInvite = JsonUtility.FromJson<PartyInvitationEvent>(messageData.data);
                        if (partyInvite != null)
                        {
                            partyInvite.fromUserId   = partyInvite.inviterUserId;   // compat alias
                            partyInvite.fromUsername = partyInvite.inviterUsername; // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Party invitation from: {partyInvite.fromUsername}");
                            OnPartyInvitationReceived?.Invoke(partyInvite);
                            // CACHE: Invalidate party invitations (new invitation received)
                            APICache.Invalidate(APICache.KEY_PARTY_INVITATIONS);
                        }
                        break;

                    case "party_member_joined":
                        var partyJoined = JsonUtility.FromJson<PartyMemberJoinedEvent>(messageData.data);
                        if (partyJoined != null)
                        {
                            ConditionalLogger.Log("GameWebSocketService", $"Party member joined: {partyJoined.username}");
                            OnPartyMemberJoined?.Invoke(partyJoined);
                            // CACHE: Invalidate party state (member joined)
                            APICache.Invalidate(APICache.KEY_PARTY_STATE);
                        }
                        break;

                    case "party_member_left":
                        var partyLeft = JsonUtility.FromJson<PartyMemberLeftEvent>(messageData.data);
                        if (partyLeft != null)
                        {
                            ConditionalLogger.Log("GameWebSocketService", $"Party member left: ID {partyLeft.userId}");
                            OnPartyMemberLeft?.Invoke(partyLeft);
                            // CACHE: Invalidate party state (member left)
                            APICache.Invalidate(APICache.KEY_PARTY_STATE);
                        }
                        break;

                    case "party_member_kicked":
                        var partyKicked = JsonUtility.FromJson<PartyMemberKickedEvent>(messageData.data);
                        if (partyKicked != null)
                        {
                            partyKicked.kickedBy = partyKicked.kickerUserId; // compat alias
                            ConditionalLogger.Log("GameWebSocketService", $"Party member kicked: {partyKicked.kickedUserId} by {partyKicked.kickerUserId}");
                            OnPartyMemberKicked?.Invoke(partyKicked);
                            // CACHE: Invalidate party state (member kicked)
                            APICache.InvalidateParty();

                            // CRITICAL: If local user was kicked, show popup
                            if (SessionState.Instance != null && partyKicked.kickedUserId == SessionState.Instance.UserId)
                            {
                                ConditionalLogger.LogWarning("GameWebSocketService", "You were kicked from party!");
                            }
                        }
                        break;

                    case "party_disbanded":
                        var partyDisbanded = JsonUtility.FromJson<PartyDisbandedEvent>(messageData.data);
                        if (partyDisbanded != null)
                        {
                            ConditionalLogger.Log("GameWebSocketService", $"Party disbanded: ID {partyDisbanded.partyId}, reason: {partyDisbanded.reason}");
                            OnPartyDisbanded?.Invoke(partyDisbanded);
                            // CACHE: Invalidate all party-related caches (party disbanded)
                            APICache.InvalidateParty();
                        }
                        break;

                    case "party_host_changed":
                        var hostChanged = JsonUtility.FromJson<PartyHostChangedEvent>(messageData.data);
                        if (hostChanged != null)
                        {
                            ConditionalLogger.Log("GameWebSocketService", $"Party host changed: {hostChanged.oldHostUserId} → {hostChanged.newHostUserId}");
                            OnPartyHostChanged?.Invoke(hostChanged);
                            APICache.Invalidate(APICache.KEY_PARTY_STATE);
                        }
                        break;

                    case "party_status_changed":
                        var partyStatusChanged = JsonUtility.FromJson<PartyStatusChangedEvent>(messageData.data);
                        if (partyStatusChanged != null)
                        {
                            ConditionalLogger.Log("GameWebSocketService", $"Party status changed: party={partyStatusChanged.partyId} {partyStatusChanged.oldStatus} -> {partyStatusChanged.newStatus}");
                            OnPartyStatusChanged?.Invoke(partyStatusChanged);
                            APICache.Invalidate(APICache.KEY_PARTY_STATE);
                        }
                        break;

                    // friend_blocked: sent to the blocker — their friend list needs refresh.
                    case "friend_blocked":
                        APICache.InvalidateFriends();
                        // Reuse OnFriendRemoved so HomeView refreshes the friend list display.
                        OnFriendRemoved?.Invoke(new FriendRemovedEvent());
                        break;

                    default:
                        ConditionalLogger.LogWarning("GameWebSocketService", $"Unknown message type: {messageData.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("GameWebSocketService", $"Error handling message: {ex.Message}", ex);
            }
        }

        private void HandleForceLogout(ForceLogoutEvent evt)
        {
            ConditionalLogger.LogWarning("GameWebSocketService", $"Force logout received: {evt?.reason ?? "Unknown reason"}");
            
            // Clear session
            if (SessionState.Instance != null)
            {
                SessionState.Instance.ClearSession();
            }
            
            // Clear all caches on logout
            APICache.InvalidateOnLogout();
            
            // Disconnect WebSocket
            Disconnect(disableReconnect: true);
            
            // Trigger event
            OnForceLogout?.Invoke();
        }

        private void HandleSessionExpired(SessionExpiredEvent evt)
        {
            Debug.LogWarning($"[GameWebSocketService] Session expired: {evt?.message ?? "Session expired"}");
            
            // Clear session
            if (SessionState.Instance != null)
            {
                SessionState.Instance.ClearSession();
            }
            
            // Disconnect WebSocket
            Disconnect(disableReconnect: true);
            
            // Trigger event
            OnSessionExpired?.Invoke();
        }

        /// <summary>
        /// Disconnect WebSocket
        /// </summary>
        public async void Disconnect(bool disableReconnect = false)
        {
            if (disableReconnect)
            {
                allowAutoReconnect = false;
            }
            else
            {
                allowAutoReconnect = true;
            }

            isConnecting = false;
            
            if (webSocket != null)
            {
                try
                {
                    await webSocket.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameWebSocketService] Error closing WebSocket: {ex.Message}");
                }
            }

            if (webSocket != null)
            {
                webSocket = null;
            }

            isConnected = false;
        }

        // ==================== Event Data Classes ====================
        
        [Serializable]
        private class WebSocketMessage
        {
            public string type;
            public string data;
        }

        [Serializable]
        private class ForceLogoutEvent
        {
            public string reason;
            public string message;
        }

        [Serializable]
        private class SessionExpiredEvent
        {
            public string message;
        }

        // Room event classes (reuse from RoomWebSocketService if available, or define here)
        [Serializable]
        public class PlayerJoinedEvent
        {
            public long userId;
            public string username;
            public RoomResponse room;
        }

        [Serializable]
        public class PlayerLeftEvent
        {
            public long userId;
            public RoomResponse room;
        }

        [Serializable]
        public class PlayerReadyEvent
        {
            public long userId;
            public bool isReady;
            public RoomResponse room;
        }

        [Serializable]
        public class TeamChangedEvent
        {
            public long userId;
            public int team; // For compatibility with existing code
            public int slot; // For compatibility with existing code
            public int newTeam; // New team (if different)
            public int newSlot; // New slot (if different)
            public RoomResponse room;
        }

        [Serializable]
        public class RoomStatusChangedEvent
        {
            public string newStatus;
            public RoomResponse room;
        }

        [Serializable]
        public class SwapRequestEvent
        {
            public long requestId;
            public long fromUserId; // For compatibility
            public string fromUsername; // For compatibility
            public long requesterId; // Alias for fromUserId
            public long targetUserId;
            public int targetTeam;
            public int targetSlot;
            public SwapRequestDTO request; // Full request data if available
        }

        [Serializable]
        public class SwapRequestStatusEvent
        {
            public long requestId;
            public string status; // "ACCEPTED" or "REJECTED"
            public RoomResponse room; // Updated room state
        }

        // ── Matchmaking event data ───────────────────────────────────────────

        [Serializable]
        public class MatchFoundEvent
        {
            public string lobbyToken;
            public string gameMode;
            public long[] playerIds;
            /// <summary>
            /// Số giây để accept trước khi auto-decline.
            /// Backend đặt qua matchmaking.accept-timeout-seconds config.
            /// 0 = dùng defaultTimeoutSec trong MatchFoundOverlay.
            /// </summary>
            public int acceptTimeoutSeconds;
        }

        [Serializable]
        public class MatchReadyEvent
        {
            public string lobbyToken;
            public string gameMode;
            public string mapId;
            public string roomCode;
            public long   roomId;
            public string matchId;
            public string dsIp;
            public int    dsPort;
            public string sessionToken;
        }

        [Serializable]
        public class MatchCancelledEvent
        {
            public string lobbyToken;
            public string reason;
        }

        // ════════════════════════════════════════════════════════════════════
        // Friend Event DTOs
        // Server field names are primary (what JsonUtility deserializes).
        // Compatibility aliases are filled in HandleMessage before the event is fired.
        // ════════════════════════════════════════════════════════════════════

        [Serializable]
        public class FriendStatusChangedEvent
        {
            public long   userId;
            public string newStatus;      // Server field: "ONLINE"|"OFFLINE"|"IN_GAME"|"AWAY"
            public long   currentPartyId; // Server field
            public long   currentRoomId;  // Server field
            // Compatibility alias — set in handler from newStatus
            public string username;       // not sent by server; kept for old log references
            public string status;         // alias filled from newStatus in handler
        }

        [Serializable]
        public class FriendRequestEvent
        {
            public long   requestId;
            public string createdAt;
            // Server field names (PRIMARY)
            public long   requesterUserId;
            public string requesterUsername;
            // Compatibility aliases — filled from server fields in handler
            public long   fromUserId;
            public string fromUsername;
        }

        [Serializable]
        public class FriendRequestAcceptedEvent
        {
            // Server field names (PRIMARY)
            public long   requesterUserId;
            public long   addresseeUserId;
            public string addresseeUsername;
            // Compatibility aliases — filled in handler
            public long   friendId;
            public long   userId;
            public string username;
        }

        [Serializable]
        public class FriendRequestDeclinedEvent
        {
            // Server field names (PRIMARY)
            public long requesterUserId;
            public long addresseeUserId;
            // Compatibility alias — filled in handler
            public long requestId;   // kept for legacy log; server doesn't send this
            public long fromUserId;  // alias for requesterUserId
        }

        [Serializable]
        public class FriendRequestCancelledEvent
        {
            // Server field names (PRIMARY)
            public long requesterUserId;
            public long addresseeUserId;
            // Compatibility alias — filled in handler
            public long fromUserId;  // alias for requesterUserId
        }

        [Serializable]
        public class FriendRemovedEvent
        {
            // Server field names (PRIMARY)
            public long userId;       // user who removed the friend
            public long friendUserId; // user who was removed
            // Compatibility aliases — filled in handler
            public long friendId;   // alias for friendUserId
            public long removedBy;  // alias for userId
        }

        // ════════════════════════════════════════════════════════════════════
        // Party Event DTOs
        // ════════════════════════════════════════════════════════════════════

        [Serializable]
        public class PartyInvitationEvent
        {
            public long   invitationId;
            public long   partyId;
            public string createdAt;
            // Server field names (PRIMARY)
            public long   inviterUserId;
            public string inviterUsername;
            public long   inviteeUserId;
            // Compatibility aliases — filled in handler
            public long   fromUserId;
            public string fromUsername;
        }

        [Serializable]
        public class PartyMemberJoinedEvent
        {
            public long   partyId;
            public long   userId;
            public string username;
        }

        [Serializable]
        public class PartyMemberLeftEvent
        {
            public long partyId;
            public long userId;
        }

        [Serializable]
        public class PartyMemberKickedEvent
        {
            public long   partyId;
            public long   kickedUserId;
            // Server field name (PRIMARY)
            public long   kickerUserId;
            // Compatibility aliases — filled in handler
            public string kickedUsername;    // not sent by server
            public long   kickedBy;          // alias for kickerUserId
            public string kickedByUsername;  // not sent by server
            public string reason;            // not sent by server
        }

        [Serializable]
        public class PartyDisbandedEvent
        {
            public long   partyId;
            public long   hostUserId; // server field (only host receives this event)
            public string reason;     // not sent by server; kept for compat
        }

        [Serializable]
        public class PartyHostChangedEvent
        {
            public long partyId;
            public long oldHostUserId;
            public long newHostUserId;
        }

        [Serializable]
        public class PartyStatusChangedEvent
        {
            public long partyId;
            public string oldStatus;
            public string newStatus;
        }

        /// <summary>
        /// game_starting — sent by server before room_status_changed IN_GAME.
        /// Custom relay: relayHost/relayPort are set; dsAddress/dsPort are 0/null.
        /// The room field contains the updated RoomResponse (with matchId).
        /// </summary>
        [Serializable]
        public class GameStartingEvent
        {
            public string      relayToken; // relay auth token (future use)
            public string      relayHost;  // relay server host
            public int         relayPort;  // relay server port (0 if not relay)
            public RoomResponse room;      // updated room state at game start
        }
    }
}

