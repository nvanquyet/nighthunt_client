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
using UnityEngine;

namespace NightHunt.Services.Game
{
    /// <summary>
    /// GameWebSocketService - Unified WebSocket service for all game events
    /// Connected after login/auto-login, kept alive throughout the session
    /// Handles: session events (force_logout, session_expired) and room events (room_updated, player_joined, etc.)
    /// </summary>
    public class GameWebSocketService : MonoBehaviour
    {
        private static GameWebSocketService _instance;
        public static GameWebSocketService Instance => _instance;

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

        // Matchmaking Events
        public event Action<MatchFoundEvent>     OnMatchFound;
        public event Action<MatchReadyEvent>     OnMatchReady;
        public event Action<MatchCancelledEvent> OnMatchCancelled;
        
        // Connection Events
        public event Action OnDisconnected;
        public event Action<string> OnError;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

        }

        private void Update()
        {
            // Dispatch WebSocket message queue
            #if !UNITY_WEBGL || UNITY_EDITOR
            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                webSocket.DispatchMessageQueue();
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
                Debug.LogWarning("[GameWebSocketService] Already connecting or connected");
                return isConnected;
            }

            if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
            {
                Debug.LogWarning("[GameWebSocketService] Cannot connect - user not authenticated");
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
                Debug.Log($"[GameWebSocketService] Connecting to Game WebSocket...");
                Debug.Log($"[GameWebSocketService] WebSocket URL: {wsUrl}");

                await ConnectWebSocket(wsUrl, thisToken);
                
                if (isConnected && thisToken == connectionToken)
                {
                    Debug.Log("[GameWebSocketService] Game WebSocket connected successfully");
                    reconnectAttempts = 0; // reset on success
                    return true;
                }
                else
                {
                    Debug.LogWarning("[GameWebSocketService] Failed to connect Game WebSocket");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameWebSocketService] Error connecting: {ex.Message}");
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
                        Debug.LogWarning($"[GameWebSocketService] Error closing existing WebSocket: {ex.Message}");
                    }
                }

                // Create WebSocket instance
                webSocket = new WebSocket(url);

                // Set up event handlers
                webSocket.OnOpen += () =>
                {
                    if (token != connectionToken) return;
                    Debug.Log("[GameWebSocketService] WebSocket opened");
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
                    Debug.LogError($"[GameWebSocketService] WebSocket error: {error}");
                    isConnected = false;
                    isConnecting = false;
                    OnError?.Invoke(error);
                };

                webSocket.OnClose += (code) =>
                {
                    if (token != connectionToken) return;
                    Debug.Log($"[GameWebSocketService] WebSocket closed: {code}");
                    isConnected = false;
                    isConnecting = false;
                    OnDisconnected?.Invoke();
                    
                    // Try to reconnect if session is still valid
                    if (allowAutoReconnect && SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
                    {
                        reconnectAttempts++;
                        if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS)
                        {
                            Debug.LogError($"[GameWebSocketService] Reconnect attempts exceeded limit ({MAX_RECONNECT_ATTEMPTS}). Stopping auto-reconnect.");
                            return;
                        }
                        Debug.Log($"[GameWebSocketService] Attempting to reconnect... (attempt {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})");
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
                    Debug.LogError("[GameWebSocketService] WebSocket connection timeout");
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
                Debug.LogWarning("[GameWebSocketService] WebSocket connection timeout");
                isConnected = false;
                isConnecting = false;
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameWebSocketService] Error in ConnectWebSocket: {ex.Message}");
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
                    bool useHttps = cfg.useHttps;
                    string host = cfg.apiHost.TrimEnd('/');

                    bool isLocal = host.Contains("localhost") || host.Contains("127.0.0.1") || host.Contains("0.0.0.0");
                    bool secure = (cfg.respectBackendHttps && useHttps) || (cfg.forceSecure && !(isLocal && cfg.allowInsecureFallback));

                    string scheme = secure ? "wss://" : "ws://";
                    baseUrl = scheme + host;
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                // Fallback for dev
                baseUrl = "ws://localhost:8080";
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

            return "/ws/game";
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
                            // When a custom room game starts, the backend includes relay
                            // serverIp/serverPort in the room payload (IN_GAME status).
                            // Store it in RoomState now so MatchNetworkConnector can use it.
                            // NOTE: Do NOT gate on CurrentGameMode here — it starts as None and is
                            //       only set to Custom_Relay by SetRelaySession() below.
                            //       Presence of serverIp is the correct signal.
                            if (roomStatusChanged.newStatus == "IN_GAME"
                                && roomStatusChanged.room != null
                                && !string.IsNullOrEmpty(roomStatusChanged.room.serverIp)
                                && roomStatusChanged.room.serverPort > 0
                                && RoomState.Instance != null)
                            {
                                long localUserId = NightHunt.State.SessionState.Instance != null
                                    ? NightHunt.State.SessionState.Instance.UserId
                                    : 0L;
                                bool isHost = roomStatusChanged.room.ownerId == localUserId;
                                string sessionId = roomStatusChanged.room.matchId ?? "";
                                RoomState.Instance.SetRelaySession(
                                    sessionId,
                                    roomStatusChanged.room.serverIp,
                                    (ushort)roomStatusChanged.room.serverPort,
                                    isHost);
                                Debug.Log($"[GameWebSocketService] Relay session stored: host={isHost} " +
                                          $"{roomStatusChanged.room.serverIp}:{roomStatusChanged.room.serverPort}");
                            }
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

                    default:
                        Debug.LogWarning($"[GameWebSocketService] Unknown message type: {messageData.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameWebSocketService] Error handling message: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void HandleForceLogout(ForceLogoutEvent evt)
        {
            Debug.LogWarning($"[GameWebSocketService] Force logout received: {evt?.reason ?? "Unknown reason"}");
            
            // Clear session
            if (SessionState.Instance != null)
            {
                SessionState.Instance.ClearSession();
            }
            
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
    }
}

