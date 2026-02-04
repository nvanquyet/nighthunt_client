using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Attachment;
using NightHunt.Inventory.Domain.Weapon;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Attachment panel UI showing available attachment slots for active weapon.
    /// Dynamically shows/hides slots based on weapon's allowed attachments.
    /// </summary>
    public class AttachmentPanelUI : MonoBehaviour
    {
        [Header("Attachment Slots")]
        [SerializeField] private AttachmentSlotUI scopeSlot;
        [SerializeField] private AttachmentSlotUI gripSlot;
        [SerializeField] private AttachmentSlotUI muzzleSlot;
        [SerializeField] private AttachmentSlotUI magazineSlot;
        [SerializeField] private AttachmentSlotUI flashlightSlot;
        [SerializeField] private AttachmentSlotUI nvgSlot;
        
        [Header("References")]
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private AttachmentManager attachmentManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Dictionary<AttachmentSlotType, AttachmentSlotUI> slotMap;
        private ItemInstance currentWeapon;
        
        #region Lifecycle
        
        void Awake()
        {
            slotMap = new Dictionary<AttachmentSlotType, AttachmentSlotUI>
            {
                { AttachmentSlotType.Scope, scopeSlot },
                { AttachmentSlotType.Grip, gripSlot },
                { AttachmentSlotType.Muzzle, muzzleSlot },
                { AttachmentSlotType.Magazine, magazineSlot },
                { AttachmentSlotType.Flashlight, flashlightSlot },
                { AttachmentSlotType.NVG, nvgSlot }
            };
            
            foreach (var kvp in slotMap)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Initialize(kvp.Key, this);
                }
            }
        }
        
        void OnEnable()
        {
            WeaponEvents.OnWeaponSwitched += HandleWeaponSwitched;
            WeaponEvents.OnWeaponEquipped += HandleWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped += HandleWeaponUnequipped;
            AttachmentEvents.OnAttachmentAdded += HandleAttachmentAdded;
            AttachmentEvents.OnAttachmentRemoved += HandleAttachmentRemoved;
        }
        
        void OnDisable()
        {
            WeaponEvents.OnWeaponSwitched -= HandleWeaponSwitched;
            WeaponEvents.OnWeaponEquipped -= HandleWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped -= HandleWeaponUnequipped;
            AttachmentEvents.OnAttachmentAdded -= HandleAttachmentAdded;
            AttachmentEvents.OnAttachmentRemoved -= HandleAttachmentRemoved;
        }
        
        void Start()
        {
            RefreshForActiveWeapon();
        }
        
        #endregion
        
        #region Public API
        
        public void OnAttachmentDropped(ItemInstance attachment, AttachmentSlotType targetSlot)
        {
            if (attachmentManager == null || currentWeapon == null)
            {
                Debug.LogError("[AttachmentPanelUI] Manager not assigned or no active weapon!");
                return;
            }
            
            // Validate attachment type
            if (attachment.Definition.ItemType != ItemType.Attachment)
            {
                UIEvents.InvokeShowError("Only attachments can be placed here");
                return;
            }
            
            // Validate slot compatibility
            if (attachment.Definition.AttachmentType != targetSlot)
            {
                UIEvents.InvokeShowError($"Wrong attachment type for this slot");
                return;
            }
            
            // Validate weapon accepts this attachment type
            if (!System.Array.Exists(currentWeapon.Definition.AttachmentSlots, 
                slot => slot == targetSlot))
            {
                UIEvents.InvokeShowError($"This weapon doesn't support {targetSlot} attachments");
                return;
            }
            
            // Try attach
            var result = attachmentManager.TryAttach(attachment, currentWeapon);
            
            if (result.IsSuccess)
            {
                // Remove from inventory
                InventoryEvents.InvokeRequestRemoveItem(attachment.InstanceId);
                
                if (enableDebugLogs)
                    Debug.Log($"[AttachmentPanelUI] Attached {attachment.Definition.ItemId} to weapon");
            }
            else
            {
                UIEvents.InvokeShowError(result.FailReason);
            }
        }
        
        public void OnDetachRequested(AttachmentSlotType slotType)
        {
            if (attachmentManager == null || currentWeapon == null) return;
            
            var attachment = attachmentManager.GetAttachmentInSlot(currentWeapon, slotType);
            if (attachment != null)
            {
                if (attachmentManager.TryDetach(attachment, currentWeapon))
                {
                    // Add back to inventory
                    InventoryEvents.InvokeRequestAddItem(attachment);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[AttachmentPanelUI] Detached {attachment.Definition.ItemId}");
                }
            }
        }
        
        public void RefreshForActiveWeapon()
        {
            if (weaponManager == null)
            {
                currentWeapon = null;
                HideAllSlots();
                return;
            }
            
            currentWeapon = weaponManager.GetActiveWeapon();
            
            if (currentWeapon == null)
            {
                HideAllSlots();
                return;
            }
            
            // Show only slots that weapon supports
            foreach (var kvp in slotMap)
            {
                bool isSupported = System.Array.Exists(currentWeapon.Definition.AttachmentSlots,
                    slot => slot == kvp.Key);
                
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(isSupported);
                    
                    if (isSupported)
                    {
                        // Update attachment display
                        var attachment = attachmentManager?.GetAttachmentInSlot(currentWeapon, kvp.Key);
                        kvp.Value.SetAttachment(attachment);
                    }
                }
            }
        }
        
        private void HideAllSlots()
        {
            foreach (var kvp in slotMap)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(false);
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleWeaponSwitched(WeaponSlotType newActiveSlot)
        {
            RefreshForActiveWeapon();
        }
        
        private void HandleWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            // Refresh if this is the active weapon
            if (weaponManager != null && weaponManager.GetActiveSlot() == slotType)
            {
                RefreshForActiveWeapon();
            }
        }
        
        private void HandleWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (currentWeapon == weapon)
            {
                RefreshForActiveWeapon();
            }
        }
        
        private void HandleAttachmentAdded(ItemInstance attachment, ItemInstance parentWeapon)
        {
            if (parentWeapon == currentWeapon)
            {
                var slotType = attachment.Definition.AttachmentType;
                if (slotMap.TryGetValue(slotType, out var slot))
                {
                    slot.SetAttachment(attachment);
                }
            }
        }
        
        private void HandleAttachmentRemoved(ItemInstance attachment, ItemInstance parentWeapon)
        {
            if (parentWeapon == currentWeapon)
            {
                var slotType = attachment.Definition.AttachmentType;
                if (slotMap.TryGetValue(slotType, out var slot))
                {
                    slot.SetAttachment(null);
                }
            }
        }
        
        #endregion
    }
}