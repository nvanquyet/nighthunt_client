using UnityEngine;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Component to manage visual state of containers (closed ↔ opened)
    /// Attach to LootContainer or ShopContainer GameObject
    /// </summary>
    public class ContainerVisualState : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private GameObject closedModel;
        [SerializeField] private GameObject openedModel;
        [SerializeField] private Renderer containerRenderer;
        [SerializeField] private Color closedColor = Color.white;
        [SerializeField] private Color openedColor = Color.green;

        [Header("Animation (Optional)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string closedAnimationTrigger = "Close";
        [SerializeField] private string openedAnimationTrigger = "Open";

        private bool isOpened = false;

        private void Awake()
        {
            // Auto-find models if not assigned
            if (closedModel == null)
            {
                closedModel = transform.Find("ClosedModel")?.gameObject;
            }
            if (openedModel == null)
            {
                openedModel = transform.Find("OpenedModel")?.gameObject;
            }

            // Auto-find renderer if not assigned
            if (containerRenderer == null)
            {
                containerRenderer = GetComponent<Renderer>();
            }

            // Auto-find animator if not assigned
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            // Initialize to closed state
            SetOpened(false);
        }

        /// <summary>
        /// Set container opened state
        /// </summary>
        public void SetOpened(bool opened)
        {
            isOpened = opened;

            // Swap models
            if (closedModel != null)
            {
                closedModel.SetActive(!opened);
            }
            if (openedModel != null)
            {
                openedModel.SetActive(opened);
            }

            // Change color
            if (containerRenderer != null && containerRenderer.material != null)
            {
                containerRenderer.material.color = opened ? openedColor : closedColor;
            }

            // Play animation
            if (animator != null)
            {
                if (opened)
                {
                    animator.SetTrigger(openedAnimationTrigger);
                }
                else
                {
                    animator.SetTrigger(closedAnimationTrigger);
                }
            }
        }

        /// <summary>
        /// Check if container is opened
        /// </summary>
        public bool IsOpened()
        {
            return isOpened;
        }
    }
}
