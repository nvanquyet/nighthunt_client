using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component that displays interaction and pickup prompts.
    /// Subscribes to InteractionEvents to show/hide prompts dynamically.
    /// Also displays hold interaction progress bar.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("Prompt UI")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TextMeshProUGUI promptText;

        [Header("Hold Progress UI")]
        [Tooltip("Progress bar fill image (optional - for hold interactions)")]
        [SerializeField] private UnityEngine.UI.Image progressBarFill;
        
        [Tooltip("Progress text showing countdown (optional)")]
        [SerializeField] private TextMeshProUGUI progressText;

        private IInteractable currentInteractable;
        private IPickupable currentPickupable;
        private bool isHoldInteraction = false;

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
            
            // Subscribe to hold progress events
            InteractionEvents.OnHoldProgressStarted += HandleHoldProgressStarted;
            InteractionEvents.OnHoldProgressChanged += HandleHoldProgressChanged;
            InteractionEvents.OnHoldProgressCompleted += HandleHoldProgressCompleted;
            InteractionEvents.OnHoldProgressCancelled += HandleHoldProgressCancelled;
        }

        private void OnDisable()
        {
            // Unsubscribe from interaction events
            InteractionEvents.OnInteractTargetChanged -= HandleInteractTargetChanged;
            InteractionEvents.OnInteractTargetLost -= HandleInteractTargetLost;
            InteractionEvents.OnPickupTargetChanged -= HandlePickupTargetChanged;
            InteractionEvents.OnPickupTargetLost -= HandlePickupTargetLost;
            
            // Unsubscribe from hold progress events
            InteractionEvents.OnHoldProgressStarted -= HandleHoldProgressStarted;
            InteractionEvents.OnHoldProgressChanged -= HandleHoldProgressChanged;
            InteractionEvents.OnHoldProgressCompleted -= HandleHoldProgressCompleted;
            InteractionEvents.OnHoldProgressCancelled -= HandleHoldProgressCancelled;
        }

        private void HandleInteractTargetChanged(IInteractable interactable, string promptText)
        {
            Debug.Log($"[InteractionPromptUI] HandleInteractTargetChanged - Interactable: {interactable?.GetType().Name ?? "null"}, Text: {promptText}");
            
            // Clear pickup target when interactable appears
            currentPickupable = null;
            currentInteractable = interactable;
            
            // Check if this is a hold interaction
            isHoldInteraction = interactable != null && interactable.GetInteractionType() == InteractionType.Hold;
            Debug.Log($"[InteractionPromptUI] Is Hold interaction: {isHoldInteraction}");
            
            ShowPrompt(promptText);
            
            // Hide progress UI initially (will show when hold starts)
            HideHoldProgress();
        }

        private void HandleInteractTargetLost()
        {
            Debug.Log("[InteractionPromptUI] HandleInteractTargetLost");
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
            Debug.Log($"[InteractionPromptUI] ShowPrompt - Text: {text}, promptPanel={promptPanel != null}, promptText={promptText != null}");
            
            if (promptText != null)
            {
                promptText.text = text;
                Debug.Log($"[InteractionPromptUI] Set prompt text to: {text}");
            }
            else
            {
                Debug.LogWarning("[InteractionPromptUI] promptText is null! Cannot display text.");
            }

            if (promptPanel != null)
            {
                promptPanel.SetActive(true);
                Debug.Log($"[InteractionPromptUI] Activated prompt panel: {promptPanel.name}");
            }
            else
            {
                Debug.LogWarning("[InteractionPromptUI] promptPanel is null! Cannot show prompt.");
            }
        }

        private void HidePrompt()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
            
            // Also hide progress UI
            HideHoldProgress();
        }

        #region Hold Progress Handlers

        private void HandleHoldProgressStarted()
        {
            // Show progress UI when hold starts
            ShowHoldProgress();
        }

        private void HandleHoldProgressChanged(float progress)
        {
            // Update progress bar (0-1)
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = progress;
            }

            // Update progress text (countdown)
            if (progressText != null && currentInteractable != null)
            {
                float requiredTime = currentInteractable.GetRequiredHoldTime();
                float remainingTime = requiredTime * (1f - progress);
                progressText.text = remainingTime > 0.1f ? $"{remainingTime:F1}s" : "";
            }
        }

        private void HandleHoldProgressCompleted()
        {
            // Hide progress UI when completed
            HideHoldProgress();
        }

        private void HandleHoldProgressCancelled()
        {
            // Hide progress UI when cancelled
            HideHoldProgress();
        }

        private void ShowHoldProgress()
        {
            if (progressBarFill != null)
            {
                progressBarFill.gameObject.SetActive(true);
                progressBarFill.fillAmount = 0f;
            }

            if (progressText != null)
            {
                progressText.gameObject.SetActive(true);
                progressText.text = "";
            }
        }

        private void HideHoldProgress()
        {
            if (progressBarFill != null)
            {
                progressBarFill.gameObject.SetActive(false);
            }

            if (progressText != null)
            {
                progressText.gameObject.SetActive(false);
            }
        }

        #endregion
    }
}
