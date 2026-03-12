using System.Collections;
using NightHunt.Core;
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
        [Header("Timing")] [SerializeField] private float _displayDuration = 10f; // Overridden by config at runtime
        [SerializeField] private float _postMatchCountdown = 10f;

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
            RoomState.Instance?.ClearRoom();

            if (mode == NightHunt.Networking.GameMode.Custom_Relay)
                SceneLoader.LoadCustomLobby();
            else
                SceneLoader.LoadHome();
        }

        #endregion
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