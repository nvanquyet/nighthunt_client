using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Systems;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.UI.Data
{
    /// <summary>
    /// Data provider for UI components.
    /// Provides inventory/equipment/weapon/quickSlot/attachment data via SpectateManager.
    /// Acts as a bridge between UI layer and inventory systems.
    /// </summary>
    public class InventoryUIDataProvider : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // === Public API - Inventory Data ===
        
        /// <summary>
        /// Get all items in current player's inventory.
        /// </summary>
        public List<ItemInstance> GetInventoryItems()
        {
            var inventory = SpectateManager.Instance?.GetCurrentInventory();
            if (inventory == null)
                return new List<ItemInstance>();
            
            return inventory.GetContainerData().GetAllItems();
        }
        
        /// <summary>
        /// Get item at specific inventory slot.
        /// </summary>
        public ItemInstance GetInventoryItemAtSlot(int slotIndex)
        {
            var spectateManager = SpectateManager.Instance;
            Log($"[InventoryUIDataProvider] GetInventoryItemAtSlot: slotIndex={slotIndex}, SpectateManager={spectateManager}");
            
            if (spectateManager == null)
            {
                Log($"[InventoryUIDataProvider] SpectateManager is null!");
                return null;
            }
            
            var inventory = spectateManager.GetCurrentInventory();
            Log($"[InventoryUIDataProvider] GetCurrentInventory: {inventory}, slotIndex={slotIndex}");
            
            if (inventory == null)
            {
                Log($"[InventoryUIDataProvider] GetCurrentInventory returned null!");
                return null;
            }
            
            var item = inventory.GetItemAtSlot(slotIndex);
            Log($"[InventoryUIDataProvider] GetItemAtSlot({slotIndex}): {item?.Definition?.DisplayName ?? "null"}");
            return item;
        }
        
        /// <summary>
        /// Get inventory slot count.
        /// </summary>
        public int GetInventorySlotCount()
        {
            var inventory = SpectateManager.Instance?.GetCurrentInventory();
            Log($"Current Inventory Valid {inventory != null} : {inventory}");
            if (inventory == null)
                return 0;
            
            return inventory.GetSlotCount();
        }
        
        /// <summary>
        /// Get current inventory weight.
        /// </summary>
        public float GetCurrentWeight()
        {
            var inventory = SpectateManager.Instance?.GetCurrentInventory();
            if (inventory == null)
                return 0f;
            
            return inventory.GetCurrentWeight();
        }
        
        /// <summary>
        /// Get max inventory weight.
        /// </summary>
        public float GetMaxWeight()
        {
            var inventory = SpectateManager.Instance?.GetCurrentInventory();
            if (inventory == null)
                return 0f;
            
            return inventory.GetMaxWeight();
        }
        
        // === Public API - Equipment Data ===
        
        /// <summary>
        /// Get equipped item in equipment slot.
        /// </summary>
        public ItemInstance GetEquippedItem(EquipmentSlotType slotType)
        {
            var equipment = SpectateManager.Instance?.GetCurrentEquipment();
            if (equipment == null)
                return null;
            
            return equipment.GetEquippedItem(slotType);
        }
        
        /// <summary>
        /// Get all equipped items.
        /// </summary>
        public Dictionary<EquipmentSlotType, ItemInstance> GetAllEquippedItems()
        {
            var equipment = SpectateManager.Instance?.GetCurrentEquipment();
            if (equipment == null)
                return new Dictionary<EquipmentSlotType, ItemInstance>();
            
            var result = new Dictionary<EquipmentSlotType, ItemInstance>();
            var slotTypes = equipment.GetAllSlotTypes();
            
            foreach (var slotType in slotTypes)
            {
                var item = equipment.GetEquippedItem(slotType);
                if (item != null)
                {
                    result[slotType] = item;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if equipment slot is equipped.
        /// </summary>
        public bool IsEquipmentSlotEquipped(EquipmentSlotType slotType)
        {
            var equipment = SpectateManager.Instance?.GetCurrentEquipment();
            if (equipment == null)
                return false;
            
            return equipment.IsSlotEquipped(slotType);
        }
        
        // === Public API - Weapon Data ===
        
        /// <summary>
        /// Get equipped weapon in weapon slot.
        /// </summary>
        public ItemInstance GetEquippedWeapon(WeaponSlotType slotType)
        {
            var weapons = SpectateManager.Instance?.GetCurrentWeapons();
            if (weapons == null)
                return null;
            
            return weapons.GetEquippedWeapon(slotType);
        }
        
        /// <summary>
        /// Get active weapon.
        /// </summary>
        public ItemInstance GetActiveWeapon()
        {
            var weapons = SpectateManager.Instance?.GetCurrentWeapons();
            if (weapons == null)
                return null;
            
            return weapons.GetActiveWeapon();
        }
        
        /// <summary>
        /// Get active weapon slot type.
        /// </summary>
        public WeaponSlotType GetActiveWeaponSlot()
        {
            var weapons = SpectateManager.Instance?.GetCurrentWeapons();
            if (weapons == null)
                return WeaponSlotType.Primary;
            
            return weapons.GetActiveWeaponSlot();
        }
        
        /// <summary>
        /// Check if weapon slot is equipped.
        /// </summary>
        public bool IsWeaponSlotEquipped(WeaponSlotType slotType)
        {
            var weapons = SpectateManager.Instance?.GetCurrentWeapons();
            if (weapons == null)
                return false;
            
            return weapons.IsSlotEquipped(slotType);
        }
        
        // === Public API - Quick Slot Data ===
        
        /// <summary>
        /// Get item in quick slot.
        /// </summary>
        public ItemInstance GetQuickSlotItem(int quickSlotIndex)
        {
            var quickSlots = SpectateManager.Instance?.GetCurrentQuickSlots();
            if (quickSlots == null)
                return null;
            
            return quickSlots.GetQuickSlotItem(quickSlotIndex);
        }
        
        /// <summary>
        /// Get all quick slot items.
        /// </summary>
        public Dictionary<int, ItemInstance> GetAllQuickSlotItems()
        {
            var quickSlots = SpectateManager.Instance?.GetCurrentQuickSlots();
            if (quickSlots == null)
                return new Dictionary<int, ItemInstance>();
            
            return quickSlots.GetAllQuickSlots();
        }
        
        /// <summary>
        /// Get quick slot count.
        /// </summary>
        public int GetQuickSlotCount()
        {
            var quickSlots = SpectateManager.Instance?.GetCurrentQuickSlots();
            if (quickSlots == null)
                return 0;
            
            return quickSlots.GetQuickSlotCount();
        }
        
        /// <summary>
        /// Check if quick slot has item.
        /// </summary>
        public bool IsQuickSlotAssigned(int quickSlotIndex)
        {
            var quickSlots = SpectateManager.Instance?.GetCurrentQuickSlots();
            if (quickSlots == null)
                return false;
            
            return quickSlots.IsSlotAssigned(quickSlotIndex);
        }
        
        // === Public API - Attachment Data ===
        
        /// <summary>
        /// Get all attachments on an item.
        /// </summary>
        public ItemInstance[] GetAttachments(ItemInstance parentItem)
        {
            var attachments = SpectateManager.Instance?.GetCurrentAttachments();
            if (attachments == null || parentItem == null)
                return new ItemInstance[0];
            
            return attachments.GetAttachments(parentItem);
        }
        
        /// <summary>
        /// Get attachment in specific slot on an item.
        /// </summary>
        public ItemInstance GetAttachment(ItemInstance parentItem, AttachmentSlotType slotType)
        {
            var attachments = SpectateManager.Instance?.GetCurrentAttachments();
            if (attachments == null || parentItem == null)
                return null;
            
            return attachments.GetAttachment(parentItem, slotType);
        }
        
        /// <summary>
        /// Check if item has attachment in slot.
        /// </summary>
        public bool HasAttachment(ItemInstance parentItem, AttachmentSlotType slotType)
        {
            var attachments = SpectateManager.Instance?.GetCurrentAttachments();
            if (attachments == null || parentItem == null)
                return false;
            
            return attachments.HasAttachment(parentItem, slotType);
        }
        
        /// <summary>
        /// Get available attachment slots for an item.
        /// </summary>
        public AttachmentSlotType[] GetAvailableAttachmentSlots(ItemInstance item)
        {
            if (item == null || item.Definition == null)
                return new AttachmentSlotType[0];
            
            return item.Definition.AttachmentSlots ?? new AttachmentSlotType[0];
        }
        
        // === Public API - Network Sync References ===
        
        /// <summary>
        /// Get InventoryNetworkSync for current player (for calling Public API).
        /// </summary>
        public Network.InventoryNetworkSync GetInventoryNetworkSync()
        {
            var player = SpectateManager.Instance?.GetCurrentPlayer();
            if (player == null)
                return null;
            
            return player.GetComponent<Network.InventoryNetworkSync>();
        }
        
        /// <summary>
        /// Get EquipmentNetworkSync for current player.
        /// </summary>
        public Network.EquipmentNetworkSync GetEquipmentNetworkSync()
        {
            var player = SpectateManager.Instance?.GetCurrentPlayer();
            if (player == null)
                return null;
            
            return player.GetComponent<Network.EquipmentNetworkSync>();
        }
        
        /// <summary>
        /// Get WeaponNetworkSync for current player.
        /// </summary>
        public Network.WeaponNetworkSync GetWeaponNetworkSync()
        {
            var player = SpectateManager.Instance?.GetCurrentPlayer();
            if (player == null)
                return null;
            
            return player.GetComponent<Network.WeaponNetworkSync>();
        }
        
        /// <summary>
        /// Get QuickSlotNetworkSync for current player.
        /// </summary>
        public Network.QuickSlotNetworkSync GetQuickSlotNetworkSync()
        {
            var player = SpectateManager.Instance?.GetCurrentPlayer();
            if (player == null)
                return null;
            
            return player.GetComponent<Network.QuickSlotNetworkSync>();
        }
        
        /// <summary>
        /// Get AttachmentNetworkSync for current player.
        /// </summary>
        public Network.AttachmentNetworkSync GetAttachmentNetworkSync()
        {
            var player = SpectateManager.Instance?.GetCurrentPlayer();
            if (player == null)
                return null;
            
            return player.GetComponent<Network.AttachmentNetworkSync>();
        }
        
        // === Public API - Utility ===
        
        /// <summary>
        /// Check if current player is local player (can interact).
        /// </summary>
        public bool CanInteract()
        {
            return SpectateManager.Instance?.IsCurrentPlayerLocal() ?? false;
        }
        
        /// <summary>
        /// Check if currently spectating.
        /// </summary>
        public bool IsSpectating()
        {
            return SpectateManager.Instance?.IsSpectating() ?? false;
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIDataProvider] {message}");
        }
    }
}
