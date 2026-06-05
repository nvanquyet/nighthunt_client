using System.Collections;
using System.Collections.Generic;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Networking;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Results View — overlaid on (or replacing) the game UI after a match ends.
    ///
    /// Listens for <see cref="MatchEndedEvent"/> via <see cref="GameplayEventBus"/>.
    ///
    /// Post-match routing:
    ///   Custom_Relay → countdown → LoadPartyCustomMode()
    ///   Ranked_DS    → countdown → LoadHome()
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultsView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Layout")] [SerializeField] private GameObject _panel;

        [Header("Result Image")]
        [Tooltip("Image showing Win/Lose/Draw result sprite.")]
        [SerializeField] private Image _resultImage;
        [SerializeField] private Sprite _winSprite;
        [SerializeField] private Sprite _loseSprite;
        [SerializeField] private Sprite _drawSprite;

        [Header("Score & Time")]
        [Tooltip("Score label e.g. '230 : 195'. Uses rich-text color tags per team.")]
        [SerializeField] private TextMeshProUGUI _scoreLabel;
        [Tooltip("Total match duration label e.g. 'Time: 09:12'.")]
        [SerializeField] private TextMeshProUGUI _matchTimeText;
        [SerializeField] private Color _localTeamColor = Color.cyan;
        [SerializeField] private Color _enemyTeamColor = Color.red;

        [Header("Scoreboard")]
        [Tooltip("Container for the local player's team rows.")]
        [SerializeField] private Transform _team1Container;
        [Tooltip("Container for the enemy team rows.")]
        [SerializeField] private Transform _team2Container;
        [SerializeField] private GameObject _resultRowPrefab;
        [Tooltip("Tint applied to the local player's own result row.")]
        [SerializeField] private Color _ownerRowColor = Color.yellow;

        [Header("Countdown")]
        [SerializeField] private TextMeshProUGUI _countdownText; // "Returning in 10s…"

        [Header("ELO Change (Ranked)")] [SerializeField]
        private GameObject _eloPanel;

        [SerializeField] private TextMeshProUGUI _eloChangeText;

        [Header("Navigation")] [SerializeField]
        private Button _continueButton;

        // ── Config ────────────────────────────────────────────────────────────
        [Header("Timing")]
#pragma warning disable CS0414
        [SerializeField] private float _displayDuration = 10f; // Overridden by config at runtime
