using NightHunt.Gameplay.Character.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// MatchPlayerCardView — một card display thông tin 1 player trong MatchLoadingOverlay.
    ///
    /// Inspector layout gợi ý:
    ///   Root (MatchPlayerCard prefab)
    ///   ├── AvatarFrame
    ///   │   └── avatarImage          ← character thumbnail
    ///   ├── InfoContainer
    ///   │   ├── nameText
    ///   │   ├── eloText              ← "1250 ELO"
    ///   │   └── rankText             ← "GOLD"
    ///   └── ProgressContainer
    ///       ├── progressBar          ← individual spawn progress 0→1
    ///       └── statusText           ← "Connecting…" / "Ready!"
    /// </summary>
    public class MatchPlayerCardView : MonoBehaviour
    {
        [Header("Avatar")]
        [SerializeField] private Image            avatarImage;
        [SerializeField] private Sprite           defaultAvatar;   // fallback nếu not found

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI  nameText;
        [SerializeField] private TextMeshProUGUI  eloText;
        [SerializeField] private TextMeshProUGUI  rankText;

        [Header("Progress")]
        [SerializeField] private Slider           progressBar;
        [SerializeField] private TextMeshProUGUI  statusText;

        [Header("Team Tint (optional)")]
        [Tooltip("Background panel to tint with team color.")]
        [SerializeField] private Image            teamBackground;
        [SerializeField] private Color            teamAColor = new Color(0.2f, 0.5f, 1f, 0.25f);
        [SerializeField] private Color            teamBColor = new Color(1f, 0.35f, 0.2f, 0.25f);

        // ── State ────────────────────────────────────────────────────────────────

        private float _targetProgress;
        private float _currentProgress;
        private bool  _ready;

        // ── Init ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// playerName  : username từ RoomPlayerResponse
        /// characterId : string CharacterId từ SessionState (chỉ local player biết),
        ///               null/empty → dùng character index 0
        /// elo         : ELO điểm, -1 = ẩn
        /// rank        : tier string ("GOLD", "SILVER"…), null = ẩn
        /// team        : 1 hoặc 2 (để tô màu nền)
        /// </summary>
        public void Initialize(string playerName, string characterId,
                               int elo, string rank, int team)
        {
            // Avatar
            Sprite avatar = ResolveAvatar(characterId);
            if (avatarImage != null)
                avatarImage.sprite = avatar != null ? avatar : defaultAvatar;

            // Name
            if (nameText != null)
                nameText.text = string.IsNullOrEmpty(playerName) ? "Player" : playerName;

            // ELO
            if (eloText != null)
            {
                eloText.gameObject.SetActive(elo >= 0);
                if (elo >= 0) eloText.text = $"{elo} ELO";
            }

            // Rank
            if (rankText != null)
            {
                bool hasRank = !string.IsNullOrEmpty(rank);
                rankText.gameObject.SetActive(hasRank);
                if (hasRank) rankText.text = rank.ToUpperInvariant();
            }

            // Team tint
            if (teamBackground != null)
                teamBackground.color = team == 1 ? teamAColor : teamBColor;

            // Progress reset
            _targetProgress  = 0f;
            _currentProgress = 0f;
            _ready           = false;
            SetProgressVisual(0f, "Connecting…");
        }

        // ── Update ───────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_ready) return;
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.deltaTime * 0.5f);
            SetProgressVisual(_currentProgress, null);
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Set normalised progress [0..1] for this player's spawn phase.</summary>
        public void SetProgress(float normalised01, string statusOverride = null)
        {
            _targetProgress = Mathf.Clamp01(normalised01);
            if (!string.IsNullOrEmpty(statusOverride))
                SetProgressVisual(_currentProgress, statusOverride);
        }

        /// <summary>Mark this player as fully ready (progress = 1, status = "Ready!").</summary>
        public void MarkReady()
        {
            _ready          = true;
            _targetProgress = 1f;
            _currentProgress = 1f;
            SetProgressVisual(1f, "Ready!");
        }

        /// <summary>Reset card to connecting state (reuse pooled instance).</summary>
        public void ResetToConnecting()
        {
            _targetProgress  = 0.1f;
            _currentProgress = 0f;
            _ready           = false;
            SetProgressVisual(0f, "Connecting…");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static Sprite ResolveAvatar(string characterId)
        {
            var db = CharacterDatabase.Instance;
            if (db == null) return null;

            if (!string.IsNullOrEmpty(characterId))
            {
                var def = db.GetById(characterId);
                if (def?.Thumbnail != null) return def.Thumbnail;
            }

            // Fallback: index 0 (default character)
            var fallback = db.GetByIndex(0);
            return fallback?.Thumbnail;
        }

        private void SetProgressVisual(float value, string label)
        {
            if (progressBar != null) progressBar.value = value;
            if (statusText  != null && label != null)  statusText.text  = label;
        }
    }
}
