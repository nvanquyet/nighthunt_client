using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Core.Abstractions
{
    /// <summary>
    /// Base class for all interactable objects.
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] protected float interactionRange = 3f;
        [SerializeField] protected InteractionType interactionType = InteractionType.Immediate;
        [SerializeField] protected float requiredHoldTime = 0f;
        [SerializeField] protected string interactionText = "Press E to interact";

        [Header("Visual")]
        [SerializeField] protected bool showHighlight = true;
        [SerializeField] protected Color highlightColor = Color.yellow;

        protected GameObject currentInteractor;

        protected virtual void Awake()
        {
            // Base implementation - can be overridden
        }

        /// <summary>
        /// Check if this object can be interacted with.
        /// </summary>
        public virtual bool CanInteract(GameObject interactor)
        {
            if (interactor == null)
                return false;

            float distance = Vector3.Distance(transform.position, interactor.transform.position);
            return distance <= interactionRange;
        }

        /// <summary>
        /// Perform the interaction.
        /// </summary>
        public abstract void Interact(GameObject interactor);

        /// <summary>
        /// Get the interaction text.
        /// </summary>
        public virtual string GetInteractionText()
        {
            if (interactionType == InteractionType.Hold && requiredHoldTime > 0f)
            {
                return $"{interactionText} ({requiredHoldTime:F1}s)";
            }
            return interactionText;
        }

        /// <summary>
        /// Called when player starts looking at this interactable.
        /// </summary>
        public virtual void OnInteractionStart(GameObject interactor)
        {
            currentInteractor = interactor;

            if (showHighlight)
            {
                // Enable highlight effect
                EnableHighlight();
            }
        }

        /// <summary>
        /// Called when player stops looking at this interactable.
        /// </summary>
        public virtual void OnInteractionEnd(GameObject interactor)
        {
            if (currentInteractor == interactor)
            {
                currentInteractor = null;

                if (showHighlight)
                {
                    DisableHighlight();
                }
            }
        }

        /// <summary>
        /// Get the interaction type.
        /// </summary>
        public virtual InteractionType GetInteractionType()
        {
            return interactionType;
        }

        /// <summary>
        /// Get the required hold time.
        /// </summary>
        public virtual float GetRequiredHoldTime()
        {
            return requiredHoldTime;
        }

        /// <summary>
        /// Get the interaction range.
        /// </summary>
        public virtual float GetInteractionRange()
        {
            return interactionRange;
        }

        /// <summary>
        /// Enable highlight effect (override for custom behavior).
        /// </summary>
        protected virtual void EnableHighlight()
        {
            // Default implementation - can be overridden
        }

        /// <summary>
        /// Disable highlight effect (override for custom behavior).
        /// </summary>
        protected virtual void DisableHighlight()
        {
            // Default implementation - can be overridden
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
