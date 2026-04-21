using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Visual ghost displayed while the player is dragging an item.
    /// Spawned by <see cref="DragDropController"/> and positioned on the drag canvas.
    ///
    /// NEW: <see cref="SnapBackToOrigin"/> animates the ghost back to a source slot
    /// when a drag is cancelled or dropped on an invalid target.
    /// </summary>
    public class DragDropGhost : MonoBehaviour
    {
        [SerializeField] private Image       _icon;
        [SerializeField] private Image       _background;
        [SerializeField] private Image       _highlightFrame;
        [SerializeField] private CanvasGroup _canvasGroup;

        public RectTransform RectTransform => (RectTransform)transform;

        // ─────────────────────────────────────────────────────────────────────
        #region Setup

        /// <summary>Copy visual state from <paramref name="sourceView"/> to the ghost.</summary>
        public void SetupFromSlot(ItemSlotView sourceView)
        {
            var state = sourceView?.State;
            if (state == null) return;

            if (_icon != null)
            {
                _icon.sprite  = state.Icon;
                _icon.enabled = state.Icon != null;
            }

            if (_background != null)
                _background.color = state.BackgroundColor;

            if (_highlightFrame != null)
                _highlightFrame.enabled = state.IsHighlight;

            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
        }

        public void SetAlpha(float alpha)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = alpha;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Snap-back Animation

        /// <summary>
        /// Animate the ghost from its current screen position back to
        /// <paramref name="originSlotRect"/> over <paramref name="duration"/> seconds,
        /// fading it out simultaneously.
        ///
        /// Destroys the ghost GameObject at the end.
        /// Called by <see cref="DragDropController"/> on drag cancel or invalid drop.
        /// </summary>
        public Coroutine SnapBackToOrigin(RectTransform originSlotRect, float duration = 0.18f)
            => StartCoroutine(SnapBackRoutine(originSlotRect, duration));

        private IEnumerator SnapBackRoutine(RectTransform originSlotRect, float duration)
        {
            if (originSlotRect == null)
            {
                Destroy(gameObject);
                yield break;
            }

            var rt        = RectTransform;
            Vector3 start = rt.position;
            Vector3 end   = originSlotRect.position;
            float   elapsed = 0f;

            float safeDuration = Mathf.Max(duration, 0.01f);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t  = Mathf.Clamp01(elapsed / safeDuration);
                float ease = 1f - (1f - t) * (1f - t); // ease-out quadratic

                rt.position = Vector3.Lerp(start, end, ease);

                if (_canvasGroup != null)
                    _canvasGroup.alpha = Mathf.Lerp(0.8f, 0f, ease);

                yield return null;
            }

            Destroy(gameObject);
        }

        #endregion
    }
}
