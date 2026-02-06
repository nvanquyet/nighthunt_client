using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Core.Interfaces;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.UI.Prompts
{
    /// <summary>
    /// Displays interaction prompts (F to pickup, E to open).
    /// Shows progress bar for hold interactions.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private GameObject progressBarRoot;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private IInteractable currentInteractable;
        
        #region Lifecycle
        
        void Awake()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
        }
        
        void OnEnable()
        {
            InteractionEvents.OnInteractableDetected += ShowPrompt;
            InteractionEvents.OnInteractableLost += HidePrompt;
            InteractionEvents.OnHoldProgress += UpdateProgress;
            InteractionEvents.OnHoldCompleted += HidePrompt;
            InteractionEvents.OnHoldCancelled += OnHoldCancelled;
        }
        
        void OnDisable()
        {
            InteractionEvents.OnInteractableDetected -= ShowPrompt;
            InteractionEvents.OnInteractableLost -= HidePrompt;
            InteractionEvents.OnHoldProgress -= UpdateProgress;
            InteractionEvents.OnHoldCompleted -= HidePrompt;
            InteractionEvents.OnHoldCancelled -= OnHoldCancelled;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void ShowPrompt(object interactableObj)
        {
            // Only show prompt if current player is local (not spectating)
            if (!SpectateManager.Instance?.IsCurrentPlayerLocal() ?? true)
            {
                return; // Spectating - disable prompt
            }
            
            currentInteractable = interactableObj as IInteractable;
            if (currentInteractable == null) return;
            
            // Set icon
            iconImage.sprite = currentInteractable.GetInteractIcon();
            
            // Set text
            promptText.text = currentInteractable.GetInteractText();
            
            // Show/hide progress bar based on interaction type
            bool isHoldInteraction = currentInteractable.GetInteractionType() == InteractionType.HoldToOpen;
            progressBarRoot.SetActive(isHoldInteraction);
            
            if (isHoldInteraction)
            {
                progressBarFill.fillAmount = 0f;
            }
            
            // Show panel
            promptPanel.SetActive(true);
            
            if (enableDebugLogs)
                Debug.Log($"[InteractionPromptUI] Showing prompt: {currentInteractable.GetInteractText()}");
        }
        
        private void HidePrompt()
        {
            currentInteractable = null;
            
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
            
            if (enableDebugLogs)
                Debug.Log("[InteractionPromptUI] Hidden");
        }
        
        private void UpdateProgress(float progress)
        {
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = progress;
            }
        }
        
        private void OnHoldCancelled(string reason)
        {
            // Reset progress bar
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = 0f;
            }
            
            if (enableDebugLogs)
                Debug.Log($"[InteractionPromptUI] Hold cancelled: {reason}");
        }
        
        #endregion
    }
}