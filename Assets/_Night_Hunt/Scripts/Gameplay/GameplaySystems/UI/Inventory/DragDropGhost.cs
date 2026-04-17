using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Visual ghost được dùng khi drag item.
    /// Được spawn bởi DragDropController.
    /// </summary>
    public class DragDropGhost : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _background;
        [SerializeField] private Image _highlightFrame;
        [SerializeField] private CanvasGroup _canvasGroup;

        public RectTransform RectTransform => (RectTransform)transform;

        public void SetupFromSlot(ItemSlotView sourceView)
        {
            var state = sourceView.State;

            if (_icon != null)
            {
                _icon.sprite = state.Icon;
                _icon.enabled = state.Icon != null;
            }

            if (_background != null)
            {
                _background.color = state.BackgroundColor;
            }

            if (_highlightFrame != null)
            {
                _highlightFrame.enabled = state.IsHighlight;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f; // Set về 1 để display ghost
            }
        }

        public void SetAlpha(float alpha)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
            }
        }
    }
}

