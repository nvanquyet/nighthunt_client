using UnityEngine;
using TMPro;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component that displays interaction and pickup prompts.
    /// Subscribes to InteractionEvents to show/hide prompts dynamically.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("Prompt UI")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TextMeshProUGUI promptText;

        private IInteractable currentInteractable;
        private IPickupable currentPickupable;

        private void Awake()
        {
            // Find prompt panel and text if not assigned
            if (promptPanel == null)
            {
                promptPanel = gameObject;
            }

            if (promptText == null)
            {
                promptText = GetComponentInChildren<TextMeshProUGUI>();
                if (promptText == null)
                {
                    Debug.LogWarning($"[InteractionPromptUI] PromptText not found on {gameObject.name}. Please assign manually.");
                }
            }

            // Hide initially
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Subscribe to interaction events
            InteractionEvents.OnInteractTargetChanged += HandleInteractTargetChanged;
            InteractionEvents.OnInteractTargetLost += HandleInteractTargetLost;
            InteractionEvents.OnPickupTargetChanged += HandlePickupTargetChanged;
            InteractionEvents.OnPickupTargetLost += HandlePickupTargetLost;
        }

        private void OnDisable()
        {
            // Unsubscribe from interaction events
            InteractionEvents.OnInteractTargetChanged -= HandleInteractTargetChanged;
            InteractionEvents.OnInteractTargetLost -= HandleInteractTargetLost;
            InteractionEvents.OnPickupTargetChanged -= HandlePickupTargetChanged;
            InteractionEvents.OnPickupTargetLost -= HandlePickupTargetLost;
        }

        private void HandleInteractTargetChanged(IInteractable interactable, string promptText)
        {
            // Clear pickup target when interactable appears
            currentPickupable = null;
            currentInteractable = interactable;
            
            ShowPrompt(promptText);
        }

        private void HandleInteractTargetLost()
        {
            currentInteractable = null;
            
            // Only hide if no pickup target
            if (currentPickupable == null)
            {
                HidePrompt();
            }
        }

        private void HandlePickupTargetChanged(IPickupable pickupable, string promptText)
        {
            // Clear interactable target when pickup appears
            currentInteractable = null;
            currentPickupable = pickupable;
            
            ShowPrompt(promptText);
        }

        private void HandlePickupTargetLost()
        {
            currentPickupable = null;
            
            // Only hide if no interactable target
            if (currentInteractable == null)
            {
                HidePrompt();
            }
        }

        private void ShowPrompt(string text)
        {
            if (promptText != null)
            {
                promptText.text = text;
            }

            if (promptPanel != null)
            {
                promptPanel.SetActive(true);
            }
        }

        private void HidePrompt()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
        }
    }
}
