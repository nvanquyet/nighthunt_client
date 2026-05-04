using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Scoring;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.UI
{
    /// <summary>
    /// Match HUD: phase name, countdown timer, score, two-team member lists.
    ///
    /// Team list populate DUY NHẤT 1 LẦN khi nhận AllPlayersReadyEvent.
    /// Source: PlayerPublicRegistry (dict sẵn có, không FindObjectsByType).
    /// Alive/dead tự update qua NetworkPlayer.OnAliveChanged bên trong TeamMemberRow.
    /// </summary>
    public class MatchUI : MonoBehaviour
    {
        // ── Phase ─────────────────────────────────────────────────────────────
        [Header("Phase Display")]
        [SerializeField] private TextMeshProUGUI phaseText;

        // ── Timer ─────────────────────────────────────────────────────────────
        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Team Score Display")]
        [SerializeField] private TextMeshProUGUI _teamAScoreText;
        [SerializeField] private TextMeshProUGUI _teamBScoreText;
        [SerializeField] private GameObject      _teamAArrow;
        [SerializeField] private GameObject      _teamBArrow;

        // ── Pre-match Countdown ───────────────────────────────────────────────
        [Header("Pre-Match Countdown")]
        [Tooltip("Full-screen countdown overlay. Hide when SecondsRemaining==0 (GO!).")]
        [SerializeField] private GameObject      _countdownPanel;
        [Tooltip("Large numeric countdown text (5, 4, 3, 2, 1, GO!).")]
        [SerializeField] private TextMeshProUGUI _countdownText;

        // ── Phase Started Banner ──────────────────────────────────────────────
        [Header("Phase Started Banner")]
        [Tooltip("Banner shown briefly when a new phase starts. Leave null to disable phase-start banners.")]
        [SerializeField] private GameObject      _phaseStartPanel;
        [Tooltip("Phase title text inside the phase-start banner.")]
        [SerializeField] private TextMeshProUGUI _phaseStartTitleText;
        [Tooltip("Objectives list text inside the phase-start banner (newline-separated bullets).")]
        [SerializeField] private TextMeshProUGUI _phaseObjectivesText;
        [SerializeField] private float           _phaseStartHoldDuration = 3.5f;
        [SerializeField] private float           _phaseStartFadeDuration = 0.3f;

        // ── Phase Warning Banner ──────────────────────────────────────────────
        [Header("Phase Warning Banner")]
        [SerializeField] private GameObject      warningPanel;
        [SerializeField] private TextMeshProUGUI warningText;
        [SerializeField] private Image           warningBackground;
        [SerializeField] private float           warningHoldDuration = 3f;
        [SerializeField] private float           warningFadeDuration = 0.4f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private MatchPhaseManager _phaseManager;
        private float             _lastUpdateTime;
        private const float       UpdateInterval            = 0.1f;
        private float             _phaseManagerRetryTime;
        private const float       PhaseManagerRetryInterval = 1f;
        private Coroutine         _warningCoroutine;
        private Coroutine         _phaseStartCoroutine;
        private NetworkPlayer     _localPlayer;
        private int               _cachedTeamAScore;
        private int               _cachedTeamBScore;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (warningPanel != null) warningPanel.SetActive(false);
            if (_phaseStartPanel != null) _phaseStartPanel.SetActive(false);
            if (_countdownPanel != null) _countdownPanel.SetActive(false);
            // Arrows start hidden until Initialize() is called with the local player.
            if (_teamAArrow != null) _teamAArrow.SetActive(false);
            if (_teamBArrow != null) _teamBArrow.SetActive(false);
        }

        /// <summary>Call once when local NetworkPlayer is known (from GameHUD or spawner).</summary>
        public void Initialize(NetworkPlayer localPlayer)
        {
            _localPlayer = localPlayer;

            // Show the arrow on the correct team column.
            bool onTeamA = localPlayer != null && localPlayer.TeamId == 0;
            if (_teamAArrow != null) _teamAArrow.SetActive(onTeamA);
            if (_teamBArrow != null) _teamBArrow.SetActive(!onTeamA);
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<PhaseWarningEvent>(OnPhaseWarning);
            GameplayEventBus.Instance?.Subscribe<PhaseStartedEvent>(OnPhaseStarted);
            GameplayEventBus.Instance?.Subscribe<MatchCountdownEvent>(OnMatchCountdown);
            GameplayEventBus.Instance?.Subscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<PhaseWarningEvent>(OnPhaseWarning);
            GameplayEventBus.Instance?.Unsubscribe<PhaseStartedEvent>(OnPhaseStarted);
            GameplayEventBus.Instance?.Unsubscribe<MatchCountdownEvent>(OnMatchCountdown);
            GameplayEventBus.Instance?.Unsubscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);
        }

        private void Start()
        {
            _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        private void Update()
        {
            if (Time.time - _lastUpdateTime < UpdateInterval) return;
            _lastUpdateTime = Time.time;
            UpdateDisplay();
        }

        // ── Display ───────────────────────────────────────────────────────────

        private void UpdateDisplay()
        {
            if (_phaseManager == null)
            {
                if (Time.time >= _phaseManagerRetryTime)
                {
                    _phaseManager          = FindFirstObjectByType<MatchPhaseManager>();
                    _phaseManagerRetryTime = Time.time + PhaseManagerRetryInterval;
                }
                return;
            }

            UpdatePhase();
            UpdateCountdown();
            UpdateScore();
        }

        private void UpdatePhase()
        {
            if (phaseText != null)
                phaseText.text = FormatPhaseName(_phaseManager.CurrentPhaseName);
        }

        private void UpdateCountdown()
        {
            if (timerText == null) return;
            float remaining = Mathf.Max(0f, _phaseManager.PhaseRemainingTime);
            int   minutes   = Mathf.FloorToInt(remaining / 60f);
            int   seconds   = Mathf.FloorToInt(remaining % 60f);
            timerText.text  = $"{minutes:00}:{seconds:00}";
        }

        private void UpdateScore()
        {
            if (_teamAScoreText != null) _teamAScoreText.text = _cachedTeamAScore.ToString();
            if (_teamBScoreText != null) _teamBScoreText.text = _cachedTeamBScore.ToString();
        }

        private void OnScoreDataSynced(ScoreDataSyncedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.ScoreDataJson)) return;

            var snapshot = JsonUtility.FromJson<ScoreSnapshot>(evt.ScoreDataJson);
            if (snapshot?.Teams == null) return;

            foreach (var team in snapshot.Teams)
            {
                if (team.TeamId == 0) _cachedTeamAScore = team.TotalScore;
                else if (team.TeamId == 1) _cachedTeamBScore = team.TotalScore;
            }
        }

        // ── Pre-Match Countdown ───────────────────────────────────────────────

        private void OnMatchCountdown(MatchCountdownEvent evt)
        {
            if (_countdownPanel == null && _countdownText == null) return;

            if (evt.SecondsRemaining > 0)
            {
                if (_countdownPanel != null) _countdownPanel.SetActive(true);
                if (_countdownText  != null) _countdownText.text = evt.SecondsRemaining.ToString();
            }
            else
            {
                // SecondsRemaining == 0 → show GO! then hide
                if (_countdownText  != null) _countdownText.text = "GO!";
                StartCoroutine(HideCountdownAfterDelay(0.8f));
            }
        }

        private IEnumerator HideCountdownAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_countdownPanel != null) _countdownPanel.SetActive(false);
            if (_countdownText  != null) _countdownText.text = string.Empty;
        }

        // ── Phase Started Banner ──────────────────────────────────────────────

        private void OnPhaseStarted(PhaseStartedEvent evt)
        {
            if (_phaseStartCoroutine != null) StopCoroutine(_phaseStartCoroutine);

            if (_phaseStartPanel == null) return;

            if (_phaseStartTitleText != null)
                _phaseStartTitleText.text = FormatPhaseName(evt.Phase.ToString());

            if (_phaseObjectivesText != null)
                _phaseObjectivesText.text = string.IsNullOrEmpty(evt.ObjectivesSummary)
                    ? string.Empty
                    : evt.ObjectivesSummary;

            _phaseStartCoroutine = StartCoroutine(ShowPhaseStartBanner(_phaseStartPanel));
        }

        private IEnumerator ShowPhaseStartBanner(GameObject panel)
        {
            panel.SetActive(true);
            // Simple fade if panel has an Image (same helper used by warning banner)
            var bg = panel.GetComponentInChildren<Image>(true);
            if (bg != null)
            {
                yield return StartCoroutine(FadePanel(bg, 0f, 1f, _phaseStartFadeDuration));
                yield return new WaitForSeconds(_phaseStartHoldDuration);
                yield return StartCoroutine(FadePanel(bg, 1f, 0f, _phaseStartFadeDuration));
            }
            else
            {
                yield return new WaitForSeconds(_phaseStartHoldDuration);
            }
            panel.SetActive(false);
            _phaseStartCoroutine = null;
        }

        // ── Phase Warning ─────────────────────────────────────────────────────

        private void OnPhaseWarning(PhaseWarningEvent evt)
        {
            if (_warningCoroutine != null) StopCoroutine(_warningCoroutine);

            string msg = $"{FormatPhaseName(evt.CurrentPhase.ToString())}\n" +
                         $"ENDING IN {Mathf.CeilToInt(evt.SecondsRemaining)}s";

            _warningCoroutine = StartCoroutine(ShowWarningBanner(msg));
        }

        private IEnumerator ShowWarningBanner(string message)
        {
            if (warningPanel == null) yield break;

            if (warningText != null) warningText.text = message;
            warningPanel.SetActive(true);

            yield return StartCoroutine(FadeWarning(0f, 1f, warningFadeDuration));
            yield return new WaitForSeconds(warningHoldDuration);
            yield return StartCoroutine(FadeWarning(1f, 0f, warningFadeDuration));

            warningPanel.SetActive(false);
            _warningCoroutine = null;
        }

        private IEnumerator FadeWarning(float from, float to, float duration)
        {
            if (warningBackground == null) yield break;
            yield return StartCoroutine(FadePanel(warningBackground, from, to, duration));
        }

        private static IEnumerator FadePanel(Image image, float from, float to, float duration)
        {
            float elapsed = 0f;
            Color c       = image.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(from, to, elapsed / duration);
                image.color = c;
                yield return null;
            }
            c.a = to;
            image.color = c;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FormatPhaseName(string phase) => phase switch
        {
            "Phase1_Preparation"    => "PHASE 1: PREPARATION",
            "Phase2_HuntObjectives" => "PHASE 2: HUNT & OBJECTIVES",
            "Phase3_FinalLockdown"  => "PHASE 3: FINAL LOCKDOWN",
            _                       => phase
        };

        private static string FormatPhaseName(MatchPhaseState phase) => phase switch
        {
            MatchPhaseState.Preparation => "PHASE 1: PREPARATION",
            MatchPhaseState.Hunt        => "PHASE 2: HUNT & OBJECTIVES",
            MatchPhaseState.Lockdown    => "PHASE 3: FINAL LOCKDOWN",
            _                           => phase.ToString()
        };
    }
}
