using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.UI
{
    /// <summary>
    /// Match HUD: phase name, countdown timer, score, two-team member lists.
    ///
    /// Team list populate DUY NHẤT 1 LẦN khi nhận AllPlayersReadyEvent.
    /// Source: PlayerPublicRegistry (dict sẵn có, không FindObjectsByType).
    /// Alive/dead tự cập nhật qua NetworkPlayer.OnAliveChanged bên trong TeamMemberRow.
    /// </summary>
    public class MatchUI : MonoBehaviour
    {
        // ── Phase ─────────────────────────────────────────────────────────────
        [Header("Phase Display")]
        [SerializeField] private TextMeshProUGUI phaseText;

        // ── Timer ─────────────────────────────────────────────────────────────
        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;

        // ── Score ─────────────────────────────────────────────────────────────
        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI teamScoreText;
        [SerializeField] private TextMeshProUGUI personalScoreText;

        // ── Team Display ──────────────────────────────────────────────────────
        [Header("Team Display")]
        [SerializeField] private Transform       teamAListParent;
        [SerializeField] private Transform       teamBListParent;
        [SerializeField] private TextMeshProUGUI teamAHeaderText;
        [SerializeField] private TextMeshProUGUI teamBHeaderText;
        [SerializeField] private GameObject      teamMemberPrefab;

        [Tooltip("Avatar sprites indexed by CharacterModelIndex.")]
        [SerializeField] private Sprite[]        characterAvatars;

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

        // Rows — allocated once, never touched again after populate
        private readonly List<TeamMemberRow> _teamARows = new();
        private readonly List<TeamMemberRow> _teamBRows = new();

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (warningPanel    != null) warningPanel.SetActive(false);
            if (teamAHeaderText != null) teamAHeaderText.text = "TEAM A";
            if (teamBHeaderText != null) teamBHeaderText.text = "TEAM B";
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
            GameplayEventBus.Instance?.Subscribe<PhaseWarningEvent>(OnPhaseWarning);
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
            GameplayEventBus.Instance?.Unsubscribe<PhaseWarningEvent>(OnPhaseWarning);
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

        // ── Team Populate — chỉ chạy 1 lần ──────────────────────────────────

        private void OnAllPlayersReady(AllPlayersReadyEvent _)
        {
            var registry = PlayerPublicRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning("[MatchUI] PlayerPublicRegistry not found.");
                return;
            }

            PopulateTeam(registry.GetPlayersByTeam(0), teamAListParent, _teamARows);
            PopulateTeam(registry.GetPlayersByTeam(1), teamBListParent, _teamBRows);
        }

        private void PopulateTeam(List<NetworkPlayer> players,
                                   Transform           parent,
                                   List<TeamMemberRow> rows)
        {
            if (parent == null || teamMemberPrefab == null) return;

            foreach (var player in players)
            {
                if (player == null) continue;

                var go  = Instantiate(teamMemberPrefab, parent);
                var row = go.GetComponent<TeamMemberRow>();

                if (row == null)
                {
                    Debug.LogWarning("[MatchUI] teamMemberPrefab missing TeamMemberRow component.");
                    Destroy(go);
                    continue;
                }

                row.Bind(player, characterAvatars);
                rows.Add(row);
            }
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
            // TODO: wire to actual scoring system
            if (teamScoreText     != null) teamScoreText.text     = "Team Score: 0";
            if (personalScoreText != null) personalScoreText.text = "Your Score: 0";
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