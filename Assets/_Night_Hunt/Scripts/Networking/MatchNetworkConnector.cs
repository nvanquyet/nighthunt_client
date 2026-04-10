using FishNet.Managing;
using FishNet.Transporting;
using NightHunt.Core;
using NightHunt.State;
using NightHunt.UI;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// MatchNetworkConnector — placed in the 06_MatchLoading scene.
    ///
    /// PURPOSE:
    ///   Bridges the gap between RoomState (relay / DS info stored after lobby)
    ///   and NetworkGameManager (FishNet connection starter).
    ///
    ///   Called on Start() immediately after the scene loads:
    ///     • Custom_Relay + IsHostPlayer  → StartHostWithRelay()
    ///     • Custom_Relay + !IsHostPlayer → StartClientWithRelay()
    ///     • Ranked_DS                    → StartClientDS()
    ///
    /// PLACEMENT:
    ///   Attach to a persistent GameObject in 06_MatchLoading.
    ///   Assign _loadingView (optional) so the view also advances stages.
    ///
    /// ERROR HANDLING:
    ///   If RoomState has no valid connection data (e.g., dev test via
    ///   NetworkStartMenu), the connector skips silently — leaving manual
    ///   connection to the dev tool.
    /// </summary>
    [DefaultExecutionOrder(10)] // After NetworkGameManager and RoomState awake
    public class MatchNetworkConnector : MonoBehaviour
    {
        [Header("References (optional — auto-found if null)")]
        [SerializeField] private NetworkGameManager _networkGameManager;
        [SerializeField] private LoadingView        _loadingView;

        [Header("Retry")]
        [Tooltip("Seconds to wait before re-attempting connection on failure.")]
        [SerializeField] private float _retryDelay = 3f;
        [SerializeField] private int   _maxRetries = 2;

        private NetworkManager _fishNetManager;
        private int  _retryCount;
        private bool _connectionStarted;
        private bool _connected;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_networkGameManager == null)
                _networkGameManager = NetworkGameManager.Instance
                    ?? FindFirstObjectByType<NetworkGameManager>();

            if (_loadingView == null)
                _loadingView = FindFirstObjectByType<LoadingView>();
        }

        private void Start()
        {
            _fishNetManager = _networkGameManager != null
                ? _networkGameManager.NetworkManager
                : null;

            SubscribeFishNetEvents();
            ConnectBasedOnRoomState();
        }

        private void OnDestroy()
        {
            UnsubscribeFishNetEvents();
        }

        // ── FishNet event wiring ──────────────────────────────────────────────

        private void SubscribeFishNetEvents()
        {
            if (_fishNetManager == null) return;
            _fishNetManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void UnsubscribeFishNetEvents()
        {
            if (_fishNetManager == null) return;
            _fishNetManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _connected = true;
                    Debug.Log("[MatchNetworkConnector] ✅ Client connected.");
                    _loadingView?.MarkConnected();
                    MatchLoadingOverlay.Instance?.MarkConnected();
                    break;

                case LocalConnectionState.Stopped:
                    if (_connectionStarted && !_connected)
                    {
                        // Connection attempt failed
                        Debug.LogWarning("[MatchNetworkConnector] Connection failed.");
                        TryRetry();
                    }
                    break;
            }
        }

        // ── Connection dispatch ───────────────────────────────────────────────

        private void ConnectBasedOnRoomState()
        {
            var room = RoomState.Instance;

            if (room == null)
            {
                Debug.LogWarning("[MatchNetworkConnector] RoomState not found — skipping auto-connect (dev mode).");
                return;
            }

            switch (room.CurrentGameMode)
            {
                case GameMode.Custom_Relay:
                    ConnectRelay(room);
                    break;

                case GameMode.Ranked_DS:
                    ConnectDS(room);
                    break;

                default:
                    // GameMode.None → dev / editor test, NetworkStartMenu handles it
                    Debug.Log("[MatchNetworkConnector] GameMode.None — skipping auto-connect (use NetworkStartMenu for dev testing).");
                    break;
            }
        }

        /// <summary>Custom Relay mode: host starts Server+Client; others start Client only.</summary>
        private void ConnectRelay(RoomState room)
        {
            if (string.IsNullOrEmpty(room.RelayIp) || room.RelayPort == 0)
            {
                Debug.LogError("[MatchNetworkConnector] Custom_Relay mode but RelayIp/Port not set in RoomState! " +
                               "Call RoomState.SetRelaySession() from the GAME_STARTING WS handler before loading this scene.");
                return;
            }

            if (_networkGameManager == null)
            {
                Debug.LogError("[MatchNetworkConnector] NetworkGameManager not found!");
                return;
            }

            _connectionStarted = true;
            _connected         = false;

            if (room.IsHostPlayer)
            {
                Debug.Log($"[MatchNetworkConnector] Starting HOST via relay {room.RelayIp}:{room.RelayPort} session={room.RelaySessionId}");
                _networkGameManager.StartHostWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);

                // Inform ServerGameManager how many players to wait for before starting the match.
                // RoomState.CurrentRoom.players is populated by the backend before game_starting.
                int expected = room.PlayerCount > 0 ? room.PlayerCount : 2;
                if (ServerGameManager.Instance != null)
                {
                    ServerGameManager.Instance.SetExpectedPlayerCount(expected);
                    Debug.Log($"[MatchNetworkConnector] Host: expectedPlayerCount set to {expected}");
                }
                else
                {
                    Debug.LogWarning("[MatchNetworkConnector] ServerGameManager.Instance null — expectedPlayerCount not applied. Assign ServerGameManager to scene.");
                }
            }
            else
            {
                Debug.Log($"[MatchNetworkConnector] Connecting CLIENT via relay {room.RelayIp}:{room.RelayPort}");
                _networkGameManager.StartClientWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);
            }
        }

        /// <summary>Ranked_DS mode: all players connect as pure clients to the dedicated server.</summary>
        private void ConnectDS(RoomState room)
        {
            if (string.IsNullOrEmpty(room.DsIp) || room.DsPort == 0)
            {
                Debug.LogError("[MatchNetworkConnector] Ranked_DS mode but DsIp/DsPort not set in RoomState! " +
                               "Call RoomState.SetDedicatedServer() from the MATCH_READY WS handler before loading this scene.");
                return;
            }

            if (_networkGameManager == null)
            {
                Debug.LogError("[MatchNetworkConnector] NetworkGameManager not found!");
                return;
            }

            _connectionStarted = true;
            _connected         = false;

            Debug.Log($"[MatchNetworkConnector] Connecting to DS {room.DsIp}:{room.DsPort}");
            _networkGameManager.StartClientDS(room.DsIp, room.DsPort);
        }

        // ── Retry logic ───────────────────────────────────────────────────────

        private void TryRetry()
        {
            if (_retryCount >= _maxRetries)
            {
                Debug.LogError("[MatchNetworkConnector] Max retries reached — giving up. Returning to home.");
                Invoke(nameof(LoadHome), 1.5f);
                return;
            }

            _retryCount++;
            Debug.Log($"[MatchNetworkConnector] Retrying connection in {_retryDelay}s (attempt {_retryCount}/{_maxRetries})…");
            Invoke(nameof(RetryConnect), _retryDelay);
        }

        private void RetryConnect()
        {
            _connected = false;
            ConnectBasedOnRoomState();
        }

        private void LoadHome()
        {
            RoomState.Instance?.ClearRoom();
            SceneLoader.LoadHome();
        }
    }
}
