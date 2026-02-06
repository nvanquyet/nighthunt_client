using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain.Inventory;
using NightHunt.Inventory.Domain.QuickSlot;
using NightHunt.Inventory.Domain.Equipment;
using NightHunt.Inventory.Domain.Weapon;
using NightHunt.Inventory.Domain.Attachment;

namespace NightHunt.Inventory.UI.Controllers
{
    /// <summary>
    /// Centralized drag-drop logic processor.
    /// Handles all location types and calls appropriate managers.
    /// Network sync is handled by managers via ServerRpc.
    /// </summary>
    public class DragDropHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private QuickSlotManager quickSlotManager;
        [SerializeField] private EquipmentManager equipmentManager;
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField] private AttachmentManager attachmentManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        #region Lifecycle
        
        void OnEnable()
        {
            DragDropEvents.OnDrop += ProcessDrop;
        }
        
        void OnDisable()
        {
            DragDropEvents.OnDrop -= ProcessDrop;
        }
        
        #endregion
        
        #region Drop Processing
        
        private void ProcessDrop(DragContext context)
        {
            if (context.ItemInstance == null)
            {
                InventoryLogger.LogWarning("DragDropHandler", "Drop context has no item", enableDebugLogs);
                return;
            }
            
            // Block all drops if not local player (spectating)
            if (!SpectateManager.Instance?.IsCurrentPlayerLocal() ?? true)
            {
                InventoryLogger.Log("DragDropHandler", "Blocked drop - not local player (spectating)", enableDebugLogs);
                return;
            }
            
            InventoryLogger.Log("DragDropHandler", 
                $"Processing drop: {context.SourceLocation}[{context.SourceIndex}] -> {context.TargetLocation}[{context.TargetIndex}]", 
                enableDebugLogs);
            
            // Route to appropriate handler based on target location
            switch (context.TargetLocation)
            {
                case SlotLocationType.Inventory:
                    HandleInventoryToInventory(context);
                    break;
                    
                case SlotLocationType.QuickSlot:
                    HandleInventoryToQuickSlot(context);
                    break;
                    
                case SlotLocationType.Equipment:
                    HandleInventoryToEquipment(context);
                    break;
                    
                case SlotLocationType.Weapon:
                    HandleInventoryToWeapon(context);
                    break;
                    
                case SlotLocationType.Attachment:
                    HandleInventoryToAttachment(context);
                    break;
                    
                case SlotLocationType.Container:
                    HandleInventoryToContainer(context);
                    break;
                    
                case SlotLocationType.Trash:
                    HandleInventoryToTrash(context);
                    break;
                    
                default:
                    InventoryLogger.LogWarning("DragDropHandler", $"Unhandled target location: {context.TargetLocation}", enableDebugLogs);
                    break;
            }
        }
        
        #endregion
        
        #region Handler Methods
        
        private void HandleInventoryToInventory(DragContext context)
        {
            if (inventoryManager == null)
            {
                InventoryLogger.LogError("DragDropHandler", "InventoryManager not assigned!");
                return;
            }
            
            // Swap items between inventory slots
            var sourceItem = inventoryManager.GetInventoryData().GetItemAtIndex(context.SourceIndex);
            var targetItem = inventoryManager.GetInventoryData().GetItemAtIndex(context.TargetIndex);
            
            if (sourceItem != null)
            {
                // Remove from source
                inventoryManager.RemoveItemAtIndex(context.SourceIndex);
                
                // Add to target (swaps if target has item)
                if (targetItem != null)
                {
                    inventoryManager.RemoveItemAtIndex(context.TargetIndex);
                    inventoryManager.TryAddItemAtIndex(sourceItem, context.TargetIndex);
                    inventoryManager.TryAddItemAtIndex(targetItem, context.SourceIndex);
                }
                else
                {
                    inventoryManager.TryAddItemAtIndex(sourceItem, context.TargetIndex);
                }
            }
        }
        
        private void HandleInventoryToQuickSlot(DragContext context)
        {
            if (quickSlotManager == null)
            {
                InventoryLogger.LogError("DragDropHandler", "QuickSlotManager not assigned!");
                return;
            }
            
            // Validate item type
            if (context.ItemInstance.Definition.ItemType != ItemType.Consumable &&
                context.ItemInstance.Definition.ItemType != ItemType.Throwable)
            {
                UIEvents.InvokeShowError("Only consumables and throwables can be assigned to quick slots");
                return;
            }
            
            // Get old item for swap
            var oldItem = quickSlotManager.GetItem(context.TargetIndex);
            
            // Try add item to quick slot
            bool success = quickSlotManager.TryAddItem(context.ItemInstance, context.TargetIndex);
            
            if (success)
            {
                // Remove from inventory
                if (inventoryManager != null)
                {
                    inventoryManager.RemoveItemAtIndex(context.SourceIndex);
                }
                
                // If swapped, add old item back to inventory
                if (oldItem != null && inventoryManager != null)
                {
                    inventoryManager.TryAddItem(oldItem);
                }
            }
            else
            {
                UIEvents.InvokeShowError("Cannot assign item to quick slot");
            }
        }
        
        private void HandleQuickSlotToInventory(DragContext context)
        {
            if (quickSlotManager == null || inventoryManager == null)
            {
                InventoryLogger.LogError("DragDropHandler", "QuickSlotManager or InventoryManager not assigned!");
                return;
            }
            
            // Get item from quick slot
            var item = quickSlotManager.GetItem(context.SourceIndex);
            if (item == null) return;
            
            // Remove from quick slot
            quickSlotManager.RemoveItem(context.SourceIndex);
            
            // Add to inventory
            inventoryManager.TryAddItemAtIndex(item, context.TargetIndex);
        }
        
        private void HandleInventoryToEquipment(DragContext context)
        {
            if (equipmentManager == null)
            {
                InventoryLogger.LogError("DragDropHandler", "EquipmentManager not assigned!");
                return;
            }
            
            // Equipment panel handles this via OnItemDroppedOnSlot
            // This is just a fallback
            InventoryLogger.Log("DragDropHandler", "Equipment drop handled by EquipmentPanelUI", enableDebugLogs);
        }
        
        private void HandleInventoryToWeapon(DragContext context)
        {
            if (weaponManager == null)
            {
                InventoryLogger.LogError("DragDropHandler", "WeaponManager not assigned!");
                return;
            }
            
            // Weapon panel handles this via OnItemDroppedOnSlot
            InventoryLogger.Log("DragDropHandler", "Weapon drop handled by WeaponPanelUI", enableDebugLogs);
        }
        
        private void HandleInventoryToAttachment(DragContext context)
        {
            if (attachmentManager == null)
            {
                InventoryLogger.LogError("DragDropHandler", "AttachmentManager not assigned!");
                return;
            }
            
            // Attachment panel handles this via OnAttachmentDropped
            InventoryLogger.Log("DragDropHandler", "Attachment drop handled by AttachmentPanelUI", enableDebugLogs);
        }
        
        private void HandleInventoryToContainer(DragContext context)
        {
            // Container logic handled by ContainerPanelUI
            InventoryLogger.Log("DragDropHandler", "Container drop handled by ContainerPanelUI", enableDebugLogs);
        }
        
        private void HandleInventoryToTrash(DragContext context)
        {
            // Trash logic handled by TrashSlotUI
            InventoryLogger.Log("DragDropHandler", "Trash drop handled by TrashSlotUI", enableDebugLogs);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Sets the InventoryManager reference.
        /// </summary>
        public void SetInventoryManager(InventoryManager manager)
        {
            inventoryManager = manager;
        }
        
        /// <summary>
        /// Sets the QuickSlotManager reference.
        /// </summary>
        public void SetQuickSlotManager(QuickSlotManager manager)
        {
            quickSlotManager = manager;
        }
        
        /// <summary>
        /// Sets the EquipmentManager reference.
        /// </summary>
        public void SetEquipmentManager(EquipmentManager manager)
        {
            equipmentManager = manager;
        }
        
        /// <summary>
        /// Sets the WeaponManager reference.
        /// </summary>
        public void SetWeaponManager(WeaponManager manager)
        {
            weaponManager = manager;
        }
        
        /// <summary>
        /// Sets the AttachmentManager reference.
        /// </summary>
        public void SetAttachmentManager(AttachmentManager manager)
        {
            attachmentManager = manager;
        }
        
        #endregion
    }
}
