using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Interfaces;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Validation;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.Core.Structs;
using NightHunt.Inventory.Database;
using NightHunt.Inventory.Stats;

namespace NightHunt.Inventory.Network
{
    /// <summary>
    /// Network-authoritative inventory system using FishNet Pro v4.
    /// NO LOCAL FILES - everything is NetworkBehaviour with SyncList.
    /// Server validates all operations, clients predict with rollback.
    /// </summary>
    public partial class PlayerInventoryNetwork : NetworkBehaviour, IInventoryOperations
    {
        [Header("Configuration")]
        [SerializeField] private InventoryConfig config;
        
        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // ===== NETWORK SYNCED DATA =====
        
        /// <summary>
        /// Main inventory list (index-based, can have gaps).
        /// SyncList automatically syncs to all clients.
        /// </summary>
        private readonly SyncList<ItemInstanceData> inventoryList = new SyncList<ItemInstanceData>();
        
        /// <summary>
        /// Equipment slots (Helmet, Armor, Backpack).
        /// </summary>
        private readonly SyncDictionary<EquipmentSlotType, ItemInstanceData> equipmentSlots = 
            new SyncDictionary<EquipmentSlotType, ItemInstanceData>();
        
        /// <summary>
        /// Weapon slots (Primary, Secondary).
        /// </summary>
        private readonly SyncList<ItemInstanceData> weaponSlots = new SyncList<ItemInstanceData>();
        
        /// <summary>
        /// Quickslots (default: 4).
        /// </summary>
        private readonly SyncList<ItemInstanceData> quickSlots = new SyncList<ItemInstanceData>();
        
        /// <summary>
        /// Current item being used (if any).
        /// </summary>
        [SyncVar(OnChange = nameof(OnCurrentUsageItemChanged))]
        private ItemInstanceData currentUsageItem;
        
        /// <summary>
        /// Usage progress timer (synced for UI).
        /// </summary>
        [SyncVar]
        private float usageTimeRemaining;
        
        // ===== LOCAL RUNTIME DATA =====
        
        private IInventoryValidator validator;
        private Dictionary<string, ItemInstance> runtimeItemCache; // InstanceId -> ItemInstance
        private ItemInstance currentUsageItemInstance;
        private bool isUsingItem;
        
        // ===== LIFECYCLE =====
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (config == null)
            {
                LogError("InventoryConfig is not assigned!");
                return;
            }
            
            validator = new InventoryValidator(config);
            runtimeItemCache = new Dictionary<string, ItemInstance>();
            
            // Initialize slots
            InitializeSlots();
            
            // Subscribe to sync events
            inventoryList.OnChange += OnInventoryListChanged;
            equipmentSlots.OnChange += OnEquipmentSlotsChanged;
            weaponSlots.OnChange += OnWeaponSlotsChanged;
            quickSlots.OnChange += OnQuickSlotsChanged;
            
            Log("Inventory system initialized on client");
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            // Unsubscribe
            inventoryList.OnChange -= OnInventoryListChanged;
            equipmentSlots.OnChange -= OnEquipmentSlotsChanged;
            weaponSlots.OnChange -= OnWeaponSlotsChanged;
            quickSlots.OnChange -= OnQuickSlotsChanged;
        }
        
        private void Update()
        {
            if (!IsOwner)
                return;
            
            // Handle item usage timer
            if (isUsingItem && usageTimeRemaining > 0f)
            {
                usageTimeRemaining -= Time.deltaTime;
                
                if (usageTimeRemaining <= 0f)
                {
                    CompleteItemUsageServerRpc();
                }
            }
        }
        
        private void InitializeSlots()
        {
            // Initialize weapon slots
            for (int i = weaponSlots.Count; i < config.WeaponSlotCount; i++)
            {
                if (IsServer)
                    weaponSlots.Add(default);
            }
            
            // Initialize quickslots
            for (int i = quickSlots.Count; i < config.QuickSlotCount; i++)
            {
                if (IsServer)
                    quickSlots.Add(default);
            }
        }
        
        // ===== SYNC CALLBACKS =====
        
        private void OnInventoryListChanged(SyncListOperation op, int index, ItemInstanceData oldItem, ItemInstanceData newItem, bool asServer)
        {
            if (asServer)
                return;
            
            // Rebuild cache on client
            RebuildRuntimeCache();
            
            // Raise events based on operation
            switch (op)
            {
                case SyncListOperation.Add:
                    HandleItemAddedOnClient(newItem, index);
                    break;
                
                case SyncListOperation.RemoveAt:
                    HandleItemRemovedOnClient(oldItem, index);
                    break;
                
                case SyncListOperation.Set:
                    HandleItemChangedOnClient(oldItem, newItem, index);
                    break;
            }
        }
        
