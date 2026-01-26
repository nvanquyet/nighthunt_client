using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Utilities
{
    /// <summary>
    /// Debug component for testing interactions.
    /// </summary>
    public class InteractionDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebug = true;

        private InteractableBase interactable;

        private void Awake()
        {
            interactable = GetComponent<InteractableBase>();
        }

        /// <summary>
        /// Test interaction (called from editor).
        /// </summary>
        public void TestInteract()
        {
            if (interactable == null)
                return;

            GameObject testPlayer = new GameObject("TestPlayer");
            testPlayer.transform.position = transform.position + Vector3.forward * 2f;

            if (interactable.CanInteract(testPlayer))
            {
                interactable.Interact(testPlayer);
                Debug.Log($"[InteractionDebugger] Interaction test successful: {interactable.GetInteractionText()}");
            }
            else
            {
                Debug.LogWarning($"[InteractionDebugger] Cannot interact: {interactable.GetInteractionText()}");
            }

            Destroy(testPlayer);
        }

        /// <summary>
        /// Test if interaction is possible.
        /// </summary>
        public bool TestCanInteract()
        {
            if (interactable == null)
                return false;

            GameObject testPlayer = new GameObject("TestPlayer");
            testPlayer.transform.position = transform.position + Vector3.forward * 2f;

            bool canInteract = interactable.CanInteract(testPlayer);
            Destroy(testPlayer);

            return canInteract;
        }
    }
}
