using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.Stats;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using NightHunt.Inventory.World;
using UnityEngine.Serialization;

namespace NightHunt.Inventory.Systems
{
    /// <summary>
    /// Main orchestrator for all inventory systems.
    /// Provides unified public API and manages inter-system communication.
    /// This is the primary interface for gameplay code to interact with inventory.
    /// </summary>
    public class PlayerInventoryController : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private InventoryConfig config;

        [SerializeField] private SlotLayoutConfig slotLayout;

        [Header("System References (Auto-found or Manual)")] [SerializeField]
        private InventorySystem inventorySystem;

        [SerializeField] private EquipmentSystem equipmentSystem;
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private QuickSlotSystem quickSlotSystem;
        [SerializeField] private AttachmentSystem attachmentSystem;

        [FormerlySerializedAs("characterStats")] [Header("Character References")] [SerializeField]
        private PlayerStats playerStats;

        [Header("World Interaction")] [SerializeField]
        private Transform dropSpawnPoint; // Where dropped items spawn

        [SerializeField] private GameObject worldItemDropPrefab; // Prefab for dropped items

        [Header("Network")] [SerializeField] private NetworkObject networkObject;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = true;

        // === Properties ===

        public InventorySystem Inventory => inventorySystem;
        public EquipmentSystem Equipment => equipmentSystem;
        public WeaponSystem Weapons => weaponSystem;
        public QuickSlotSystem QuickSlots => quickSlotSystem;
        public AttachmentSystem Attachments => attachmentSystem;
        public PlayerStats Stats => playerStats;

        // === Lifecycle ===

        void Awake()
        {
            AutoFindSystems();
            InjectDependencies();
            ValidateSetup();
        }

        /// <summary>
        /// Auto-find child systems if not manually assigned.
        /// </summary>
        void AutoFindSystems()
        {
            if (inventorySystem == null)
                inventorySystem = GetComponentInChildren<InventorySystem>();

            if (equipmentSystem == null)
                equipmentSystem = GetComponentInChildren<EquipmentSystem>();

            if (weaponSystem == null)
                weaponSystem = GetComponentInChildren<WeaponSystem>();

            if (quickSlotSystem == null)
                quickSlotSystem = GetComponentInChildren<QuickSlotSystem>();

            if (attachmentSystem == null)
                attachmentSystem = GetComponentInChildren<AttachmentSystem>();

            if (playerStats == null)
                playerStats = GetComponent<PlayerStats>();

            Log("Auto-found systems");
        }

        /// <summary>
        /// Inject dependencies between systems.
        /// </summary>
        void InjectDependencies()
        {
            // Equipment system needs inventory + character stats
            if (equipmentSystem != null)
            {
                equipmentSystem.SetInventorySystem(inventorySystem);
                equipmentSystem.SetCharacterStats(playerStats);
            }

            // Weapon system needs inventory
            if (weaponSystem != null)
            {
                weaponSystem.SetInventorySystem(inventorySystem);
            }

            // Attachment system needs inventory
            if (attachmentSystem != null)
            {
                attachmentSystem.SetInventorySystem(inventorySystem);
            }

            // Quick slot system needs inventory
            if (quickSlotSystem != null)
            {
                quickSlotSystem.SetInventorySystem(inventorySystem);
            }

            Log("Injected dependencies between systems");
        }

        /// <summary>
        /// Validate all systems are properly configured.
        /// </summary>
        void ValidateSetup()
        {
            bool valid = true;

            if (config == null)
            {
                LogError("InventoryConfig is not assigned!");
                valid = false;
            }

            if (slotLayout == null)
            {
                LogError("SlotLayoutConfig is not assigned!");
                valid = false;
            }

            if (inventorySystem == null)
            {
                LogError("InventorySystem not found!");
                valid = false;
            }

            if (equipmentSystem == null)
            {
                LogWarning("EquipmentSystem not found - equipment disabled");
            }

            if (weaponSystem == null)
            {
                LogWarning("WeaponSystem not found - weapons disabled");
            }

            if (playerStats == null)
            {
                LogWarning("CharacterStats not found - stat modifiers will not work");
            }

            if (valid)
            {
                Log("✓ PlayerInventoryController setup validated successfully");
            }
        }

        // === PUBLIC API - INVENTORY ===

        #region Inventory Operations

        /// <summary>
        /// Add item to inventory.
        /// </summary>
        public OperationResult AddItem(ItemInstance item, out int assignedSlot)
        {
            if (inventorySystem == null)
            {
                assignedSlot = -1;
                return OperationResult.UnknownError;
            }

            return inventorySystem.AddItem(item, out assignedSlot);
        }

