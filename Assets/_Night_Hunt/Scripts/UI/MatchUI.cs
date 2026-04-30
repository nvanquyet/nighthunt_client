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
        private NetworkPlayer     _localPlayer;
        private int               _cachedTeamAScore;
        private int               _cachedTeamBScore;



        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (warningPanel != null) warningPanel.SetActive(false);
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
            GameplayEventBus.Instance?.Subscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<PhaseWarningEvent>(OnPhaseWarning);
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

            float elapsed = 0f;
            Color c       = warningBackground.color;
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

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FormatPhaseName(string phase) => phase switch
        {
            "Phase1_Preparation"    => "PHASE 1: PREPARATION",
            "Phase2_HuntObjectives" => "PHASE 2: HUNT & OBJECTIVES",
            "Phase3_FinalLockdown"  => "PHASE 3: FINAL LOCKDOWN",
            _                       => phase
        };


    }
}