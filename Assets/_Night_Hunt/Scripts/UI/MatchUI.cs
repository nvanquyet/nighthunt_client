using UnityEngine;
using TMPro;
using NightHunt.Gameplay.Match;

namespace NightHunt.UI
{
    /// <summary>
    /// Match UI showing phase, timer, score, etc.
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

        private MatchPhaseManager phaseManager;
        private float updateInterval = 0.1f;
        private float lastUpdateTime;

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
    }
}