        /// <summary>
        /// Remove item from inventory by instance ID.
        /// </summary>
        public OperationResult RemoveItem(string instanceId)
        {
            if (inventorySystem == null)
                return OperationResult.UnknownError;

            return inventorySystem.RemoveItem(instanceId);
        }

        /// <summary>
        /// Remove item from specific slot.
        /// </summary>
        public OperationResult RemoveItemAtSlot(int slotIndex, out ItemInstance removedItem)
        {
            if (inventorySystem == null)
            {
                removedItem = null;
                return OperationResult.UnknownError;
            }

            return inventorySystem.RemoveItemAtSlot(slotIndex, out removedItem);
        }

        /// <summary>
        /// Move item between inventory slots.
        /// </summary>
        public OperationResult MoveItem(int fromSlot, int toSlot)
        {
            if (inventorySystem == null)
                return OperationResult.UnknownError;

            return inventorySystem.MoveItem(fromSlot, toSlot);
        }

        /// <summary>
        /// Get current inventory weight.
        /// </summary>
        public float GetCurrentWeight()
        {
            return inventorySystem?.GetCurrentWeight() ?? 0f;
        }

        /// <summary>
        /// Get max inventory weight capacity.
        /// </summary>
        public float GetMaxWeight()
        {
            return inventorySystem?.GetMaxWeight() ?? 0f;
        }

        /// <summary>
        /// Check if player is overweight.
        /// </summary>
        public bool IsOverweight()
        {
            return inventorySystem?.IsOverweight() ?? false;
        }

        #endregion

        // === PUBLIC API - EQUIPMENT ===

        #region Equipment Operations

        /// <summary>
        /// Equip item from inventory to equipment slot.
        /// </summary>
        public OperationResult EquipFromInventory(int inventorySlot, EquipmentSlotType equipSlot)
        {
            if (equipmentSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            // Get item from inventory
            var item = inventorySystem.GetItemAtSlot(inventorySlot);
            if (item == null)
                return OperationResult.ItemNotFound;

            // Equip item
            var result = equipmentSystem.EquipItem(item, equipSlot);

            // Remove from inventory on success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItemAtSlot(inventorySlot, out _);
            }

            return result;
        }