        private void OnEquipmentSlotsChanged(SyncDictionaryOperation op, EquipmentSlotType key, ItemInstanceData value, bool asServer)
        {
            if (asServer)
                return;
            
            RebuildRuntimeCache();
            
            // Raise equipment changed event
            if (op == SyncDictionaryOperation.Add || op == SyncDictionaryOperation.Set)
            {
                var item = DeserializeItem(value);
                InventoryEvents.RaiseItemEquipped(new ItemEquippedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = IsOwner,
                    Item = item,
                    SlotType = key,
                    SwappedItem = null
                });
            }
        }
        
        private void OnWeaponSlotsChanged(SyncListOperation op, int index, ItemInstanceData oldItem, ItemInstanceData newItem, bool asServer)
        {
            if (asServer)
                return;
            
            RebuildRuntimeCache();
            
            if (op == SyncListOperation.Set && !IsDefaultItemData(newItem))
            {
                var weapon = DeserializeItem(newItem);
                InventoryEvents.RaiseWeaponEquipped(new WeaponEquippedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = IsOwner,
                    Weapon = weapon,
                    WeaponSlotIndex = index,
                    SwappedWeapon = null
                });
            }
        }
        
        private void OnQuickSlotsChanged(SyncListOperation op, int index, ItemInstanceData oldItem, ItemInstanceData newItem, bool asServer)
        {
            if (asServer)
                return;
            
            RebuildRuntimeCache();
            
            if (op == SyncListOperation.Set && !IsDefaultItemData(newItem))
            {
                var item = DeserializeItem(newItem);
                InventoryEvents.RaiseQuickSlotAssigned(new QuickSlotAssignedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = IsOwner,
                    Item = item,
                    QuickSlotIndex = index,
                    SwappedItem = null
                });
            }
        }
        
        private void OnCurrentUsageItemChanged(ItemInstanceData oldValue, ItemInstanceData newValue, bool asServer)
        {
            if (asServer)
                return;
            
            if (!IsDefaultItemData(newValue))
            {
                // Item usage started
                currentUsageItemInstance = DeserializeItem(newValue);
                isUsingItem = true;
                
                InventoryEvents.RaiseItemUsageStarted(new ItemUsageStartedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = IsOwner,
                    Item = currentUsageItemInstance,
                    UsageDuration = usageTimeRemaining
                });
            }
            else
            {
                // Item usage ended
                isUsingItem = false;
                currentUsageItemInstance = null;
            }
        }
        
        // ===== RUNTIME CACHE MANAGEMENT =====
        
        private void RebuildRuntimeCache()
        {
            runtimeItemCache.Clear();
            
            // Cache inventory items
            foreach (var itemData in inventoryList)
            {
                if (!IsDefaultItemData(itemData))
                {
                    var item = DeserializeItem(itemData);
                    runtimeItemCache[item.InstanceId] = item;
                }
            }
            
            // Cache equipment items
            foreach (var kvp in equipmentSlots)
            {
                if (!IsDefaultItemData(kvp.Value))
                {
                    var item = DeserializeItem(kvp.Value);
                    runtimeItemCache[item.InstanceId] = item;
                }
            }
            
            // Cache weapon items
            foreach (var itemData in weaponSlots)
            {
                if (!IsDefaultItemData(itemData))
                {
                    var item = DeserializeItem(itemData);
                    runtimeItemCache[item.InstanceId] = item;
                }
            }
            
            // Cache quickslot items
            foreach (var itemData in quickSlots)
            {
                if (!IsDefaultItemData(itemData))
                {
                    var item = DeserializeItem(itemData);
                    runtimeItemCache[item.InstanceId] = item;
                }
            }
        }
        
        // ===== HELPER METHODS =====
        
        private ItemInstance DeserializeItem(ItemInstanceData data)
        {
            if (IsDefaultItemData(data))
                return null;
            
            var definition = ItemDefinitionDatabase.Instance.GetDefinition(data.ItemId);
            if (definition == null)
            {
                LogError($"Failed to find ItemDefinition for {data.ItemId}");
                return null;
            }
            
            return ItemInstance.Deserialize(data, definition);
        }
        
        private bool IsDefaultItemData(ItemInstanceData data)
        {
            return string.IsNullOrEmpty(data.InstanceId);
        }
        
        private void HandleItemAddedOnClient(ItemInstanceData itemData, int index)
        {
            var item = DeserializeItem(itemData);
            if (item == null)
                return;
            
            InventoryEvents.RaiseItemAdded(new ItemAddedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = IsOwner,
                Item = item,
                InventoryIndex = index
            });
            
            UpdateWeightDisplay();
        }
        
        private void HandleItemRemovedOnClient(ItemInstanceData itemData, int index)
        {
            InventoryEvents.RaiseItemRemoved(new ItemRemovedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = IsOwner,
                InstanceId = itemData.InstanceId,
                InventoryIndex = index
            });
            
            UpdateWeightDisplay();
        }
        
        private void HandleItemChangedOnClient(ItemInstanceData oldData, ItemInstanceData newData, int index)
        {
            // Item was modified at this index
            if (!IsDefaultItemData(newData))
            {
                var item = DeserializeItem(newData);
                InventoryEvents.RaiseItemAdded(new ItemAddedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = IsOwner,
                    Item = item,
                    InventoryIndex = index
                });
            }
            else
            {
                InventoryEvents.RaiseItemRemoved(new ItemRemovedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = IsOwner,
                    InstanceId = oldData.InstanceId,
                    InventoryIndex = index
                });
            }
            
            UpdateWeightDisplay();
        }
        
        private void UpdateWeightDisplay()
        {
            float currentWeight = GetCurrentWeight();
            float maxWeight = playerStats != null ? playerStats.GetWeightCapacity() : 100f;
            
            InventoryEvents.RaiseWeightChanged(new WeightChangedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = IsOwner,
                CurrentWeight = currentWeight,
                MaxWeight = maxWeight,
                IsOverweight = currentWeight > maxWeight
            });
        }
        
        // ===== LOGGING =====
        
        private void Log(string message)
        {
            if (enableDebugLogs || (config != null && config.EnableDebugLogs))
                UnityEngine.Debug.Log($"[PlayerInventoryNetwork] {message}");
        }
        
        private void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[PlayerInventoryNetwork] {message}");
        }
        
        private void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[PlayerInventoryNetwork] {message}");
        }
        
        // ===== CONTINUED IN PART 2 (RPC methods and IInventoryOperations implementation) =====
    }
}