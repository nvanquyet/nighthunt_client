using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.UI.Components
{
    /// <summary>
    /// Component for displaying item icons with stack count overlay.
    /// Handles icon loading and display.
    /// </summary>
    public class ItemIconUI : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Text stackCountText;
        [SerializeField] private GameObject stackCountBackground;
        
        [Header("Settings")]
        [SerializeField] private bool showStackCount = true;
        [SerializeField] private int minStackCountToShow = 2;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private ItemInstance currentItem;
        
        // === Public API ===
        
        /// <summary>
        /// Set item to display.
        /// </summary>
        public void SetItem(ItemInstance item)
        {
            currentItem = item;
            UpdateIcon();
            UpdateStackCount();
        }
        
        /// <summary>
        /// Clear item display.
        /// </summary>
        public void ClearItem()
        {
            currentItem = null;
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
            }
            
            if (stackCountText != null)
                stackCountText.gameObject.SetActive(false);
            
            if (stackCountBackground != null)
                stackCountBackground.SetActive(false);
        }
        
        /// <summary>
        /// Get current item.
        /// </summary>
        public ItemInstance GetItem() => currentItem;
        
        // === Visual Updates ===
        
        private void UpdateIcon()
        {
            if (iconImage == null)
                return;
            
            if (currentItem == null || currentItem.Definition == null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
                return;
            }
            
            if (currentItem.Definition.Icon != null)
            {
                iconImage.sprite = currentItem.Definition.Icon;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color = Color.clear;
                LogWarning($"Item {currentItem.Definition.ItemId} has no icon");
            }
        }
        
        private void UpdateStackCount()
        {
            if (!showStackCount || stackCountText == null)
                return;
            
            if (currentItem == null || currentItem.StackSize < minStackCountToShow)
            {
                stackCountText.gameObject.SetActive(false);
                if (stackCountBackground != null)
                    stackCountBackground.SetActive(false);
                return;
            }
            
            stackCountText.gameObject.SetActive(true);
            stackCountText.text = currentItem.StackSize.ToString();
            
            if (stackCountBackground != null)
                stackCountBackground.SetActive(true);
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            // Auto-find components if not assigned
            if (iconImage == null)
                iconImage = GetComponent<Image>();
            
            if (stackCountText == null)
                stackCountText = transform.Find("StackCount")?.GetComponent<Text>();
            
            if (stackCountBackground == null)
                stackCountBackground = transform.Find("StackCountBackground")?.gameObject;
        }
        
        // === Debug ===
        
        void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ItemIconUI] {message}");
        }
    }
}
