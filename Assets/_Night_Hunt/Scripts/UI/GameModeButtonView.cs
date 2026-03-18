using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Single game mode button with status badge.
    ///
    /// SETUP (Prefab):
    ///   GameModeButton (this script)
    ///   ├── ModeNameText    (TMP)
    ///   ├── StatusBadgeText (TMP)
    ///   ├── Button          (Button)
    ///   └── LockedOverlay   (GameObject)
    /// </summary>
    public class GameModeButtonView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI modeNameText;
        [SerializeField] private TextMeshProUGUI statusBadgeText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject lockedOverlay;

        private GameModeResponse _mode;
        private System.Action<GameModeResponse> _onClick;

        public void Setup(GameModeResponse mode, System.Action<GameModeResponse> onClick)
        {
            _mode    = mode;
            _onClick = onClick;

            if (modeNameText    != null) modeNameText.text = mode.displayName;
            if (statusBadgeText != null)
            {
                statusBadgeText.text  = mode.status;
                statusBadgeText.color = GetStatusColor(mode.status);
            }

            if (lockedOverlay != null) lockedOverlay.SetActive(!mode.isEnabled);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(_mode));
                button.interactable = mode.isEnabled;
            }
        }

        private static Color GetStatusColor(string status) => status switch
        {
            "ACTIVE"      => Color.green,
            "COMING_SOON" => Color.yellow,
            "DISABLED"    => Color.red,
            _             => Color.gray
        };
    }
}