        /// <summary>
        /// Unequip item and return to inventory.
        /// </summary>
        public OperationResult UnequipToInventory(EquipmentSlotType equipSlot)
        {
            if (equipmentSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            // Unequip
            var result = equipmentSystem.UnequipItem(equipSlot, out ItemInstance unequippedItem);

            if (result != OperationResult.Success)
                return result;

            // Try to add to inventory
            var addResult = inventorySystem.AddItem(unequippedItem, out _);

            if (addResult != OperationResult.Success)
            {
                // Inventory full - drop item instead
                Log($"Inventory full when unequipping - dropping {unequippedItem.Definition.DisplayName}");
                DropItemInWorld(unequippedItem);
            }

            return OperationResult.Success;
        }

        /// <summary>
        /// Get equipped item in slot.
        /// </summary>
        public ItemInstance GetEquippedItem(EquipmentSlotType slotType)
        {
            return equipmentSystem?.GetEquippedItem(slotType);
        }

        #endregion

        // === PUBLIC API - WEAPONS ===

        #region Weapon Operations

        /// <summary>
        /// Equip weapon from inventory.
        /// </summary>
        public OperationResult EquipWeaponFromInventory(int inventorySlot, WeaponSlotType weaponSlot)
        {
            if (weaponSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            // Get weapon from inventory
            var weapon = inventorySystem.GetItemAtSlot(inventorySlot);
            if (weapon == null)
                return OperationResult.ItemNotFound;

            // Equip weapon
            var result = weaponSystem.EquipWeapon(weapon, weaponSlot);

            // Remove from inventory on success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItemAtSlot(inventorySlot, out _);
            }

            return result;
        }

        /// <summary>
        /// Unequip weapon and return to inventory.
        /// </summary>
        public OperationResult UnequipWeaponToInventory(WeaponSlotType weaponSlot)
        {
            if (weaponSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            // Unequip
            var result = weaponSystem.UnequipWeapon(weaponSlot, out ItemInstance unequippedWeapon);

            if (result != OperationResult.Success)
                return result;

            // Try to add to inventory
            var addResult = inventorySystem.AddItem(unequippedWeapon, out _);

            if (addResult != OperationResult.Success)
            {
                // Inventory full - drop weapon
                Log($"Inventory full when unequipping weapon - dropping {unequippedWeapon.Definition.DisplayName}");
                DropItemInWorld(unequippedWeapon);
            }

            return OperationResult.Success;
        }

        /// <summary>
        /// Switch to weapon in slot.
        /// </summary>
        public OperationResult SwitchWeapon(WeaponSlotType slotType)
        {
            return weaponSystem?.SwitchToWeapon(slotType) ?? OperationResult.UnknownError;
        }

        /// <summary>
        /// Get currently active weapon.
        /// </summary>
        public ItemInstance GetActiveWeapon()
        {
            return weaponSystem?.GetActiveWeapon();
        }

        /// <summary>
        /// Reload active weapon.
        /// </summary>
        public OperationResult ReloadWeapon(int ammoAmount)
        {
            return weaponSystem?.Reload(ammoAmount) ?? OperationResult.UnknownError;
        }

        #endregion

        // === PUBLIC API - ATTACHMENTS ===

        #region Attachment Operations

        /// <summary>
        /// Attach item from inventory to parent item.
        /// </summary>
        public OperationResult AttachFromInventory(int inventorySlot, ItemInstance parentItem)
        {
            if (attachmentSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            // Get attachment from inventory
            var attachment = inventorySystem.GetItemAtSlot(inventorySlot);
            if (attachment == null)
                return OperationResult.ItemNotFound;

            // Attach
            var result = attachmentSystem.AttachItem(parentItem, attachment);

            // Remove from inventory on success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItemAtSlot(inventorySlot, out _);
            }

            return result;
        }

        /// <summary>
        /// Detach attachment and return to inventory.
        /// </summary>
        public OperationResult DetachToInventory(ItemInstance parentItem, AttachmentSlotType slotType)
        {
            if (attachmentSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            // Detach
            var result = attachmentSystem.DetachItem(parentItem, slotType, out ItemInstance detached);

            if (result != OperationResult.Success)
                return result;

            // Attachment system already adds to inventory
            // (handled in AttachmentSystem.DetachItem)

            return OperationResult.Success;
        }

        #endregion

        // === PUBLIC API - QUICK SLOTS ===

        #region Quick Slot Operations

        /// <summary>
        /// Assign inventory item to quick slot.
        /// </summary>
        public OperationResult AssignQuickSlot(int inventorySlot, int quickSlotIndex)
        {
            if (quickSlotSystem == null || inventorySystem == null)
                return OperationResult.UnknownError;

            var item = inventorySystem.GetItemAtSlot(inventorySlot);
            if (item == null)
                return OperationResult.ItemNotFound;

            return quickSlotSystem.AssignToQuickSlot(item, quickSlotIndex);
        }

        /// <summary>
        /// Use quick slot.
        /// </summary>
        public OperationResult UseQuickSlot(int quickSlotIndex)
        {
            return quickSlotSystem?.UseQuickSlot(quickSlotIndex) ?? OperationResult.UnknownError;
        }

        /// <summary>
        /// Clear quick slot.
        /// </summary>
        public void ClearQuickSlot(int quickSlotIndex)
        {
            quickSlotSystem?.ClearQuickSlot(quickSlotIndex);
        }

        #endregion

        // === PUBLIC API - WORLD INTERACTION ===

        #region World Interaction

        /// <summary>
        /// Pickup item from world (called by WorldItemDrop trigger).
        /// </summary>
        public OperationResult PickupItem(ItemInstance item)
        {
            if (item == null)
                return OperationResult.ItemNotFound;

            var result = AddItem(item, out int slot);

            if (result == OperationResult.Success)
            {
                Log($"Picked up {item.Definition.DisplayName} into slot {slot}");
            }
            else
            {
                Log($"Failed to pickup {item.Definition.DisplayName}: {result}");
            }

            return result;
        }

        /// <summary>
        /// Drop item from inventory slot into world.
        /// </summary>
        public OperationResult DropItem(int inventorySlot)
        {
            if (inventorySystem == null)
                return OperationResult.UnknownError;

            // Get item from inventory
            var result = inventorySystem.RemoveItemAtSlot(inventorySlot, out ItemInstance droppedItem);

            if (result != OperationResult.Success)
                return result;

            // Spawn in world
            DropItemInWorld(droppedItem);

            Log($"Dropped {droppedItem.Definition.DisplayName}");
            return OperationResult.Success;
        }

        /// <summary>
        /// Drop equipped item into world.
        /// </summary>
        public OperationResult DropEquippedItem(EquipmentSlotType slotType)
        {
            if (equipmentSystem == null)
                return OperationResult.UnknownError;

            var result = equipmentSystem.UnequipItem(slotType, out ItemInstance unequippedItem);

            if (result != OperationResult.Success)
                return result;

            DropItemInWorld(unequippedItem);

            Log($"Dropped equipped {unequippedItem.Definition.DisplayName}");
            return OperationResult.Success;
        }

        /// <summary>
        /// Spawn item in world as WorldItemDrop.
        /// </summary>
        private void DropItemInWorld(ItemInstance item)
        {
            if (worldItemDropPrefab == null)
            {
                LogError("WorldItemDrop prefab not assigned! Cannot drop item.");
                return;
            }

            // Calculate drop position
            Vector3 dropPosition = GetDropPosition();

            // NETWORK SPAWN (only on server)
            if (networkObject != null && networkObject.IsServer)
            {
                // Spawn as network object
                GameObject dropObject = Instantiate(worldItemDropPrefab, dropPosition, Quaternion.identity);

                // Get NetworkObject component
                var dropNetObj = dropObject.GetComponent<NetworkObject>();
                if (dropNetObj != null)
                {
                    // Spawn on network ...
                    //todo: Spawn(dropObject);

                    // Initialize with item data
                    var dropSync = dropObject.GetComponent<WorldItemDropNetworkSync>();
                    if (dropSync != null)
                    {
                        dropSync.InitializeOnServer(item);
                    }
                }
                else
                {
                    LogError("WorldItemDrop prefab missing NetworkObject component!");
                    Destroy(dropObject);
                    return;
                }
            }
            else if (networkObject == null)
            {
                // Fallback for non-networked game (single player testing)
                GameObject dropObject = Instantiate(worldItemDropPrefab, dropPosition, Quaternion.identity);

                var worldDrop = dropObject.GetComponent<WorldItemDrop>();
                if (worldDrop != null)
                {
                    worldDrop.Initialize(item);
                }
            }
            else
            {
                LogWarning("Cannot drop item - not on server");
                return;
            }

            Log($"Spawned WorldItemDrop for {item.Definition.DisplayName}");
        }


        /// <summary>
        /// Get position where dropped items spawn.
        /// </summary>
        private Vector3 GetDropPosition()
        {
            if (dropSpawnPoint != null)
                return dropSpawnPoint.position;

            if (config != null)
            {
                return transform.position + transform.forward * config.DropDistance;
            }

            return transform.position + transform.forward * 1.5f;
        }

        #endregion

        // === PUBLIC API - UTILITY ===

        #region Utility

        /// <summary>
        /// Get all systems status (for debug UI).
        /// </summary>
        public Dictionary<string, object> GetSystemsStatus()
        {
            return new Dictionary<string, object>
            {
                { "Inventory Slots", inventorySystem?.GetSlotCount() ?? 0 },
                { "Inventory Empty", inventorySystem?.GetEmptySlotCount() ?? 0 },
                { "Current Weight", GetCurrentWeight() },
                { "Max Weight", GetMaxWeight() },
                { "Is Overweight", IsOverweight() },
                { "Equipped Items", equipmentSystem?.GetAllEquippedItems().Count ?? 0 },
                { "Equipped Weapons", weaponSystem?.GetAllEquippedWeapons().Count ?? 0 },
                { "Active Weapon", GetActiveWeapon()?.Definition.DisplayName ?? "None" },
                { "Quick Slots", quickSlotSystem?.GetQuickSlotCount() ?? 0 }
            };
        }

        /// <summary>
        /// Clear entire inventory (for testing/admin).
        /// </summary>
        public void ClearAll()
        {
            inventorySystem?.Clear();
            equipmentSystem?.UnequipAll();
            weaponSystem?.UnequipAll();

            for (int i = 0; i < (quickSlotSystem?.GetQuickSlotCount() ?? 0); i++)
            {
                quickSlotSystem.ClearQuickSlot(i);
            }

            Log("Cleared all inventory systems");
        }

        /// <summary>
        /// Validate all quick slots (cleanup invalid references).
        /// </summary>
        public void ValidateQuickSlots()
        {
            quickSlotSystem?.ValidateQuickSlots();
        }

        #endregion

        // === DEBUG ===

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[PlayerInventoryController] {message}");
        }

        void LogWarning(string message)
        {
            Debug.LogWarning($"[PlayerInventoryController] {message}");
        }

        void LogError(string message)
        {
            Debug.LogError($"[PlayerInventoryController] {message}");
        }

        // === DEBUG GUI ===

#if UNITY_EDITOR
        [ContextMenu("Debug: Print System Status")]
        void DebugPrintStatus()
        {
            var status = GetSystemsStatus();
            Debug.Log("=== INVENTORY SYSTEM STATUS ===");
            foreach (var kvp in status)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value}");
            }
        }

        [ContextMenu("Debug: Clear All")]
        void DebugClearAll()
        {
            ClearAll();
        }
#endif
    }
}