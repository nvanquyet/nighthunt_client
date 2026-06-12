using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Scoring;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.UI
{
    /// <summary>
    /// Match HUD: zone index display, shrink countdown timer, score, two-team member lists.
    ///
    /// Team list populate DUY NHẤT 1 LẦN khi nhận AllPlayersReadyEvent.
    /// Source: PlayerPublicRegistry (dict sẵn có, không FindObjectsByType).
    /// Alive/dead tự update qua NetworkPlayer.OnAliveChanged bên trong TeamMemberRow.
    /// </summary>
    public class MatchUI : MonoBehaviour
    {
        // ── Zone Display ─────────────────────────────────────────────────────
        [Header("Zone Display")]
        [Tooltip("Shows 'ZONE 1', 'ZONE 2', etc. or 'FINAL ZONE'.")]
        // Compact zone label in the main HUD bar (top area). Intentionally separate from
        // SafeZoneHUD._zoneLabel which drives the dedicated zone-ring panel on its own canvas.
        // Both subscribe to SafeZoneHUDProxy events but update DIFFERENT TMP components.
        [SerializeField] private TextMeshProUGUI phaseText;

        // ── Timer ─────────────────────────────────────────────────────────────
        [Header("Timer")]
        // Compact countdown in the main HUD bar. Intentionally separate from
        // SafeZoneHUD._countdownText which drives the zone-ring panel countdown.
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
        private float             _lastUpdateTime;
        private const float       UpdateInterval = 0.1f;
        private NetworkPlayer     _localPlayer;
        private int               _cachedTeamAScore;
        private int               _cachedTeamBScore;
        // Zone HUD cache
        private int               _cachedZoneIndex = -1;
        private float             _cachedCountdown;
        private bool              _cachedIsShrinking;

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
            SafeZoneHUDProxy.OnZoneIndexChanged   += OnZoneIndexChanged;
            SafeZoneHUDProxy.OnCountdownChanged   += OnCountdownChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged += OnShrinkStateChanged;
            SafeZoneManager.Instance?.ReplayCurrentHudState();
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<PhaseWarningEvent>(OnPhaseWarning);
            GameplayEventBus.Instance?.Unsubscribe<PhaseStartedEvent>(OnPhaseStarted);
            GameplayEventBus.Instance?.Unsubscribe<MatchCountdownEvent>(OnMatchCountdown);
            GameplayEventBus.Instance?.Unsubscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);
            SafeZoneHUDProxy.OnZoneIndexChanged   -= OnZoneIndexChanged;
            SafeZoneHUDProxy.OnCountdownChanged   -= OnCountdownChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged -= OnShrinkStateChanged;
        }

        private void Start() { }

        private void Update()
        {
            if (Time.time - _lastUpdateTime < UpdateInterval) return;
            _lastUpdateTime = Time.time;
            UpdateDisplay();
        }

        // ── Display ───────────────────────────────────────────────────────────

        private void UpdateDisplay()
        {
            UpdateZone();
            UpdateCountdownTimer();
            UpdateScore();
        }

        private void UpdateScore()
        {
            if (_teamAScoreText != null) _teamAScoreText.text = _cachedTeamAScore.ToString();
            if (_teamBScoreText != null) _teamBScoreText.text = _cachedTeamBScore.ToString();
        }

        // ── SafeZone HUD handlers ─────────────────────────────────────────────

        private void OnZoneIndexChanged(int zoneIndex)
        {
            _cachedZoneIndex = zoneIndex;
            UpdateZone();
        }

        private void OnCountdownChanged(float t)
        {
            _cachedCountdown = t;
            UpdateCountdownTimer();
        }

        private void OnShrinkStateChanged(bool shrinking)
        {
            _cachedIsShrinking = shrinking;
        }

        private void UpdateZone()
        {
            if (phaseText == null) return;
            bool isFinal = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            phaseText.text = isFinal ? "FINAL ZONE" : $"ZONE {_cachedZoneIndex + 1}";
        }

        private void UpdateCountdownTimer()
        {
            if (timerText == null) return;
            int mins = Mathf.FloorToInt(_cachedCountdown / 60f);
            int secs = Mathf.FloorToInt(_cachedCountdown % 60f);
            timerText.text = _cachedCountdown > 0f
                ? (mins > 0 ? $"{mins}:{secs:D2}" : $"{secs}s")
                : (_cachedIsShrinking ? "SHRINKING" : "--");
        }

        private void OnScoreDataSynced(ScoreDataSyncedEvent evt)
        {
            UnityEngine.Debug.Log($"[MatchUI] OnScoreDataSynced: Received Json = '{evt.ScoreDataJson}'");
            if (string.IsNullOrEmpty(evt.ScoreDataJson)) return;

            var snapshot = JsonUtility.FromJson<ScoreSnapshot>(evt.ScoreDataJson);
            if (snapshot == null || snapshot.Teams == null)
            {
                UnityEngine.Debug.LogWarning($"[MatchUI] OnScoreDataSynced: snapshot is null or Teams is null. snapshot={snapshot != null}");
                return;
            }

            foreach (var team in snapshot.Teams)
            {
                UnityEngine.Debug.Log($"[MatchUI] Team {team.TeamId} Score={team.TotalScore}");
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
            // Legacy event — no-op now; zone transitions handled via SafeZoneHUDProxy.OnZoneIndexChanged
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
        }

        // ── Phase Warning ─────────────────────────────────────────────────────

        private void OnPhaseWarning(PhaseWarningEvent evt)
        {
            // Legacy event kept for backward compat — no-op now; zone shrink warning handled via SafeZoneHUDProxy
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

        private static string FormatZoneLabel(int zoneIndex, bool isFinal)
            => isFinal ? "FINAL ZONE" : $"ZONE {zoneIndex + 1}";

    }
}
