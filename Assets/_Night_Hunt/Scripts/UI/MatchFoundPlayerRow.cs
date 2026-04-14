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
    ///   statusIcon     — Image: màu amber trong khi server đang khởi động
    ///   localIndicator — GameObject (optional): show nếu đây là local player (e.g. "(Bạn)")
    ///
    /// Flow:
    ///   MatchFoundOverlay.PopulatePlayers() → row.Bind(userId, isLocalPlayer)
    ///   Overlay là thông tin thuần — không có accept/decline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchFoundPlayerRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image           statusIcon;
        [SerializeField] private GameObject      localIndicator;   // "(Bạn)" label — optional

        // ── Colors ────────────────────────────────────────────────────────────────────────────────────
        private static readonly Color LoadingColor = new Color(0.95f, 0.72f, 0.17f, 1f);    // amber

        // ── Public ────────────────────────────────────────────────────────────

        public long UserId { get; private set; }

        /// <summary>
        /// Bind row tới một player.
        /// nameText hiện UserId tạm thời; statusIcon màu amber (waiting for server).
        /// </summary>
        public void Bind(long userId, bool isLocalPlayer)
        {
            UserId = userId;

            if (nameText != null)
                nameText.text = isLocalPlayer ? $"Bạn ({userId})" : $"Player {userId}";

            if (statusIcon      != null) statusIcon.color = LoadingColor;
            if (localIndicator  != null) localIndicator.SetActive(isLocalPlayer);
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
