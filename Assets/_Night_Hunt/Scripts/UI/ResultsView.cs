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
    ///   Custom_Relay → countdown → LoadCustomLobby()
    ///   Ranked_DS    → countdown → LoadHome()
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultsView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Layout")] [SerializeField] private GameObject _panel;

        [Header("Result")] [SerializeField] private TextMeshProUGUI _resultHeaderText; // "VICTORY" / "DEFEAT" / "DRAW"
        [SerializeField] private TextMeshProUGUI _reasonText; // e.g. "Team eliminated"
        [SerializeField] private TextMeshProUGUI _countdownText; // "Returning in 10s…"

        [Header("Scoreboard")] [SerializeField]
        private Transform _scoreboardContainer;

        [SerializeField] private GameObject _resultRowPrefab;

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

        // ──────────────────────────────────────────────────────────────────────

        #region Lifecycle

        private void Awake()
        {
            if (_panel != null)
                _panel.SetActive(false);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(OnContinueClicked);

            GameplayEventBus.Instance?.Subscribe<MatchEndedEvent>(OnMatchEnded);
        }

        private void OnDestroy()
        {
            GameplayEventBus.Instance?.Unsubscribe<MatchEndedEvent>(OnMatchEnded);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────

        #region MatchEnded handler

        private void OnMatchEnded(MatchEndedEvent evt)
        {
            _lastMatchResult = evt;
            ShowResults(evt);
            StartCoroutine(CountdownCoroutine());
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

            // Header
            if (_resultHeaderText != null)
            {
                if (evt.WinnerTeamId == -1)
                    _resultHeaderText.text = "DRAW";
                else if (evt.WinnerTeamId == localTeam)
                    _resultHeaderText.text = "VICTORY";
                else
                    _resultHeaderText.text = "DEFEAT";
            }

            // Reason
            if (_reasonText != null)
                _reasonText.text = evt.Reason switch
                {
                    MatchEndReason.TeamEliminated => "Enemy team eliminated",
                    MatchEndReason.TimerExpired => "Timer expired",
                    MatchEndReason.Draw => "Match drawn",
                    _ => ""
                };

            // Scoreboard rows
            BuildScoreboard(evt.PlayerResults);

            // ELO (only for Ranked)
            bool isRanked = RoomState.Instance?.CurrentGameMode == NightHunt.Networking.GameMode.Ranked_DS;
            if (_eloPanel != null) _eloPanel.SetActive(isRanked);

            if (isRanked && _eloChangeText != null)
            {
                // Find local player ELO change
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

        private void BuildScoreboard(MatchResult[] results)
        {
            if (_scoreboardContainer == null || _resultRowPrefab == null || results == null)
                return;

            foreach (Transform t in _scoreboardContainer)
                Destroy(t.gameObject);

            foreach (var r in results)
            {
                var go = Instantiate(_resultRowPrefab, _scoreboardContainer);
                var row = ComponentResolver.Find<ResultRowView>(go)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] ResultRowView not found")
                    .Resolve();
                row?.SetData(r);
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

            string matchId = roomState.CurrentMatchId;
            if (string.IsNullOrEmpty(matchId))
            {
                // Custom_Relay uses room matchId
                matchId = roomState.CurrentRoom?.matchId ?? string.Empty;
            }

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
                    playerEntries.Add(new MatchResultPlayerEntry
                    {
                        backendPlayerId = r.BackendPlayerId,
                        teamId          = r.TeamId,
                        kills           = r.Kills,
                        deaths          = r.Deaths,
                        score           = r.Score,
                    });
                }
            }

            var request = new MatchResultRequest
            {
                matchId      = matchId,
                winnerTeamId = evt.Value.WinnerTeamId,
                endReason    = evt.Value.Reason.ToString(),
                players      = playerEntries,
            };

            string endpoint = string.Format(Constants.API_MATCH_RESULT, matchId);
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