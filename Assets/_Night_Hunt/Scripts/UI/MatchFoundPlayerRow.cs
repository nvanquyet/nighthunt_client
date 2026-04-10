using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Một hàng hiển thị thông tin 1 player trong MatchFoundOverlay.
    ///
    /// Inspector slots:
    ///   nameText       — TextMeshProUGUI: hiển thị userId hoặc username khi có
    ///   statusIcon     — Image: màu xanh khi ACCEPTED, xám khi PENDING
    ///   localIndicator — GameObject (optional): show nếu đây là local player (e.g. "(Bạn)")
    ///
    /// Flow:
    ///   1. MatchFoundOverlay.PopulatePlayers() → row.Bind(userId, isLocalPlayer)
    ///      → hiện userId tạm thời, status = PENDING (xám)
    ///   2. Khi player accept → MatchFoundOverlay.MarkPlayerAccepted(userId)
    ///      → row.SetAccepted(true) → status = ACCEPTED (xanh)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchFoundPlayerRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image           statusIcon;
        [SerializeField] private GameObject      localIndicator;   // "(Bạn)" label — optional

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color PendingColor  = new Color(0.55f, 0.55f, 0.55f, 1f);   // grey
        private static readonly Color AcceptedColor = new Color(0.18f, 0.82f, 0.42f, 1f);   // green

        // ── Public ────────────────────────────────────────────────────────────

        public long UserId { get; private set; }

        /// <summary>
        /// Bind row tới một player.
        /// nameText hiện UserId tạm thời; màu statusIcon = PENDING.
        /// </summary>
        public void Bind(long userId, bool isLocalPlayer)
        {
            UserId = userId;

            if (nameText != null)
                nameText.text = isLocalPlayer ? $"Bạn ({userId})" : $"Player {userId}";

            if (statusIcon      != null) statusIcon.color = PendingColor;
            if (localIndicator  != null) localIndicator.SetActive(isLocalPlayer);
        }

        /// <summary>Cập nhật trạng thái visual sau khi player này đã accept.</summary>
        public void SetAccepted(bool accepted)
        {
            if (statusIcon != null)
                statusIcon.color = accepted ? AcceptedColor : PendingColor;
        }

        /// <summary>
        /// Update display name khi có username thật (từ registry / profile service).
        /// </summary>
        public void SetDisplayName(string displayName)
        {
            if (nameText == null || string.IsNullOrEmpty(displayName)) return;

            bool isLocal = nameText.text.StartsWith("Bạn");
            nameText.text = isLocal ? $"Bạn ({displayName})" : displayName;
        }
    }
}