#pragma warning restore CS0414
        [SerializeField] private float _postMatchCountdown = 10f;

        // Last match end result (cached for post-match backend call)
        private MatchEndedEvent? _lastMatchResult;
        private float            _matchStartTime;

        // ──────────────────────────────────────────────────────────────────────

        #region Lifecycle

        private void Awake()
        {
            _matchStartTime = Time.time;

            if (_panel != null)
                _panel.SetActive(false);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);

            GameplayEventBus.Instance?.Subscribe<MatchEndedEvent>(OnMatchEnded);
            GameplayEventBus.Instance?.Subscribe<MatchEndedWsResultsEvent>(OnMatchEndedWsResults);
        }

        private void OnDestroy()
        {
            GameplayEventBus.Instance?.Unsubscribe<MatchEndedEvent>(OnMatchEnded);
            GameplayEventBus.Instance?.Unsubscribe<MatchEndedWsResultsEvent>(OnMatchEndedWsResults);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region MatchEnded handler

        private void OnMatchEnded(MatchEndedEvent evt)
        {
            _lastMatchResult = evt;
            ShowResults(evt);
            ShowMatchResultToast(evt);
            StartCoroutine(CountdownCoroutine());
        }

        private void OnMatchEndedWsResults(MatchEndedWsResultsEvent evt)
        {
            if (!_lastMatchResult.HasValue || evt.PlayerResults == null || evt.PlayerResults.Length == 0)
                return;

            var updated = _lastMatchResult.Value;
            updated.PlayerResults = evt.PlayerResults;
            _lastMatchResult = updated;
            ShowResults(updated);
        }

        private void ShowMatchResultToast(MatchEndedEvent evt)
        {
            var session  = SessionState.Instance;
            var registry = RegistryService.Instance;
            int localTeam = -1;
            if (session != null && registry != null)
            {
                var np = registry.GetActivePlayerByBackendId(session.UserId.ToString());
                if (np != null) localTeam = np.TeamId;
            }

            string title, message;
            if (evt.WinnerTeamId == -1)
            {
                title   = "DRAW";
                message = "The match ended in a draw.";
            }
            else if (evt.WinnerTeamId == localTeam)
            {
                title   = "VICTORY";
                message = "Your team won the match!";
            }
            else
            {
                title   = "DEFEAT";
                message = "Your team was defeated.";
            }

            Debug.Log($"[ResultsView] Match ended: {title} — winnerTeamId={evt.WinnerTeamId} localTeam={localTeam}");
            var toast = PersistentUICanvas.Instance?.ToastService ?? ToastService.Instance;
            toast?.Show(title, message);
        }

        private void ShowResults(MatchEndedEvent evt)
        {
            if (_panel != null)
                _panel.SetActive(true);

            // Determine local team
            var session = SessionState.Instance;
            var registry = RegistryService.Instance;
            int localTeam = -1;

            if (session != null && registry != null)
            {
                var np = registry.GetActivePlayerByBackendId(session.UserId.ToString());
                if (np != null) localTeam = np.TeamId;
            }

            // Result image
            if (_resultImage != null)
            {
                Sprite s = evt.WinnerTeamId == -1 ? _drawSprite
                         : evt.WinnerTeamId == localTeam ? _winSprite
                         : _loseSprite;
                if (s != null) { _resultImage.sprite = s; _resultImage.enabled = true; }
            }

            // Score label with per-team colors
            if (evt.PlayerResults != null && _scoreLabel != null)
            {
                int team0Score = 0, team1Score = 0;
                foreach (var r in evt.PlayerResults)
                {
                    if (r.TeamId == 0) team0Score += r.Score;
                    else               team1Score += r.Score;
                }

                bool   localIsTeam0 = localTeam == 0;
                string localHex     = ColorUtility.ToHtmlStringRGB(_localTeamColor);
                string enemyHex     = ColorUtility.ToHtmlStringRGB(_enemyTeamColor);
                string s0 = $"<color=#{(localIsTeam0 ? localHex : enemyHex)}>{team0Score}</color>";
                string s1 = $"<color=#{(localIsTeam0 ? enemyHex : localHex)}>{team1Score}</color>";
                _scoreLabel.text = $"{s0} : {s1}";
            }

            // Match duration
            if (_matchTimeText != null)
            {
                float elapsed = Time.time - _matchStartTime;
                int   minutes = Mathf.FloorToInt(elapsed / 60f);
                int   seconds = Mathf.FloorToInt(elapsed % 60f);
                _matchTimeText.text = $"Time: {minutes:00}:{seconds:00}";
            }

            // Scoreboard rows
            BuildScoreboard(evt.PlayerResults, localTeam);

            // ELO (only for Ranked)
            bool isRanked = RoomState.Instance?.CurrentGameMode == NightHunt.Networking.GameMode.Ranked_DS;
            if (_eloPanel != null) _eloPanel.SetActive(isRanked);

            if (isRanked && _eloChangeText != null)
            {
                int eloChange = 0;
                if (session != null && evt.PlayerResults != null)
                    foreach (var r in evt.PlayerResults)
                        if (r.BackendPlayerId == session.UserId.ToString())
                        {
                            eloChange = r.EloChange;
                            break;
                        }

                _eloChangeText.text = eloChange >= 0
                    ? $"ELO <color=#00FF88>+{eloChange}</color>"
                    : $"ELO <color=#FF4444>{eloChange}</color>";
            }
        }

        private void BuildScoreboard(MatchResult[] results, int localTeamId)
        {
            if (_resultRowPrefab == null || results == null) return;

            string localBackendId = SessionState.Instance?.UserId.ToString() ?? string.Empty;

            // Clear both containers.
            if (_team1Container != null) foreach (Transform t in _team1Container) Destroy(t.gameObject);
            if (_team2Container != null) foreach (Transform t in _team2Container) Destroy(t.gameObject);

            foreach (var r in results)
            {
                // Route to local-team container or enemy container.
                Transform container = r.TeamId == localTeamId ? _team1Container : _team2Container;
                if (container == null) continue;

                var go  = Instantiate(_resultRowPrefab, container);
                var row = ComponentResolver.Find<ResultRowView>(go)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] ResultRowView not found")
                    .Resolve();
                row?.SetData(r);

                // Highlight the owner row.
                if (!string.IsNullOrEmpty(localBackendId) && r.BackendPlayerId == localBackendId)
                {
                    var img = go.GetComponentInChildren<Image>(true);
                    if (img != null) img.color = _ownerRowColor;
                }
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region Countdown

        private IEnumerator CountdownCoroutine()
        {
            float remaining = _postMatchCountdown;
            while (remaining > 0f)
            {
                if (_countdownText != null)
                    _countdownText.text = $"Returning in {Mathf.CeilToInt(remaining)}s…";
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }

            NavigatePostMatch();
        }

        private void OnContinueClicked()
        {
            StopAllCoroutines();
            NavigatePostMatch();
        }

        private void NavigatePostMatch()
        {
            var mode = RoomState.Instance?.CurrentGameMode ?? NightHunt.Networking.GameMode.None;

            // Report match result to backend before clearing session.
            // Fire-and-forget: don't block navigation on network latency.
            _ = PostMatchResultAsync();

            var network = NetworkGameManager.Instance;
            if (network != null)
            {
                if (mode == NightHunt.Networking.GameMode.Custom_Relay)
                    _ = network.DisconnectWithCleanup();
                else
                    network.Disconnect();
            }

            RoomState.Instance?.ClearRoom();

            // Đang ở gameplay scene → luôn dùng SceneLoader.LoadHome() để về 01_Home.
            // UINavigator trong 01_Home sẽ tự điều hướng đến đúng panel.
            // (Custom_Relay cũng về Home trước, Home panel có reconnect check)
            Debug.Log($"[ResultsView] Match ended (mode={mode}) → LoadHome");
            SceneLoader.LoadHome();
        }

        private async System.Threading.Tasks.Task PostMatchResultAsync()
        {
            var backend = GameManager.Instance?.BackendClient;
            var roomState = RoomState.Instance;
            if (backend == null || roomState == null) return;

            // Only the Custom_Relay HOST reports to backend.
            // For Ranked_DS the Dedicated Server calls /api/match/end/ranked directly.
            var mode = roomState.CurrentGameMode;
            if (mode != NightHunt.Networking.GameMode.Custom_Relay)
            {
                Debug.Log($"[ResultsView] Mode={mode} — skipping client result report (DS handles ranked).");
                return;
            }

            // Custom_Relay host check: only the FishNet host should call this endpoint.
            if (!roomState.IsHostPlayer)
            {
                Debug.Log("[ResultsView] Not relay host — skipping result report.");
                return;
            }

            string matchId = roomState.CurrentMatchId;
            if (string.IsNullOrEmpty(matchId))
                matchId = roomState.CurrentRoom?.matchId ?? string.Empty;

            if (string.IsNullOrEmpty(matchId))
            {
                Debug.LogWarning("[ResultsView] matchId not set — skipping backend result push.");
                return;
            }

            var evt = _lastMatchResult;
            if (!evt.HasValue) return;

            var playerEntries = new List<MatchResultPlayerEntry>();
            if (evt.Value.PlayerResults != null)
            {
                foreach (var r in evt.Value.PlayerResults)
                {
                    long uid = long.TryParse(r.BackendPlayerId, out long parsed) ? parsed : 0L;
                    playerEntries.Add(new MatchResultPlayerEntry
                    {
                        userId      = uid,
                        displayName = r.DisplayName ?? string.Empty,
                        teamId      = r.TeamId,
                        kills       = r.Kills,
                        deaths      = r.Deaths,
                        score       = r.Score,
                    });
                }
            }

            var request = new MatchResultRequest
            {
                matchId      = matchId,
                winnerTeamId = evt.Value.WinnerTeamId,
                endReason    = evt.Value.Reason.ToString(),
                playerResults = playerEntries,
            };

            // Custom relay host sends to /api/match/end/custom
            string endpoint = Constants.API_MATCH_END_CUSTOM;
            var result = await backend.PostAsync<object>(endpoint, request);

            if (!result.Success)
                Debug.LogWarning($"[ResultsView] Backend result push failed: {result.Message}");
            else
                Debug.Log($"[ResultsView] Match result reported to backend (matchId={matchId}).");
        }

        #endregion

#if UNITY_EDITOR
        // ── Editor — Context Menu: Create ResultRow Template Prefab ──────────

        [ContextMenu("NightHunt/Create ResultRow Template Prefab")]
        private void Editor_CreateResultRowPrefab()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            const string path = dir + "/ResultRow_Template.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[ResultsView] ResultRow_Template already exists at {path}");
                return;
            }

            var go  = new GameObject("ResultRow_Template");
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(500f, 40f);
            go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.8f);
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.spacing = 4f;
            hlg.padding = new RectOffset(6, 6, 2, 2);

            string[] colNames   = { "NameText", "TeamText", "KillsText", "DeathsText", "ScoreText", "EloText" };
            string[] colSamples = { "PlayerX",  "Team A",   "5",          "2",           "1200",      "+25" };
            float[]  colWidths  = { 160f, 70f, 50f, 50f, 70f, 60f };

            for (int i = 0; i < colNames.Length; i++)
            {
                var colGo  = new GameObject(colNames[i], typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                colGo.transform.SetParent(go.transform, false);
                colGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = colWidths[i];
                colGo.GetComponent<TMPro.TextMeshProUGUI>().text = colSamples[i];
            }

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (_resultRowPrefab == null)
            {
                _resultRowPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[ResultsView] Created ResultRow_Template at {path}. " +
                      "Add ResultRowView component and wire nameText/teamText/killsText/deathsText/scoreText/eloText.");
        }
#endif
    }

    /// <summary>
    /// A single row in the post-match scoreboard. Attach to the result row prefab.
    /// </summary>
    public sealed class ResultRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI teamText;
        [SerializeField] private TextMeshProUGUI killsText;
        [SerializeField] private TextMeshProUGUI deathsText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI eloText;

        public void SetData(MatchResult r)
        {
            if (nameText != null) nameText.text = r.DisplayName;
            if (teamText != null) teamText.text = $"Team {(char)('A' + r.TeamId)}";
            if (killsText != null) killsText.text = r.Kills.ToString();
            if (deathsText != null) deathsText.text = r.Deaths.ToString();
            if (scoreText != null) scoreText.text = r.Score.ToString();
            if (eloText != null)
            {
                if (r.EloChange == 0) eloText.text = "-";
                else eloText.text = r.EloChange > 0 ? $"+{r.EloChange}" : r.EloChange.ToString();
            }
        }
    }
}
