using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI
{
    /// <summary>
    /// Single toast notification item.
    /// Attach this to the ToastItem prefab so ToastService can wire up text
    /// and background via direct references instead of GetComponentInChildren.
    /// </summary>
    public class ToastItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Image           _background;

        public TextMeshProUGUI MessageText => _messageText;
        public Image           Background  => _background;
    }
}
