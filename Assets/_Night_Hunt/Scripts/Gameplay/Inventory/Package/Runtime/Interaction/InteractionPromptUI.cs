using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// UI prompt for interactions (icon + text + progress bar).
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private GameObject progressBarRoot;
        
        public void Show(Sprite icon, string text, InteractionType type)
        {
            promptPanel.SetActive(true);
            iconImage.sprite = icon;
            promptText.text = text;
            progressBarRoot.SetActive(type == InteractionType.HoldToOpen);
            
            if (type == InteractionType.HoldToOpen)
            {
                progressBarFill.fillAmount = 0f;
            }
        }
        
        public void UpdateProgress(float progress)
        {
            progressBarFill.fillAmount = progress;
        }
        
        public void Hide()
        {
            promptPanel.SetActive(false);
        }
    }
}
