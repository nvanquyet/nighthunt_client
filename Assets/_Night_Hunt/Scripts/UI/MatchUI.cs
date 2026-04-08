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
        private int               _cachedTeamScore;
        private int               _cachedPersonalScore;

        // Rows — allocated once per round; cleared before each populate
        private readonly List<TeamMemberRow> _teamARows = new();
        private readonly List<TeamMemberRow> _teamBRows = new();
        private bool _teamsPopulated = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (warningPanel    != null) warningPanel.SetActive(false);
            if (teamAHeaderText != null) teamAHeaderText.text = "TEAM A";
            if (teamBHeaderText != null) teamBHeaderText.text = "TEAM B";
        }

        /// <summary>Call once when local NetworkPlayer is known (from GameHUD or spawner).</summary>
        public void Initialize(NetworkPlayer localPlayer)
        {
            _localPlayer = localPlayer;
        }

        private void OnEnable()
        {
            GameplayEventBus.Instance?.Subscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
            GameplayEventBus.Instance?.Subscribe<PhaseWarningEvent>(OnPhaseWarning);
            GameplayEventBus.Instance?.Subscribe<ScoreDataSyncedEvent>(OnScoreDataSynced);
        }

        private void OnDisable()
        {
            GameplayEventBus.Instance?.Unsubscribe<AllPlayersReadyEvent>(OnAllPlayersReady);
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

        // ── Team Populate — chỉ chạy 1 lần ──────────────────────────────────

        private void OnAllPlayersReady(AllPlayersReadyEvent _)
        {
            // Guard: even if the event somehow fires twice (or the subscription
            // was added twice), only populate once per session.
            if (_teamsPopulated) return;
            _teamsPopulated = true;

            // Defer to end-of-frame so all _playerData SyncVars (team IDs) have
            // settled before we read TeamId from each NetworkPlayer.
            StartCoroutine(PopulateTeamsNextFrame());
        }

        private System.Collections.IEnumerator PopulateTeamsNextFrame()
        {
            yield return null; // wait one frame for SyncVar deltas to process

            var registry = PlayerPublicRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning("[MatchUI] PlayerPublicRegistry not found.");
                _teamsPopulated = false; // allow retry
                yield break;
            }

            ClearTeamRows(_teamARows, teamAListParent);
            ClearTeamRows(_teamBRows, teamBListParent);
            PopulateTeam(registry.GetPlayersByTeam(0), teamAListParent, _teamARows);
            PopulateTeam(registry.GetPlayersByTeam(1), teamBListParent, _teamBRows);
        }

        private static void ClearTeamRows(List<TeamMemberRow> rows, Transform parent)
        {
            foreach (var row in rows)
                if (row != null) Destroy(row.gameObject);
            rows.Clear();
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

                row.Bind(player);
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
            if (teamScoreText     != null) teamScoreText.text     = $"Team Score: {_cachedTeamScore}";
            if (personalScoreText != null) personalScoreText.text = $"Your Score: {_cachedPersonalScore}";
        }

        private void OnScoreDataSynced(ScoreDataSyncedEvent evt)
        {
            if (string.IsNullOrEmpty(evt.ScoreDataJson)) return;

            var snapshot = JsonUtility.FromJson<ScoreSnapshot>(evt.ScoreDataJson);
            if (snapshot == null) return;

            if (_localPlayer != null && snapshot.Teams != null)
            {
                foreach (var team in snapshot.Teams)
                {
                    if (team.TeamId == _localPlayer.TeamId)
                    {
                        _cachedTeamScore = team.TotalScore;
                        break;
                    }
                }
            }

            if (_localPlayer != null && snapshot.Players != null)
            {
                uint myId = (uint)_localPlayer.ObjectId;
                foreach (var player in snapshot.Players)
                {
                    if (player.PlayerId == myId)
                    {
                        _cachedPersonalScore = player.TotalScore;
                        break;
                    }
                }
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

#if UNITY_EDITOR
        // ── Editor — Context Menu: Create TeamMemberRow Template Prefab ───────

        [ContextMenu("NightHunt/Create TeamMemberRow Template Prefab")]
        private void Editor_CreateTeamMemberRowPrefab()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            const string path = dir + "/TeamMemberRow_Template.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[MatchUI] TeamMemberRow_Template already exists at {path}");
                return;
            }

            var go  = new GameObject("TeamMemberRow_Template");
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(200f, 36f);
            go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.1f, 0.15f, 0.1f, 0.7f);
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.spacing = 4f;
            hlg.padding = new RectOffset(4, 4, 2, 2);

            // Avatar
            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            avatarGo.transform.SetParent(go.transform, false);
            avatarGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 32f;

            // Name
            var nameGo  = new GameObject("NameText", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            nameGo.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
            nameGo.GetComponent<TMPro.TextMeshProUGUI>().text = "PlayerName";

            // Alive/Dead indicator
            var aliveGo = new GameObject("AliveIndicator", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            aliveGo.transform.SetParent(go.transform, false);
            aliveGo.GetComponent<UnityEngine.UI.Image>().color = Color.green;
            aliveGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 14f;

            var deadGo  = new GameObject("DeadIndicator", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            deadGo.transform.SetParent(go.transform, false);
            deadGo.GetComponent<UnityEngine.UI.Image>().color = Color.red;
            deadGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 14f;
            deadGo.SetActive(false);

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (teamMemberPrefab == null)
            {
                teamMemberPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[MatchUI] Created TeamMemberRow_Template at {path}. " +
                      "Add TeamMemberRow component and wire name/alive/dead fields.");
        }
#endif
    }
}