using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Single player row inside <see cref="MatchFoundOverlay"/>.
    /// Displays player id / display name and an accepted indicator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchFoundPlayerRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image           statusIcon;
        [SerializeField] private Color           pendingColor  = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color           acceptedColor = new Color(0.2f, 0.9f, 0.4f, 1f);

        public long UserId { get; private set; }

        /// <summary>Bind row to a player id. Display name resolves later via profile cache.</summary>
        public void Bind(long userId, bool isLocalPlayer)
        {
            UserId = userId;
            if (nameText != null)
                nameText.text = isLocalPlayer ? "You" : $"Player {userId}";
            if (statusIcon != null)
                statusIcon.color = pendingColor;
        }

        /// <summary>Mark player as accepted / ready.</summary>
        public void SetAccepted(bool accepted)
        {
            if (statusIcon != null)
                statusIcon.color = accepted ? acceptedColor : pendingColor;
        }

        /// <summary>Update display name once resolved from profile service.</summary>
        public void SetDisplayName(string displayName)
        {
            if (nameText != null && !string.IsNullOrEmpty(displayName))
                nameText.text = displayName;
        }
    }
}
