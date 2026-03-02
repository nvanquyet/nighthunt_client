using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.UI
{
    /// <summary>
    /// Match UI showing phase, timer, score, etc.
    /// Also shows a full-screen phase warning banner when <see cref="PhaseWarningEvent"/> fires.
    /// </summary>
    public class MatchUI : MonoBehaviour
    {
        [Header("Phase Display")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI phaseDescriptionText;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI phaseTimerText;

        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI teamScoreText;
        [SerializeField] private TextMeshProUGUI personalScoreText;

        [Header("Team Display")]
        [SerializeField] private Transform teamListParent;
        [SerializeField] private GameObject teamMemberPrefab;

        // ── Phase Warning Banner ──────────────────────────────────────────────
        [Header("Phase Warning Banner")]
        [Tooltip("Root panel — shown briefly when a phase is about to end.")]
        [SerializeField] private GameObject  warningPanel;
        [Tooltip("e.g. 'PHASE 2 ENDING IN 30s'")]
        [SerializeField] private TextMeshProUGUI warningText;
        [Tooltip("Optional background image — its alpha is faded in/out.")]
        [SerializeField] private Image       warningBackground;
        [Tooltip("How many seconds the banner stays fully visible at its peak.")]
        [SerializeField] private float       warningHoldDuration = 3f;
        [Tooltip("Fade in/out duration (each way).")]
        [SerializeField] private float       warningFadeDuration = 0.4f;
        // ──────────────────────────────────────────────────────────────────────

        private MatchPhaseManager phaseManager;
        private float updateInterval = 0.1f;
        private float lastUpdateTime;
        private Coroutine _warningCoroutine;

        private void Awake()
        {
            if (warningPanel != null)
                warningPanel.SetActive(false);

            GameplayEventBus.Instance?.Subscribe<PhaseWarningEvent>(OnPhaseWarning);
        }

        private void OnDestroy()
        {
            GameplayEventBus.Instance?.Unsubscribe<PhaseWarningEvent>(OnPhaseWarning);
        }

        private void Start()
        {
            phaseManager = FindObjectOfType<MatchPhaseManager>();
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateDisplay();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Update all UI elements
        /// </summary>
        private void UpdateDisplay()
        {
            if (phaseManager == null)
            {
                phaseManager = FindObjectOfType<MatchPhaseManager>();
                return;
            }

            UpdatePhaseDisplay();
            UpdateTimer();
            UpdateScore();
        }

        /// <summary>
        /// Update phase display
        /// </summary>
        private void UpdatePhaseDisplay()
        {
            if (phaseManager == null) return;

            string currentPhase = phaseManager.CurrentPhaseName;
            var phaseConfig = phaseManager.GetCurrentPhaseConfig();

            if (phaseText != null)
            {
                // Format phase name
                string phaseName = FormatPhaseName(currentPhase);
                phaseText.text = phaseName;
            }

            if (phaseDescriptionText != null && phaseConfig != null)
            {
                phaseDescriptionText.text = GetPhaseDescription(currentPhase);
            }
        }

        /// <summary>
        /// Update timer display
        /// </summary>
        private void UpdateTimer()
        {
            if (phaseManager == null) return;

            float remainingTime = phaseManager.PhaseRemainingTime;

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(remainingTime / 60f);
                int seconds = Mathf.FloorToInt(remainingTime % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }

            if (phaseTimerText != null)
            {
                float elapsed = phaseManager.PhaseElapsedTime;
                int minutes = Mathf.FloorToInt(elapsed / 60f);
                int seconds = Mathf.FloorToInt(elapsed % 60f);
                phaseTimerText.text = $"Phase Time: {minutes:00}:{seconds:00}";
            }
        }

        /// <summary>
        /// Update score display
        /// </summary>
        private void UpdateScore()
        {
            // Would integrate with scoring system
            if (teamScoreText != null)
            {
                teamScoreText.text = "Team Score: 0";
            }

            if (personalScoreText != null)
            {
                personalScoreText.text = "Your Score: 0";
            }
        }

        /// <summary>
        /// Format phase name for display
        /// </summary>
        private string FormatPhaseName(string phase)
        {
            switch (phase)
            {
                case "Phase1_Preparation":
                    return "PHASE 1: PREPARATION";
                case "Phase2_HuntObjectives":
                    return "PHASE 2: HUNT & OBJECTIVES";
                case "Phase3_FinalLockdown":
                    return "PHASE 3: FINAL LOCKDOWN";
                default:
                    return phase;
            }
        }

        /// <summary>
        /// Get phase description
        /// </summary>
        private string GetPhaseDescription(string phase)
        {
            switch (phase)
            {
                case "Phase1_Preparation":
                    return "Loot items and place beacons. Prepare for the hunt.";
                case "Phase2_HuntObjectives":
                    return "Boss has spawned. Capture zones are active. Hunt or be hunted.";
                case "Phase3_FinalLockdown":
                    return "Beacons disabled. Zone closing. Last team standing wins.";
                default:
                    return "";
            }
        }

        // ── Phase Warning ──────────────────────────────────────────────────────

        private void OnPhaseWarning(PhaseWarningEvent evt)
        {
            if (_warningCoroutine != null)
                StopCoroutine(_warningCoroutine);

            string phaseName = FormatPhaseName(evt.CurrentPhase.ToString());
            string msg       = $"{phaseName}\nENDING IN {Mathf.CeilToInt(evt.SecondsRemaining):F0}s";

            _warningCoroutine = StartCoroutine(ShowWarningBanner(msg, evt.SecondsRemaining));
        }

        private IEnumerator ShowWarningBanner(string message, float displaySeconds)
        {
            if (warningPanel == null) yield break;

            if (warningText != null) warningText.text = message;
            warningPanel.SetActive(true);

            // Fade in
            yield return StartCoroutine(FadeWarning(0f, 1f, warningFadeDuration));

            // Hold for the lesser of warningHoldDuration or remaining time
            float hold = Mathf.Min(warningHoldDuration, displaySeconds);
            yield return new WaitForSeconds(hold);

            // Fade out
            yield return StartCoroutine(FadeWarning(1f, 0f, warningFadeDuration));

            warningPanel.SetActive(false);
            _warningCoroutine = null;
        }

        private IEnumerator FadeWarning(float from, float to, float duration)
        {
            if (warningBackground == null) yield break;

            float elapsed = 0f;
            Color c = warningBackground.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(from, to, elapsed / duration);
                warningBackground.color = c;
                yield return null;
            }
            c.a = to;
            warningBackground.color = c;
        }
    }
}

