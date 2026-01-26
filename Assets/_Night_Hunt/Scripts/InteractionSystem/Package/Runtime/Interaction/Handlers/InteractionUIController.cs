using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.InteractionSystem.Interaction.Handlers
{
    /// <summary>
    /// Controls interaction UI (prompts and progress bars).
    /// </summary>
    public class InteractionUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject interactionPromptPanel;
        [SerializeField] private Text interactionText;
        [SerializeField] private GameObject progressBarPanel;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private Text progressText;

        private void Awake()
        {
            if (progressBarPanel != null)
            {
                progressBarPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Show interaction prompt.
        /// </summary>
        public void ShowPrompt(string text)
        {
            if (interactionPromptPanel != null)
            {
                interactionPromptPanel.SetActive(true);
            }

            if (interactionText != null)
            {
                interactionText.text = text;
            }
        }

        /// <summary>
        /// Hide interaction prompt.
        /// </summary>
        public void HidePrompt()
        {
            if (interactionPromptPanel != null)
            {
                interactionPromptPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Show progress bar.
        /// </summary>
        public void ShowProgress()
        {
            if (progressBarPanel != null)
            {
                progressBarPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Hide progress bar.
        /// </summary>
        public void HideProgress()
        {
            if (progressBarPanel != null)
            {
                progressBarPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Update progress bar (0-1).
        /// </summary>
        public void UpdateProgress(float progress)
        {
            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = progress;
            }

            if (progressText != null)
            {
                progressText.text = $"{progress * 100f:F0}%";
            }
        }
    }
}
