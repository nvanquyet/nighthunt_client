using UnityEngine;
using DG.Tweening;

namespace NightHunt.InteractionSystem.Pickup.Animation
{
    /// <summary>
    /// Handles pickup animations using DOTween.
    /// </summary>
    public class PickupAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float pickupAnimationDuration = 0.3f;
        [SerializeField] private float highlightPulseDuration = 1f;
        [SerializeField] private Color highlightColor = Color.yellow;

        private Material highlightMaterial;
        private Tween highlightTween;
        private Tween pickupTween;

        /// <summary>
        /// Start highlight animation for pickupable item.
        /// </summary>
        public void StartHighlight(GameObject item)
        {
            StopHighlight();

            // Create or get highlight material
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer == null)
                return;

            highlightMaterial = renderer.material;

            // Pulse highlight color
            highlightTween = highlightMaterial.DOColor(highlightColor, highlightPulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        /// <summary>
        /// Stop highlight animation.
        /// </summary>
        public void StopHighlight()
        {
            if (highlightTween != null)
            {
                highlightTween.Kill();
                highlightTween = null;
            }

            if (highlightMaterial != null)
            {
                highlightMaterial.color = Color.white;
            }
        }

        /// <summary>
        /// Play pickup animation (item moves to player).
        /// </summary>
        public void PlayPickupAnimation(GameObject item, Transform target, System.Action onComplete = null)
        {
            if (item == null || target == null)
                return;

            StopHighlight();

            // Animate item moving to player
            pickupTween = item.transform.DOMove(target.position, pickupAnimationDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (onComplete != null)
                        onComplete();
                });

            // Scale down
            item.transform.DOScale(Vector3.zero, pickupAnimationDuration)
                .SetEase(Ease.InBack);
        }

        /// <summary>
        /// Play quick pickup animation (for auto pickup).
        /// </summary>
        public void PlayQuickPickupAnimation(GameObject item, System.Action onComplete = null)
        {
            if (item == null)
                return;

            float quickDuration = pickupAnimationDuration * 0.1f;

            // Quick scale down
            item.transform.DOScale(Vector3.zero, quickDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (onComplete != null)
                        onComplete();
                });
        }

        private void OnDestroy()
        {
            StopHighlight();

            if (pickupTween != null)
            {
                pickupTween.Kill();
            }
        }
    }
}
