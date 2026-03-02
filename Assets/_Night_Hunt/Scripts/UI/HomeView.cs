using System;
using System.Threading.Tasks;
using NightHunt.Core;
using NightHunt.Networking;
using NightHunt.Common;
using NightHunt.Data.DTOs;
using NightHunt.Services.Auth;
using NightHunt.Services.Game;
using NightHunt.Services.Room;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Home View — PUBG-style main menu.
    ///
    /// Layout:
    ///   • User info panel (top)
    ///   • [CUSTOM GAME] button → loads CustomLobby scene
    ///   • [RANKED] section:
    ///       Idle      → "Find Ranked Match" button
    ///       Searching → "Searching…" label + elapsed time + "Cancel" button
    ///       MatchFound→ "Match Found!" + accept countdown + "Accept" / "Decline" buttons
    /// </summary>
    public class HomeView : MonoBehaviour
    {
        // ── User Info ─────────────────────────────────────────────────────────
        [Header("User Info")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI emailText;
        [SerializeField] private TextMeshProUGUI userIdText;
        [SerializeField] private TextMeshProUGUI rankTierText;

        // ── Navigation Buttons ────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button customGameButton;
        [SerializeField] private Button logoutButton;

        // ── Ranked Queue Panel ────────────────────────────────────────────────
        [Header("Ranked Queue")]
        [SerializeField] private GameObject rankedIdlePanel;
        [SerializeField] private Button     findMatchButton;

        [SerializeField] private GameObject rankedSearchingPanel;
        [SerializeField] private TextMeshProUGUI searchTimeText;
        [SerializeField] private Button     cancelSearchButton;

        [SerializeField] private GameObject rankedMatchFoundPanel;
        [SerializeField] private TextMeshProUGUI matchFoundCountdownText;
        [SerializeField] private Button     acceptMatchButton;
        [SerializeField] private Button     declineMatchButton;

        // ── Services ──────────────────────────────────────────────────────────
        private AuthService   _authService;
        private RoomService   _roomService;
        private SessionState  _sessionState;
        private RoomState     _roomState;

        // ── Ranked Queue State ────────────────────────────────────────────────
        private RankedQueueState _queueState = RankedQueueState.Idle;
        private float _searchElapsed;
        private float _acceptCountdown;
        private const float AcceptWindow = 15f;
        private string _pendingLobbyToken;
        private string _pendingGameMode = Constants.MODE_2V2;

        // ──────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _authService  = GameManager.Instance.AuthService;
                _roomService  = GameManager.Instance.RoomService;
                _sessionState = GameManager.Instance.SessionState;
            }
            _roomState = RoomState.Instance;

            // Buttons
            if (customGameButton  != null) customGameButton.onClick.AddListener(OnCustomGameClicked);
            if (logoutButton      != null) logoutButton.onClick.AddListener(OnLogoutClicked);
            if (findMatchButton   != null) findMatchButton.onClick.AddListener(OnFindMatchClicked);
            if (cancelSearchButton!= null) cancelSearchButton.onClick.AddListener(OnCancelSearchClicked);
            if (acceptMatchButton != null) acceptMatchButton.onClick.AddListener(OnAcceptMatchClicked);
            if (declineMatchButton!= null) declineMatchButton.onClick.AddListener(OnDeclineMatchClicked);
        }

        private async void Start()
        {
            UpdateUserInfo();
            SetQueueState(RankedQueueState.Idle);

            // Subscribe to matchmaking WS events
            var ws = GameWebSocketService.Instance;
            if (ws != null)
            {
                ws.OnMatchFound     += HandleMatchFound;
                ws.OnMatchReady     += HandleMatchReady;
                ws.OnMatchCancelled += HandleMatchCancelled;
            }

            // Hide global loading overlay if present
            var loading = PersistentUICanvas.Instance != null
                ? PersistentUICanvas.Instance.LoadingManager : null;
            if (loading != null && loading.IsShowing())
                loading.Hide();

            await CheckAndShowReconnectPopup();
        }

        private void OnDestroy()
        {
            var ws = GameWebSocketService.Instance;
            if (ws != null)
            {
                ws.OnMatchFound     -= HandleMatchFound;
                ws.OnMatchReady     -= HandleMatchReady;
                ws.OnMatchCancelled -= HandleMatchCancelled;
            }
        }

        private void Update()
        {
            switch (_queueState)
            {
                case RankedQueueState.Searching:
                    _searchElapsed += Time.deltaTime;
                    if (searchTimeText != null)
                        searchTimeText.text = FormatTime(_searchElapsed);
                    break;

                case RankedQueueState.MatchFound:
                    _acceptCountdown -= Time.deltaTime;
                    if (matchFoundCountdownText != null)
                        matchFoundCountdownText.text = $"{Mathf.CeilToInt(_acceptCountdown)}s";
                    if (_acceptCountdown <= 0f)
                        OnDeclineMatchClicked();   // auto-decline on timeout
                    break;
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Queue state machine

        private void SetQueueState(RankedQueueState newState)
        {
            _queueState = newState;

            if (rankedIdlePanel       != null) rankedIdlePanel.SetActive(newState == RankedQueueState.Idle);
            if (rankedSearchingPanel  != null) rankedSearchingPanel.SetActive(newState == RankedQueueState.Searching);
            if (rankedMatchFoundPanel != null) rankedMatchFoundPanel.SetActive(newState == RankedQueueState.MatchFound);
        }

        private async void OnFindMatchClicked()
        {
            SetQueueState(RankedQueueState.Searching);
            _searchElapsed = 0f;

            var result = await GameManager.Instance.BackendClient.PostAsync<object>(
                Constants.API_MATCHMAKING_QUEUE,
                new MatchmakingQueueRequest { gameMode = _pendingGameMode });

            if (!result.Success)
            {
                Debug.LogError($"[HomeView] Queue join failed: {result.Message}");
                SetQueueState(RankedQueueState.Idle);
            }
            else
            {
                Debug.Log($"[HomeView] Ranked matchmaking queue started (mode={_pendingGameMode}).");
            }
        }

        private async void OnCancelSearchClicked()
        {
            await GameManager.Instance.BackendClient.DeleteAsync<object>(Constants.API_MATCHMAKING_QUEUE);
            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
        }

        /// <summary>
        /// Called externally (by WS event handler) when the backend sends MATCH_FOUND.
        /// </summary>
        public void ShowMatchFound()
        {
            _acceptCountdown = AcceptWindow;
            SetQueueState(RankedQueueState.MatchFound);
        }

        private async void OnAcceptMatchClicked()
        {
            if (string.IsNullOrEmpty(_pendingLobbyToken)) return;

            var result = await GameManager.Instance.BackendClient.PostAsync<object>(
                Constants.API_MATCHMAKING_ACCEPT,
                new MatchmakingAcceptRequest { lobbyToken = _pendingLobbyToken });

            if (!result.Success)
            {
                Debug.LogError($"[HomeView] Accept failed: {result.Message}");
                SetQueueState(RankedQueueState.Idle);
            }
            // Stay in MatchFound state — navigate only on match_ready WS event
        }

        private async void OnDeclineMatchClicked()
        {
            if (!string.IsNullOrEmpty(_pendingLobbyToken))
            {
                await GameManager.Instance.BackendClient.PostAsync<object>(
                    Constants.API_MATCHMAKING_DECLINE,
                    new MatchmakingDeclineRequest { lobbyToken = _pendingLobbyToken });
            }
            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Matchmaking WS handlers

        private void HandleMatchFound(GameWebSocketService.MatchFoundEvent evt)
        {
            _pendingLobbyToken = evt.lobbyToken;
            _pendingGameMode   = !string.IsNullOrEmpty(evt.gameMode) ? evt.gameMode : _pendingGameMode;
            ShowMatchFound();
        }

        private void HandleMatchReady(GameWebSocketService.MatchReadyEvent evt)
        {
            _pendingLobbyToken = null;
            // RoomState.SetDedicatedServer already called inside GameWebSocketService
            Debug.Log($"[HomeView] Match ready! room={evt.roomCode}, ds={evt.dsIp}:{evt.dsPort}");
            SetQueueState(RankedQueueState.Idle);
            SceneLoader.LoadMatchLoading();
        }

        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent evt)
        {
            _pendingLobbyToken = null;
            SetQueueState(RankedQueueState.Idle);
            Debug.Log($"[HomeView] Match cancelled: {evt.reason}");
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Navigation

        private void OnCustomGameClicked()
        {
            SceneLoader.LoadCustomLobby();
        }

        private void OnLogoutClicked()
        {
            _authService?.Logout();
            SceneLoader.LoadLogin();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region User info

        private void UpdateUserInfo()
        {
            if (_sessionState == null) return;
            if (usernameText != null) usernameText.text = $"Username: {_sessionState.Username}";
            if (emailText    != null) emailText.text    = $"Email: {_sessionState.Email}";
            if (userIdText   != null) userIdText.text   = $"ID: {_sessionState.UserId}";

            // Rank tier — will be populated after BE-29 adds ELO to SessionState
            if (rankTierText != null) rankTierText.text = "Rank: ---";
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Reconnect check

        private async Task CheckAndShowReconnectPopup()
        {
            if (_roomState == null || !_roomState.IsInRoom || _roomService == null)
                return;

            var reconnectPopup = PersistentUICanvas.Instance != null
                ? PersistentUICanvas.Instance.ReconnectPopup : null;
            if (reconnectPopup == null) return;

            var result = await _roomService.Reconnect(_roomState.RoomId);
            if (result.Success && result.Data != null)
            {
                reconnectPopup.Show(
                    message: $"You are still in room {result.Data.roomCode}. Reconnect?",
                    onReconnectCallback: SceneLoader.LoadCustomLobby,
                    onLeaveCallback: _roomState.ClearRoom
                );
            }
            else
            {
                _roomState.ClearRoom();
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Helpers

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            int s = (int)(seconds % 60f);
            return m > 0 ? $"{m}:{s:D2}" : $"{s}s";
        }

        #endregion
    }

    public enum RankedQueueState { Idle, Searching, MatchFound }
}
