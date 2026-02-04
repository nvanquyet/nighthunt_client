using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Weapon;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Weapon panel UI with Primary/Secondary slots.
    /// Shows active weapon, handles switching, displays ammo.
    /// </summary>
    public class WeaponPanelUI : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private WeaponSlotConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private WeaponManager weaponManager;
        private Dictionary<WeaponSlotType, WeaponSlotUI> slotMap;
        
        #region Lifecycle
        
        void Awake()
        {
            // Initialize slot map
            slotMap = new Dictionary<WeaponSlotType, WeaponSlotUI>();
            
            // Spawn slots from config (required)
            if (config == null)
            {
                Debug.LogError("[WeaponPanelUI] WeaponSlotConfig is required! Please assign config in Inspector.");
                return;
            }
            
            if (slotContainer == null)
            {
                Debug.LogError("[WeaponPanelUI] SlotContainer is required! Please assign container Transform in Inspector.");
                return;
            }
            
            if (slotPrefab == null)
            {
                Debug.LogError("[WeaponPanelUI] SlotPrefab is required! Please assign slot prefab in Inspector.");
                return;
            }
            
            SpawnSlotsFromConfig();
        }
        
        /// <summary>
        /// Sets the WeaponManager reference (from local player).
        /// Called by parent controller or player setup.
        /// </summary>
        public void SetWeaponManager(WeaponManager manager)
        {
            weaponManager = manager;
            
            if (enableDebugLogs)
                Debug.Log("[WeaponPanelUI] WeaponManager injected");
            
            // Refresh slots after manager is set
            if (slotMap.Count > 0)
            {
                RefreshAllSlots();
            }
        }
        
        private void SpawnSlotsFromConfig()
        {
            if (config.Slots == null || config.Slots.Length == 0)
            {
                Debug.LogError("[WeaponPanelUI] Config has no slots defined! Please configure WeaponSlotConfig.");
                return;
            }
            
            // Spawn slots from config
            foreach (var slotData in config.Slots)
            {
                var slotObj = Instantiate(slotPrefab, slotContainer);
                var slotUI = slotObj.GetComponent<WeaponSlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Initialize(slotData.SlotType, this);
                    slotMap[slotData.SlotType] = slotUI;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[WeaponPanelUI] Spawned {slotData.SlotType} slot from config");
                }
                else
                {
                    Debug.LogError($"[WeaponPanelUI] Slot prefab doesn't have WeaponSlotUI component!");
                    Destroy(slotObj);
                }
            }
        }
        
        void OnEnable()
        {
            WeaponEvents.OnWeaponEquipped += HandleWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped += HandleWeaponUnequipped;
            WeaponEvents.OnWeaponSwitched += HandleWeaponSwitched;
            WeaponEvents.OnAmmoChanged += HandleAmmoChanged;
        }
        
        void OnDisable()
        {
            WeaponEvents.OnWeaponEquipped -= HandleWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped -= HandleWeaponUnequipped;
            WeaponEvents.OnWeaponSwitched -= HandleWeaponSwitched;
            WeaponEvents.OnAmmoChanged -= HandleAmmoChanged;
        }
        
        void Start()
        {
            RefreshAllSlots();
        }
        
        #endregion
        
        #region Public API
        
        public void OnItemDroppedOnSlot(ItemInstance item, WeaponSlotType targetSlot)
        {
            if (weaponManager == null)
            {
                Debug.LogError("[WeaponPanelUI] WeaponManager not assigned!");
                return;
            }
            
            // Validate item type
            if (item.Definition.ItemType != ItemType.Weapon)
            {
                UIEvents.InvokeShowError("Only weapons can be equipped here");
                return;
            }
            
            // Try equip
            var result = weaponManager.EquipWeapon(item, targetSlot);
            
            if (result.IsSuccess)
            {
                // If swapped, add old weapon back to inventory
                if (result.SwappedItem != null)
                {
                    InventoryEvents.InvokeRequestAddItem(result.SwappedItem);
                }
                
                // Remove from inventory
                InventoryEvents.InvokeRequestRemoveItem(item.InstanceId);
                
                if (enableDebugLogs)
                    Debug.Log($"[WeaponPanelUI] Equipped {item.Definition.ItemId} in {targetSlot}");
            }
            else
            {
                UIEvents.InvokeShowError(result.FailReason);
            }
        }
        
        public void OnUnequipRequested(WeaponSlotType slotType)
        {
            if (weaponManager == null) return;
            
            if (weaponManager.TryUnequipWeapon(slotType, out ItemInstance unequippedWeapon))
            {
                InventoryEvents.InvokeRequestAddItem(unequippedWeapon);
                
                if (enableDebugLogs)
                    Debug.Log($"[WeaponPanelUI] Unequipped {unequippedWeapon.Definition.ItemId}");
            }
        }
        
        public void OnSwitchRequested(WeaponSlotType slotType)
        {
            if (weaponManager == null) return;
            
            weaponManager.SwitchToSlot(slotType);
        }
        
        public void RefreshAllSlots()
        {
            if (weaponManager == null) return;
            
            foreach (var kvp in slotMap)
            {
                var weapon = weaponManager.GetWeapon(kvp.Key);
                kvp.Value.SetWeapon(weapon);
                
                // Update active state
                bool isActive = (weaponManager.GetActiveSlot() == kvp.Key);
                kvp.Value.SetActiveState(isActive);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                slot.SetWeapon(weapon);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[WeaponPanelUI] UI updated - equipped {weapon.Definition.ItemId}");
        }
        
        private void HandleWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                slot.SetWeapon(null);
                slot.SetActiveState(false);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[WeaponPanelUI] UI updated - unequipped {weapon.Definition.ItemId}");
        }
        
        private void HandleWeaponSwitched(WeaponSlotType newActiveSlot)
        {
            // Update all slots' active state
            foreach (var kvp in slotMap)
            {
                bool isActive = (kvp.Key == newActiveSlot);
                kvp.Value.SetActiveState(isActive);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[WeaponPanelUI] Active weapon changed to {newActiveSlot}");
        }
        
        private void HandleAmmoChanged(ItemInstance weapon, int newAmmo)
        {
            // Find slot with this weapon and update ammo display
            foreach (var kvp in slotMap)
            {
                if (kvp.Value.GetWeapon() == weapon)
                {
                    kvp.Value.UpdateAmmoDisplay(newAmmo);
                    break;
                }
            }
        }
        
        #endregion
    }
}