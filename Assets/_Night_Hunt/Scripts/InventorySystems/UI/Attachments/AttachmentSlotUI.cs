using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Slots;

namespace NightHunt.Inventory.UI.Attachments
{
    /// <summary>
    /// UI component for individual attachment slots.
    /// Displays empty/filled states, attachment icons, slot type indicators.
    /// Handles attachment interactions.
    /// </summary>
    public class AttachmentSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image attachmentIconImage;
        [SerializeField] private Image emptySlotIcon;
        [SerializeField] private Text slotTypeText;
        
        [Header("Colors")]
        [SerializeField] private Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private Color filledColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.5f, 0.3f);
        
        [Header("Settings")]
        [SerializeField] private AttachmentSlotType slotType;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private ItemInstance parentItem;
        private ItemInstance currentAttachment;
        private bool isHovered = false;
        private SlotLayoutConfig slotLayoutConfig;
        
        // Events
        public event System.Action<AttachmentSlotUI, ItemInstance> OnAttachmentHovered;
        public event System.Action<AttachmentSlotUI> OnAttachmentUnhovered;
        
        // === Public API ===
        
        /// <summary>
        /// Get slot type.
        /// </summary>
        public AttachmentSlotType GetSlotType() => slotType;
        
        /// <summary>
        /// Set slot type.
        /// </summary>
        public void SetSlotType(AttachmentSlotType type)
        {
            slotType = type;
            UpdateSlotTypeText();
        }
        
        /// <summary>
        /// Set parent item (item that can have attachments).
        /// </summary>
        public void SetParentItem(ItemInstance item)
        {
            parentItem = item;
            RefreshAttachment();
        }
        
        /// <summary>
        /// Get current attachment.
        /// </summary>
        public ItemInstance GetAttachment() => currentAttachment;
        
        /// <summary>
        /// Check if slot is empty.
        /// </summary>
        public bool IsEmpty() => currentAttachment == null;
        
        /// <summary>
        /// Set SlotLayoutConfig for empty icon display.
        /// </summary>
        public void SetSlotLayoutConfig(SlotLayoutConfig config)
        {
            slotLayoutConfig = config;
            UpdateEmptySlotIcon(); // Refresh icon when config is set
        }
        
        /// <summary>
        /// Refresh attachment display from parent item.
        /// </summary>
        public void RefreshAttachment()
        {
            if (parentItem == null)
            {
                currentAttachment = null;
                UpdateVisuals();
                return;
            }
            
            // Find attachment in this slot type
            currentAttachment = parentItem.GetAttachment(slotType);
            UpdateVisuals();
        }
        
        // === Unity Event Handlers ===
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisuals();
            
            if (currentAttachment != null)
            {
                OnAttachmentHovered?.Invoke(this, currentAttachment);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateVisuals();
            
            OnAttachmentUnhovered?.Invoke(this);
        }
        
        // === Visual Updates ===
        
        private void UpdateVisuals()
        {
            UpdateBackgroundColor();
            UpdateAttachmentIcon();
            UpdateEmptySlotIcon();
        }
        
        private void UpdateBackgroundColor()
        {
            if (backgroundImage == null)
                return;
            
            Color targetColor = currentAttachment != null ? filledColor : emptyColor;
            if (isHovered)
                targetColor = Color.Lerp(targetColor, hoverColor, 0.5f);
            
            backgroundImage.color = targetColor;
        }
        
        private void UpdateAttachmentIcon()
        {
            if (attachmentIconImage == null)
                return;
            
            if (currentAttachment == null || currentAttachment.Definition == null)
            {
                attachmentIconImage.gameObject.SetActive(false);
                return;
            }
            
            attachmentIconImage.gameObject.SetActive(true);
            
            if (currentAttachment.Definition.Icon != null)
            {
                attachmentIconImage.sprite = currentAttachment.Definition.Icon;
                attachmentIconImage.color = Color.white;
            }
            else
            {
                attachmentIconImage.color = Color.clear;
            }
        }
        
        private void UpdateEmptySlotIcon()
        {
            if (emptySlotIcon == null)
                return;
            
            bool shouldShow = currentAttachment == null;
            emptySlotIcon.gameObject.SetActive(shouldShow);
            
            if (shouldShow && slotLayoutConfig != null)
            {
                // Get empty icon from config for this attachment slot type
                Sprite iconSprite = slotLayoutConfig.GetAttachmentEmptyIcon(slotType);
                if (iconSprite != null)
                {
                    emptySlotIcon.sprite = iconSprite;
                    emptySlotIcon.color = Color.white;
                }
                else
                {
                    emptySlotIcon.color = Color.clear;
                }
            }
        }
        
        private void UpdateSlotTypeText()
        {
            if (slotTypeText == null)
                return;
            
            slotTypeText.text = slotType.ToString();
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            
            if (attachmentIconImage == null)
                attachmentIconImage = transform.Find("AttachmentIcon")?.GetComponent<Image>();
            
            if (emptySlotIcon == null)
                emptySlotIcon = transform.Find("EmptyIcon")?.GetComponent<Image>();
            
            if (slotTypeText == null)
                slotTypeText = GetComponentInChildren<Text>();
            
            UpdateSlotTypeText();
        }
        
        void Start()
        {
            UpdateVisuals();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[AttachmentSlotUI] {message}");
        }
    }
}
