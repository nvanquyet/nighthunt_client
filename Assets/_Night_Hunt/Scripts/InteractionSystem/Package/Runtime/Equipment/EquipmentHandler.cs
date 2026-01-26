using UnityEngine;
using FishNet.Object;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Items.Data;
using NightHunt.InteractionSystem.Items.Attachments;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Equipment
{
    /// <summary>
    /// Handles equipment operations (equip/unequip) and attachment management.
    /// Works with both player equipment and attachments on equipment.
    /// </summary>
    [RequireComponent(typeof(EquipmentManager))]
    [RequireComponent(typeof(InventoryComponentBase))]
    public class EquipmentHandler : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private EquipmentManager equipmentManager;
        [SerializeField] private InventoryComponentBase inventory;

        private void Awake()
        {
            if (equipmentManager == null)
                equipmentManager = GetComponent<EquipmentManager>();
            
            if (inventory == null)
                inventory = GetComponentInParent<InventoryComponentBase>();
        }

        /// <summary>
        /// Equip an item from inventory to equipment slot.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void EquipItemFromInventory(string itemId, EquipmentSlot slot)
        {
            if (inventory == null || equipmentManager == null)
                return;

            // Find item in inventory
            var itemInstance = inventory.FindItem(itemId);
            if (!itemInstance.HasValue)
            {
                Debug.LogWarning($"[EquipmentHandler] Item {itemId} not found in inventory");
                return;
            }

            // Load equipment data
            EquipmentDataBase equipmentData = LoadEquipmentData(itemId);
            if (equipmentData == null)
            {
                Debug.LogWarning($"[EquipmentHandler] Equipment data not found for {itemId}");
                return;
            }

            // Check if slot matches
            if (equipmentData.EquipmentSlot != slot)
            {
                Debug.LogWarning($"[EquipmentHandler] Item {itemId} cannot be equipped to slot {slot}");
                return;
            }

            // Remove from inventory
            if (inventory.RemoveItem(itemId, 1))
            {
                // Equip item
                equipmentManager.EquipItem(slot, itemInstance.Value, equipmentData);

            // Initialize attachment manager for this equipment
            InitializeAttachmentManager(slot, equipmentData);
            
            // Set equipment slot reference on AttachmentManager
            GameObject equipmentVisual = GetEquipmentVisual(slot);
            if (equipmentVisual != null)
            {
                AttachmentManager attachmentManager = equipmentVisual.GetComponent<AttachmentManager>();
                if (attachmentManager != null)
                {
                    attachmentManager.SetEquipmentSlot(slot);
                }
            }
            }
        }

        /// <summary>
        /// Unequip an item from equipment slot back to inventory.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void UnequipItemToInventory(EquipmentSlot slot)
        {
            if (equipmentManager == null || inventory == null)
                return;

            var equippedItem = equipmentManager.GetEquippedItem(slot);
            if (!equippedItem.HasValue)
                return;

            // Try to add back to inventory
            if (inventory.CanAddItem(equippedItem.Value))
            {
                if (inventory.AddItem(equippedItem.Value))
                {
                    // Unequip item
                    equipmentManager.UnequipItem(slot);

                    // Cleanup attachment manager
                    CleanupAttachmentManager(slot);
                }
            }
            else
            {
                Debug.LogWarning("[EquipmentHandler] Cannot unequip: inventory full");
            }
        }

        /// <summary>
        /// Attach an attachment to equipment.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void AttachToEquipment(EquipmentSlot equipmentSlot, Core.Interfaces.AttachmentSlotType attachmentSlot, string attachmentItemId)
        {
            if (equipmentManager == null)
                return;

            // Get equipped item
            var equippedItem = equipmentManager.GetEquippedItem(equipmentSlot);
            if (!equippedItem.HasValue)
            {
                Debug.LogWarning($"[EquipmentHandler] No item equipped in slot {equipmentSlot}");
                return;
            }

            // Find attachment manager for this equipment
            AttachmentManager attachmentManager = GetAttachmentManager(equipmentSlot);
            if (attachmentManager == null)
            {
                Debug.LogWarning($"[EquipmentHandler] AttachmentManager not found for slot {equipmentSlot}");
                return;
            }

            // Load attachment data
            AttachmentData attachmentData = LoadAttachmentData(attachmentItemId);
            if (attachmentData == null)
            {
                Debug.LogWarning($"[EquipmentHandler] Attachment data not found for {attachmentItemId}");
                return;
            }

            // Check if attachment is in inventory
            if (inventory != null)
            {
                var attachmentInstance = inventory.FindItem(attachmentItemId);
                if (!attachmentInstance.HasValue)
                {
                    Debug.LogWarning($"[EquipmentHandler] Attachment {attachmentItemId} not in inventory");
                    return;
                }

                // Remove attachment from inventory
                if (inventory.RemoveItem(attachmentItemId, 1))
                {
                    // Attach to equipment
                    if (attachmentManager.AttachAttachment(attachmentSlot, attachmentData))
                    {
                        Debug.Log($"[EquipmentHandler] Attached {attachmentItemId} to {equipmentSlot} slot {attachmentSlot}");
                        // Event will be invoked by AttachmentManager if we add events there
                    }
                    else
                    {
                        // Failed to attach - return to inventory
                        inventory.AddItem(attachmentInstance.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Detach an attachment from equipment back to inventory.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void DetachFromEquipment(EquipmentSlot equipmentSlot, Core.Interfaces.AttachmentSlotType attachmentSlot)
        {
            if (equipmentManager == null || inventory == null)
                return;

            // Get attachment manager
            AttachmentManager attachmentManager = GetAttachmentManager(equipmentSlot);
            if (attachmentManager == null)
                return;

            // Get attached attachment
            AttachmentData attachmentData = attachmentManager.GetAttachment(attachmentSlot);
            if (attachmentData == null)
                return;

            // Detach
            if (attachmentManager.DetachAttachment(attachmentSlot))
            {
                // Create item instance and add back to inventory
                ItemInstance attachmentInstance = attachmentData.CreateInstance(1);
                if (inventory.CanAddItem(attachmentInstance))
                {
                    inventory.AddItem(attachmentInstance);
                }
                else
                {
                    Debug.LogWarning("[EquipmentHandler] Cannot return attachment to inventory: full");
                    // Could drop on ground or handle differently
                }
            }
        }

        /// <summary>
        /// Initialize attachment manager for equipped item.
        /// </summary>
        private void InitializeAttachmentManager(EquipmentSlot slot, EquipmentDataBase equipmentData)
        {
            // Find or create attachment manager for this equipment
            // AttachmentManager should be on the equipment visual GameObject
            GameObject equipmentVisual = GetEquipmentVisual(slot);
            if (equipmentVisual != null)
            {
                AttachmentManager attachmentManager = equipmentVisual.GetComponent<AttachmentManager>();
                if (attachmentManager == null)
                {
                    attachmentManager = equipmentVisual.AddComponent<AttachmentManager>();
                    // Set equipment data using reflection
                    var equipmentDataField = typeof(AttachmentManager).GetField("equipmentData", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (equipmentDataField != null)
                        equipmentDataField.SetValue(attachmentManager, equipmentData);
                }
            }
        }

        /// <summary>
        /// Cleanup attachment manager when unequipping.
        /// </summary>
        private void CleanupAttachmentManager(EquipmentSlot slot)
        {
            GameObject equipmentVisual = GetEquipmentVisual(slot);
            if (equipmentVisual != null)
            {
                AttachmentManager attachmentManager = equipmentVisual.GetComponent<AttachmentManager>();
                if (attachmentManager != null)
                {
                    // Detach all attachments first
                    var allAttachments = attachmentManager.GetAllAttachments();
                    foreach (var kvp in allAttachments)
                    {
                        DetachFromEquipment(slot, kvp.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Get equipment visual GameObject for a slot.
        /// </summary>
        private GameObject GetEquipmentVisual(EquipmentSlot slot)
        {
            // This should be managed by EquipmentVisualController
            EquipmentVisualController visualController = GetComponent<EquipmentVisualController>();
            if (visualController != null)
            {
                // Use reflection or add public method to get visual
                return visualController.GetEquipmentVisual(slot);
            }
            return null;
        }

        /// <summary>
        /// Get attachment manager for equipment slot.
        /// </summary>
        private AttachmentManager GetAttachmentManager(EquipmentSlot slot)
        {
            GameObject equipmentVisual = GetEquipmentVisual(slot);
            if (equipmentVisual != null)
            {
                return equipmentVisual.GetComponent<AttachmentManager>();
            }
            return null;
        }

        /// <summary>
        /// Load equipment data (placeholder - should use proper data loading system).
        /// </summary>
        private EquipmentDataBase LoadEquipmentData(string itemId)
        {
            // TODO: Implement proper data loading from Resources or database
            // For now, return null - this should be implemented by game-specific code
            return null;
        }

        /// <summary>
        /// Load attachment data (placeholder - should use proper data loading system).
        /// </summary>
        private AttachmentData LoadAttachmentData(string itemId)
        {
            // TODO: Implement proper data loading from Resources or database
            // For now, return null - this should be implemented by game-specific code
            return null;
        }
    }
}
