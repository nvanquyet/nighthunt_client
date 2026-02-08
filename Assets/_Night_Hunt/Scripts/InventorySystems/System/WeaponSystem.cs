using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.Stats;
using _Night_Hunt.Scripts.InventorySystems.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Systems
{
    /// <summary>
    /// Weapon system managing weapon slots (Primary, Secondary, future: Melee).
    /// Handles weapon switching, ammo management, and weapon stat tracking.
    /// Implements IWeaponSystem interface.
    /// </summary>
    public class WeaponSystem : MonoBehaviour, IWeaponSystem
    {
        [Header("Configuration")]
        [SerializeField] private InventoryConfig config;
        [SerializeField] private SlotLayoutConfig slotLayout;
        
        [Header("References")]
        [SerializeField] private InventorySystem inventorySystem; // Injected
        [SerializeField] private Transform weaponHolder; // Parent transform for equipped weapon models
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // Weapon slots: SlotType → ItemInstance
        private Dictionary<WeaponSlotType, ItemInstance> weaponSlots = new Dictionary<WeaponSlotType, ItemInstance>();
        
        // Active weapon tracking
        private WeaponSlotType currentActiveSlot = WeaponSlotType.Primary;
        
        // Weapon stats component (created dynamically)
        private WeaponStats currentWeaponStats;
        
        // Weapon model instances
        private Dictionary<WeaponSlotType, GameObject> weaponModels = new Dictionary<WeaponSlotType, GameObject>();
        
        // === Lifecycle ===
        
        void Awake()
        {
            Initialize();
        }
        
        void Initialize()
        {
            if (config == null || slotLayout == null)
            {
                LogError("InventoryConfig or SlotLayoutConfig not assigned!");
                return;
            }
            
            // Initialize weapon slots from config
            foreach (var weaponSlot in slotLayout.WeaponSlots)
            {
                weaponSlots[weaponSlot.SlotType] = null;
            }
            
            Log($"Initialized weapon system with {weaponSlots.Count} slots");
        }
        
        // === Dependency Injection ===
        
        public void SetInventorySystem(InventorySystem inventory)
        {
            inventorySystem = inventory;
        }
        
        // === IWeaponSystem Implementation ===
        
        #region Query
        
        public ItemInstance GetEquippedWeapon(WeaponSlotType slotType)
        {
            return weaponSlots.ContainsKey(slotType) ? weaponSlots[slotType] : null;
        }
        
        public ItemInstance GetActiveWeapon()
        {
            return GetEquippedWeapon(currentActiveSlot);
        }
        
        public WeaponSlotType GetActiveWeaponSlot()
        {
            return currentActiveSlot;
        }
        
        public bool IsSlotEquipped(WeaponSlotType slotType)
        {
            return GetEquippedWeapon(slotType) != null;
        }
        
        public bool CanEquip(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (weapon == null || weapon.Definition == null)
                return false;
            
            // Check if item is a weapon
            if (weapon.Definition.ItemType != ItemType.Weapon)
                return false;
            
            // Check if slot exists in config
            if (!weaponSlots.ContainsKey(slotType))
                return false;
            
            // Check if weapon type is allowed in this slot
            var slotDef = slotLayout.WeaponSlots.FirstOrDefault(s => s.SlotType == slotType);
            if (slotDef.AllowedTypes != null && slotDef.AllowedTypes.Length > 0)
            {
                if (!slotDef.AllowedTypes.Contains(weapon.Definition.ItemType))
                    return false;
            }
            
            return true;
        }
        
        #endregion
        
        #region Equip/Unequip
        
        public OperationResult EquipWeapon(ItemInstance weapon, WeaponSlotType slotType)
        {
            // Validate
            if (!CanEquip(weapon, slotType))
            {
                WeaponEvents.InvokeWeaponOperationFailed(OperationResult.InvalidItemType, slotType, "Cannot equip this weapon in this slot");
                return OperationResult.InvalidItemType;
            }
            
            // If slot occupied, swap
            if (IsSlotEquipped(slotType))
            {
                return SwapWeapon(weapon, slotType, out _);
            }
            
            // Equip
            weaponSlots[slotType] = weapon;
            weapon.IsEquipped = true;
            weapon.EquippedLocation = SlotLocationType.Weapon;
            
            // Spawn weapon model
            SpawnWeaponModel(weapon, slotType);
            
            // If this is the first weapon, make it active
            if (GetAllEquippedWeapons().Count == 1)
            {
                SwitchToWeapon(slotType);
            }
            
            // Fire event
            WeaponEvents.InvokeWeaponEquipped(weapon, slotType);
            
            Log($"Equipped {weapon.Definition.DisplayName} in {slotType} slot");
            return OperationResult.Success;
        }
        
        public OperationResult UnequipWeapon(WeaponSlotType slotType, out ItemInstance unequippedWeapon)
        {
            unequippedWeapon = null;
            
            // Check if equipped
            if (!IsSlotEquipped(slotType))
            {
                WeaponEvents.InvokeWeaponOperationFailed(OperationResult.NotEquipped, slotType, "No weapon in this slot");
                return OperationResult.NotEquipped;
            }
            
            unequippedWeapon = weaponSlots[slotType];
            
            // If this was the active weapon, switch to another or clear
            if (currentActiveSlot == slotType)
            {
                // Try to switch to another weapon
                var otherSlot = weaponSlots.Keys.FirstOrDefault(k => k != slotType && IsSlotEquipped(k));
                if (otherSlot != default(WeaponSlotType))
                {
                    SwitchToWeapon(otherSlot);
                }
                else
                {
                    // No other weapons, clear active
                    DestroyWeaponStats();
                }
            }
            
            // Destroy weapon model
            DestroyWeaponModel(slotType);
            
            // Clear slot
            weaponSlots[slotType] = null;
            unequippedWeapon.IsEquipped = false;
            unequippedWeapon.EquippedLocation = SlotLocationType.Inventory;
            
            // Fire event
            WeaponEvents.InvokeWeaponUnequipped(unequippedWeapon, slotType);
            
            Log($"Unequipped {unequippedWeapon.Definition.DisplayName} from {slotType} slot");
            return OperationResult.Success;
        }
        
        public OperationResult SwapWeapon(ItemInstance newWeapon, WeaponSlotType slotType, out ItemInstance oldWeapon)
        {
            oldWeapon = null;
            
            // Validate new weapon
            if (!CanEquip(newWeapon, slotType))
            {
                WeaponEvents.InvokeWeaponOperationFailed(OperationResult.InvalidItemType, slotType, "Cannot equip this weapon");
                return OperationResult.InvalidItemType;
            }
            
            // Get old weapon
            oldWeapon = weaponSlots[slotType];
            
            // If slot empty, just equip
            if (oldWeapon == null)
            {
                return EquipWeapon(newWeapon, slotType);
            }
            
            // Was the old weapon active?
            bool wasActive = (currentActiveSlot == slotType);
            
            // Destroy old weapon model
            DestroyWeaponModel(slotType);
            
            // Swap
            weaponSlots[slotType] = newWeapon;
            newWeapon.IsEquipped = true;
            newWeapon.EquippedLocation = SlotLocationType.Weapon;
            oldWeapon.IsEquipped = false;
            oldWeapon.EquippedLocation = SlotLocationType.Inventory;
            
            // Spawn new weapon model
            SpawnWeaponModel(newWeapon, slotType);
            
            // If was active, reinitialize stats with new weapon
            if (wasActive)
            {
                InitializeWeaponStats(newWeapon);
            }
            
            // Fire event
            WeaponEvents.InvokeWeaponSwapped(oldWeapon, newWeapon, slotType);
            
            Log($"Swapped {oldWeapon.Definition.DisplayName} with {newWeapon.Definition.DisplayName} in {slotType} slot");
            return OperationResult.Success;
        }
        
        #endregion
        
        #region Weapon Switching
        
        public OperationResult SwitchToWeapon(WeaponSlotType slotType)
        {
            // Check if weapon equipped in slot
            if (!IsSlotEquipped(slotType))
            {
                WeaponEvents.InvokeWeaponOperationFailed(OperationResult.NotEquipped, slotType, "No weapon in this slot");
                return OperationResult.NotEquipped;
            }
            
            // Already active?
            if (currentActiveSlot == slotType)
            {
                Log($"{slotType} weapon already active");
                return OperationResult.Success;
            }
            
            var previousWeapon = GetActiveWeapon();
            var previousSlot = currentActiveSlot;
            
            // Hide previous weapon model
            if (weaponModels.ContainsKey(previousSlot))
            {
                weaponModels[previousSlot].SetActive(false);
            }
            
            // Switch active slot
            currentActiveSlot = slotType;
            var newWeapon = GetActiveWeapon();
            
            // Show new weapon model
            if (weaponModels.ContainsKey(slotType))
            {
                weaponModels[slotType].SetActive(true);
            }
            
            // Initialize weapon stats for new active weapon
            InitializeWeaponStats(newWeapon);
            
            // Fire event
            WeaponEvents.InvokeActiveWeaponChanged(previousWeapon, newWeapon, slotType);
            
            Log($"Switched to {slotType} weapon: {newWeapon.Definition.DisplayName}");
            return OperationResult.Success;
        }
        
        public OperationResult SwitchToNextWeapon()
        {
            var equippedSlots = weaponSlots.Keys.Where(k => IsSlotEquipped(k)).ToList();
            if (equippedSlots.Count == 0)
                return OperationResult.NotEquipped;
            
            int currentIndex = equippedSlots.IndexOf(currentActiveSlot);
            int nextIndex = (currentIndex + 1) % equippedSlots.Count;
            
            return SwitchToWeapon(equippedSlots[nextIndex]);
        }
        
        public OperationResult SwitchToPreviousWeapon()
        {
            var equippedSlots = weaponSlots.Keys.Where(k => IsSlotEquipped(k)).ToList();
            if (equippedSlots.Count == 0)
                return OperationResult.NotEquipped;
            
            int currentIndex = equippedSlots.IndexOf(currentActiveSlot);
            int prevIndex = (currentIndex - 1 + equippedSlots.Count) % equippedSlots.Count;
            
            return SwitchToWeapon(equippedSlots[prevIndex]);
        }
        
        #endregion
        
        #region Ammo Management
        
        public OperationResult Reload(int ammoAmount)
        {
            var weapon = GetActiveWeapon();
            if (weapon == null)
            {
                return OperationResult.NotEquipped;
            }
            
            int maxAmmo = GetMaxAmmo();
            int currentAmmo = weapon.CurrentAmmo;
            
            // Already full?
            if (currentAmmo >= maxAmmo)
            {
                Log("Weapon already fully loaded");
                return OperationResult.Success;
            }
            
            // Calculate reload amount
            int neededAmmo = maxAmmo - currentAmmo;
            int actualReload = Mathf.Min(ammoAmount, neededAmmo);
            
            // Update ammo
            weapon.CurrentAmmo += actualReload;
            
            // Fire events
            WeaponEvents.InvokeWeaponReloaded(weapon, actualReload);
            WeaponEvents.InvokeAmmoChanged(weapon, weapon.CurrentAmmo, maxAmmo);
            
            Log($"Reloaded {actualReload} rounds. Current: {weapon.CurrentAmmo}/{maxAmmo}");
            return OperationResult.Success;
        }
        
        public int GetCurrentAmmo()
        {
            var weapon = GetActiveWeapon();
            return weapon?.CurrentAmmo ?? 0;
        }
        
        public int GetMaxAmmo()
        {
            if (currentWeaponStats == null)
                return 0;
            
            return currentWeaponStats.GetMagazineSize();
        }
        
        /// <summary>
        /// Consume ammo when firing (called by weapon/combat system).
        /// </summary>
        public bool ConsumeAmmo(int amount = 1)
        {
            var weapon = GetActiveWeapon();
            if (weapon == null)
                return false;
            
            if (weapon.CurrentAmmo < amount)
                return false;
            
            weapon.CurrentAmmo -= amount;
            
            WeaponEvents.InvokeAmmoChanged(weapon, weapon.CurrentAmmo, GetMaxAmmo());
            
            return true;
        }
        
        #endregion
        
        // === Weapon Stats Management ===
        
        /// <summary>
        /// Initialize WeaponStats component for active weapon.
        /// </summary>
        private void InitializeWeaponStats(ItemInstance weapon)
        {
            if (weapon == null)
            {
                DestroyWeaponStats();
                return;
            }
            
            // Create WeaponStats component if not exists
            if (currentWeaponStats == null)
            {
                currentWeaponStats = gameObject.AddComponent<WeaponStats>();
            }
            
            // Initialize with weapon instance
            currentWeaponStats.Initialize(weapon);
            
            Log($"Initialized weapon stats for {weapon.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Destroy WeaponStats component when no weapon equipped.
        /// </summary>
        private void DestroyWeaponStats()
        {
            if (currentWeaponStats != null)
            {
                Destroy(currentWeaponStats);
                currentWeaponStats = null;
                Log("Destroyed weapon stats component");
            }
        }
        
        // === Weapon Model Management ===
        
        /// <summary>
        /// Spawn 3D weapon model in world/hand.
        /// </summary>
        private void SpawnWeaponModel(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (weapon.Definition.EquippedModelPrefab == null)
            {
                Log($"No equipped model prefab for {weapon.Definition.DisplayName}");
                return;
            }
            
            if (weaponHolder == null)
            {
                LogError("WeaponHolder transform not assigned!");
                return;
            }
            
            // Destroy old model if exists
            DestroyWeaponModel(slotType);
            
            // Spawn new model
            GameObject model = Instantiate(weapon.Definition.EquippedModelPrefab, weaponHolder);
            model.name = $"Weapon_{slotType}_{weapon.Definition.DisplayName}";
            
            // Hide if not active slot
            model.SetActive(slotType == currentActiveSlot);
            
            weaponModels[slotType] = model;
            
            Log($"Spawned weapon model for {weapon.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Destroy weapon model.
        /// </summary>
        private void DestroyWeaponModel(WeaponSlotType slotType)
        {
            if (weaponModels.ContainsKey(slotType))
            {
                Destroy(weaponModels[slotType]);
                weaponModels.Remove(slotType);
            }
        }
        
        // === Public API - Additional ===
        
        /// <summary>
        /// Get all equipped weapons.
        /// </summary>
        public List<ItemInstance> GetAllEquippedWeapons()
        {
            return weaponSlots.Values.Where(w => w != null).ToList();
        }
        
        /// <summary>
        /// Get weapon stats component (for combat system).
        /// </summary>
        public WeaponStats GetWeaponStats()
        {
            return currentWeaponStats;
        }
        
        /// <summary>
        /// Handle weapon durability decrease.
        /// </summary>
        public void DamageWeapon(WeaponSlotType slotType, float damage)
        {
            var weapon = GetEquippedWeapon(slotType);
            if (weapon == null)
                return;
            
            weapon.DecreaseDurability(damage);
            
            WeaponEvents.InvokeWeaponDurabilityChanged(weapon, slotType, weapon.CurrentDurability);
            
            // Auto-unequip if broken
            if (config.ItemsBreakAtZeroDurability && weapon.IsBroken())
            {
                Log($"{weapon.Definition.DisplayName} broke and was unequipped");
                UnequipWeapon(slotType, out _);
            }
        }
        
        /// <summary>
        /// Unequip all weapons.
        /// </summary>
        public void UnequipAll()
        {
            var slotTypes = weaponSlots.Keys.ToArray();
            
            foreach (var slotType in slotTypes)
            {
                if (IsSlotEquipped(slotType))
                {
                    UnequipWeapon(slotType, out _);
                }
            }
            
            Log("Unequipped all weapons");
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[WeaponSystem] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[WeaponSystem] {message}");
        }
    }
}