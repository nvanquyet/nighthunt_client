using System;
using System.Collections.Generic;
using NativeWebSocket;
using System.Text;
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

        private WebSocket webSocket;
        private bool isConnected = false;
        private bool isConnecting = false;
        private string accessToken;
        private string connectionToken = null; // track current connect attempt
        private bool allowAutoReconnect = true;
        private bool isShuttingDown = false;
        private int reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 5;
        private const int MAX_MESSAGES_PER_FRAME = 5; // PERF: Limit message processing to prevent frame drops

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
        public event Action<FriendRequestDeclinedEvent> OnFriendRequestDeclined;
        public event Action<FriendRemovedEvent> OnFriendRemoved;
        
        // Party Events
        public event Action<PartyInvitationEvent> OnPartyInvitationReceived;
        public event Action<PartyMemberJoinedEvent> OnPartyMemberJoined;
        public event Action<PartyMemberLeftEvent> OnPartyMemberLeft;
        public event Action<PartyMemberKickedEvent> OnPartyMemberKicked;
        public event Action<PartyDisbandedEvent>    OnPartyDisbanded;
        public event Action<PartyHostChangedEvent>  OnPartyHostChanged;
        
        // Connection Events
        public event Action OnDisconnected;
        public event Action<string> OnError;



        private void Update()
        {
            // Dispatch WebSocket message queue
            #if !UNITY_WEBGL || UNITY_EDITOR
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                // PERF-FIX: Process max 5 messages per frame to prevent frame drops
                // This ensures smooth 60fps even with message bursts (10+ messages)
                // Example: 10 friends come online at once = 10 messages dispatched over 2 frames
                int messagesProcessed = 0;
                
                while (messagesProcessed < MAX_MESSAGES_PER_FRAME)
                {
                    try
                    {
                        // Check if there are messages in queue
                        // Note: DispatchMessageQueue() processes all messages
                        // We need to manually limit it, but NativeWebSocket doesn't expose HasMessageInQueue()
                        // So we use a workaround: call DispatchMessageQueue() which processes ALL messages
                        // TODO: Consider switching to a WebSocket library that supports per-message dispatch
                        webSocket.DispatchMessageQueue();
                        break; // DispatchMessageQueue processes all, so break after one call
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameWebSocketService] Error dispatching messages: {ex.Message}");
                        break;
                    }
                }
            }
            #endif
        }

        private void OnDestroy()
        {
            Disconnect();
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
                    reconnectAttempts = 0; // reset on success
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

                // Set up event handlers
                webSocket.OnOpen += () =>
                {
                    if (token != connectionToken) return;
                    ConditionalLogger.Log("GameWebSocketService", "WebSocket opened");
                    isConnected = true;
                    isConnecting = false;
                };

                webSocket.OnMessage += (bytes) =>
                {
                    if (token != connectionToken) return;
                    string message = Encoding.UTF8.GetString(bytes);
                    HandleMessage(message);
                };

                webSocket.OnError += (error) =>
                {
                    if (token != connectionToken) return;
                    ConditionalLogger.LogError("GameWebSocketService", $"WebSocket error: {error}");
                    isConnected = false;
                    isConnecting = false;
                    OnError?.Invoke(error);
                };

                webSocket.OnClose += (code) =>
                {
                    if (token != connectionToken) return;
                    ConditionalLogger.Log("GameWebSocketService", $"WebSocket closed: {code}");
                    isConnected = false;
                    isConnecting = false;
                    OnDisconnected?.Invoke();
                    
                    // Try to reconnect if session is still valid
                    if (allowAutoReconnect && SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
                    {
                        reconnectAttempts++;
                        if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS)
                        {
                            ConditionalLogger.LogError("GameWebSocketService", $"Reconnect attempts exceeded limit ({MAX_RECONNECT_ATTEMPTS}). Stopping auto-reconnect.");
                            return;
                        }
                        ConditionalLogger.Log("GameWebSocketService", $"Attempting to reconnect... (attempt {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})");
                        _ = Connect(); // Fire and forget
                    }
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
                    // SEC-FIX: Use new ShouldUseSecureConnection() method for consistent HTTPS/WSS logic
                    bool useSecure = cfg.ShouldUseSecureConnection();
                    string host = cfg.apiHost.TrimEnd('/');

                    string scheme = useSecure ? "wss://" : "ws://";
                    baseUrl = scheme + host;
                    
                    Debug.Log($"[GameWebSocketService] Building WebSocket URL: {scheme}{host} (Environment: {cfg.environment}, Secure: {useSecure})");
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                // Fallback for dev
                baseUrl = "ws://localhost:8080";
                Debug.LogWarning("[GameWebSocketService] Using fallback WebSocket URL: ws://localhost:8080");
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
                                    matchReady.dsIp, (ushort)matchReady.dsPort, matchReady.matchId);
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
        }

        [Serializable]
        public class MatchReadyEvent
        {
            public string lobbyToken;
            public string gameMode;
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

