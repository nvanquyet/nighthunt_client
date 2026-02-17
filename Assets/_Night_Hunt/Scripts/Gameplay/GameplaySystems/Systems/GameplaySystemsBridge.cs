using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Data;
using GameplaySystems.Inventory;
using GameplaySystems.Stat;

namespace GameplaySystems.Core
{
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Contract that every caller (Console, UI, AI, …) uses to talk to gameplay systems.
    /// All public operations + all re-published events live here.
    /// Implement once on GameplaySystemsBridge; the rest of the codebase only knows
    /// about this interface.
    /// </summary>
    public interface IGameplayBridge
    {
        bool IsReady { get; }

        // ─ Systems (read-only access when you need raw data) ──────────────────
        IInventorySystem Inventory { get; }
        IEquipmentSystem Equipment { get; }
        IWeaponSystem Weapon { get; }
        IQuickSlotSystem QuickSlot { get; }
        IPlayerStat Stat { get; }
        ItemUseSystem ItemUse { get; }


        // ─ Inventory ──────────────────────────────────────────────────────────
        void AddItem(string defID, int qty = 1);
        void RemoveItem(string instanceID, int qty = 1);
        void RemoveItemByDef(string defID, int qty = 1);
        void SwapItems(string id1, string id2);
        void DropItem(string instanceID, int qty = 1);
        void ClearInventory();
        IReadOnlyList<ItemInstance> GetAllItems();
        ItemInstance GetItemByInstanceID(string id);
        List<ItemInstance> GetItemsByDef(string defID);
        (float current, float capacity, float percent) GetWeightInfo();

        // ─ Equipment ──────────────────────────────────────────────────────────
        void EquipItem(string instanceID);
        void UnequipItem(EquipmentSlotType slot);
        void UnequipAll();
        void AddAndEquip(string defID);
        Dictionary<EquipmentSlotType, ItemInstance> GetAllEquipped();

        // ─ Weapons ────────────────────────────────────────────────────────────
        void EquipWeapon(string instanceID);
        void AddAndEquipWeapon(string defID, WeaponSlotType slot);
        void UnequipWeapon(WeaponSlotType slot);
        void SelectWeapon(WeaponSlotType slot);
        void HolsterWeapon();
        void Reload(WeaponSlotType slot);
        Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons();
        WeaponSlotType? GetActiveSlot();

        // ─ QuickSlots ─────────────────────────────────────────────────────────
        void AssignToQuickSlot(string instanceID, int slotIndex);
        void AddAndAssignQuickSlot(string defID, int slotIndex);
        void RemoveFromQuickSlot(int slotIndex);
        void UseQuickSlot(int slotIndex);
        void CancelItemUse();
        void ExecuteThrow();
        ItemInstance[] GetAllQuickSlots();

        // ─ Stats ──────────────────────────────────────────────────────────────
        float GetStat(PlayerStatType type);
        float GetBaseStat(PlayerStatType type);
        float GetStatModifier(PlayerStatType type);
        Dictionary<PlayerStatType, float> GetAllStats();
        float GetCurrentWeight();
        float GetWeightCapacity();
        float GetWeightPercent();
        float GetMovementSpeedMultiplier();

        // ─ Scenarios ──────────────────────────────────────────────────────────
        void ScenarioFullLoadout();
        void ScenarioOverweight();

        // ─ Unified events (all system events re-published here) ───────────────
        event Action<ItemInstance> OnItemAdded;
        event Action<ItemInstance, int> OnItemRemoved;
        event Action<ItemInstance, ItemInstance> OnItemsSwapped;
        event Action OnInventoryCleared;
        event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;
        event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;
        event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        event Action<int, ItemInstance> OnQuickSlotAssigned;
        event Action<int> OnQuickSlotRemoved;
        event Action<int, ItemInstance> OnQuickSlotUsed;
        event Action<PlayerStatType, float, float> OnStatChanged;
        event Action<float, float> OnWeightChanged;
        event Action<ItemInstance> OnItemUseStarted;
        event Action<ItemInstance> OnItemUseCompleted;
        event Action<ItemInstance> OnItemUseCancelled;
        event Action<ItemInstance, float> OnItemUseProgress;
    }

    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Concrete implementation of IGameplayBridge.
    ///
    /// ▸ Plain C# class (NOT MonoBehaviour) – instantiated by NetworkPlayer.
    /// ▸ NetworkPlayer exposes it via  public IGameplayBridge GameplayBridge { get; }
    /// ▸ Console resolves it via  SpectateManager.Instance.GetLocalPlayer().GameplayBridge
    ///
    /// Lifecycle:
    ///   1. NetworkPlayer.OnStartServer/OnStartClient calls Initialize()
    ///   2. GameplaySystemsBridge wires up all system event listeners
    ///   3. Dispose() called from NetworkPlayer.OnStopClient
    /// </summary>
    public class GameplaySystemsBridge : IGameplayBridge
    {
        // ── Backing system references ──────────────────────────────────────────
        private IInventorySystem _inventory;
        private IEquipmentSystem _equipment;
        private IWeaponSystem _weapon;
        private IQuickSlotSystem _quickSlot;
        private IPlayerStat _stat;
        private ItemUseSystem _itemUse;

        // ── IGameplayBridge – system accessors ────────────────────────────────
        public IInventorySystem Inventory => _inventory;
        public IEquipmentSystem Equipment => _equipment;
        public IWeaponSystem Weapon => _weapon;
        public IQuickSlotSystem QuickSlot => _quickSlot;
        public IPlayerStat Stat => _stat;
        public ItemUseSystem ItemUse => _itemUse;
        public bool IsReady { get; private set; }

        // ── Unified events ────────────────────────────────────────────────────
        public event Action<ItemInstance> OnItemAdded;
        public event Action<ItemInstance, int> OnItemRemoved;
        public event Action<ItemInstance, ItemInstance> OnItemsSwapped;
        public event Action OnInventoryCleared;
        public event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;
        public event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;
        public event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        public event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        public event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        public event Action<int, ItemInstance> OnQuickSlotAssigned;
        public event Action<int> OnQuickSlotRemoved;
        public event Action<int, ItemInstance> OnQuickSlotUsed;
        public event Action<PlayerStatType, float, float> OnStatChanged;
        public event Action<float, float> OnWeightChanged;
        public event Action<ItemInstance> OnItemUseStarted;
        public event Action<ItemInstance> OnItemUseCompleted;
        public event Action<ItemInstance> OnItemUseCancelled;
        public event Action<ItemInstance, float> OnItemUseProgress;

        // ══════════════════════════════════════════════════════════════════════

        #region Init / Dispose

        public GameplaySystemsBridge(MonoBehaviour rootObject)
        {
            if (!rootObject)
            {
                Debug.LogError($"[GameplaySystemBride] Player must be not null");
                return;
            }

            _inventory = rootObject.GetComponentInChildren<IInventorySystem>();
            _equipment = rootObject.GetComponentInChildren<IEquipmentSystem>();
            _weapon = rootObject.GetComponentInChildren<IWeaponSystem>();
            _quickSlot = rootObject.GetComponentInChildren<IQuickSlotSystem>();
            _stat = rootObject.GetComponentInChildren<IPlayerStat>();
            _itemUse = rootObject.GetComponentInChildren<ItemUseSystem>();

            WireEvents();
            IsReady = true;
            Debug.Log("[GameplayBridge] Initialized – all systems wired.");
        }


        /// <summary>
        /// Call once from NetworkPlayer after all NetworkBehaviour Awake/OnStart
        /// callbacks have fired and component references are valid.
        /// </summary>
        public GameplaySystemsBridge(
            IInventorySystem inventory,
            IEquipmentSystem equipment,
            IWeaponSystem weapon,
            IQuickSlotSystem quickSlot,
            IPlayerStat stat,
            ItemUseSystem itemUse = null)
        {
            _inventory = inventory;
            _equipment = equipment;
            _weapon = weapon;
            _quickSlot = quickSlot;
            _stat = stat;
            _itemUse = itemUse;

            WireEvents();
            IsReady = true;
            Debug.Log("[GameplayBridge] Initialized – all systems wired.");
        }


        public void Dispose()
        {
            IsReady = false;
            // Lambda subscriptions are GC'd with NetworkPlayer; no explicit removal needed.
        }

        private void WireEvents()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded += i => OnItemAdded?.Invoke(i);
                _inventory.OnItemRemoved += (i, q) => OnItemRemoved?.Invoke(i, q);
                _inventory.OnItemsSwapped += (a, b) => OnItemsSwapped?.Invoke(a, b);
                _inventory.OnInventoryCleared += () => OnInventoryCleared?.Invoke();
            }

            if (_equipment != null)
            {
                _equipment.OnItemEquipped += (s, i) => OnItemEquipped?.Invoke(s, i);
                _equipment.OnItemUnequipped += (s, i) => OnItemUnequipped?.Invoke(s, i);
            }

            if (_weapon is WeaponSystem ws)
            {
                ws.OnWeaponEquipped += (s, i) => OnWeaponEquipped?.Invoke(s, i);
                ws.OnWeaponUnequipped += (s, i) => OnWeaponUnequipped?.Invoke(s, i);
                ws.OnActiveWeaponChanged += (o, n) => OnActiveWeaponChanged?.Invoke(o, n);
            }

            if (_quickSlot != null)
            {
                _quickSlot.OnQuickSlotAssigned += (i, it) => OnQuickSlotAssigned?.Invoke(i, it);
                _quickSlot.OnQuickSlotRemoved += i => OnQuickSlotRemoved?.Invoke(i);
                _quickSlot.OnQuickSlotUsed += (i, it) => OnQuickSlotUsed?.Invoke(i, it);
            }

            if (_stat is PlayerStatSystem pss)
            {
                pss.OnStatChanged += (t, o, n) => OnStatChanged?.Invoke(t, o, n);
                pss.OnWeightChanged += (c, cap) => OnWeightChanged?.Invoke(c, cap);
            }

            if (_itemUse != null)
            {
                _itemUse.OnItemUseStarted += i => OnItemUseStarted?.Invoke(i);
                _itemUse.OnItemUseCompleted += i => OnItemUseCompleted?.Invoke(i);
                _itemUse.OnItemUseCancelled += i => OnItemUseCancelled?.Invoke(i);
                _itemUse.OnItemUseProgress += (i, p) => OnItemUseProgress?.Invoke(i, p);
            }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region Inventory

        public void AddItem(string defID, int qty = 1)
        {
            if (!Ready("Inventory")) return;
            _inventory.AddItem(defID, qty);
            Debug.Log($"[Bridge] AddItem {qty}× {defID}");
        }

        public void RemoveItem(string instanceID, int qty = 1)
        {
            if (!Ready("Inventory")) return;
            _inventory.RemoveItem(instanceID, qty);
        }

        public void RemoveItemByDef(string defID, int qty = 1)
        {
            if (!Ready("Inventory")) return;
            _inventory.RemoveItemByDefinition(defID, qty);
        }

        public void SwapItems(string id1, string id2)
        {
            if (!Ready("Inventory")) return;
            _inventory.SwapItems(id1, id2);
        }

        public void DropItem(string instanceID, int qty = 1)
        {
            if (!Ready("Inventory")) return;
            _inventory.DropItem(instanceID, qty);
        }

        public void ClearInventory()
        {
            if (!Ready("Inventory")) return;
            _inventory.ClearInventory();
            Debug.Log("[Bridge] Inventory cleared");
        }

        public IReadOnlyList<ItemInstance> GetAllItems()
            => _inventory?.GetAllItems() ?? new List<ItemInstance>();

        public ItemInstance GetItemByInstanceID(string id)
            => _inventory?.GetItemByInstanceID(id);

        public List<ItemInstance> GetItemsByDef(string defID)
            => _inventory?.GetItemsByDefinition(defID) ?? new List<ItemInstance>();

        public (float current, float capacity, float percent) GetWeightInfo()
            => _inventory?.GetWeightInfo() ?? (0, 0, 0);

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region Equipment

        public void EquipItem(string instanceID)
        {
            if (!Ready("Equipment")) return;
            _equipment.EquipItem(instanceID);
            Debug.Log($"[Bridge] EquipItem {instanceID}");
        }

        public void UnequipItem(EquipmentSlotType slot)
        {
            if (!Ready("Equipment")) return;
            if (!_equipment.IsSlotOccupied(slot))
            {
                Debug.Log($"[Bridge] UnequipItem: slot {slot} is already empty");
                return;
            }

            _equipment.UnequipItem(slot);
        }

        public void UnequipAll()
        {
            if (!Ready("Equipment")) return;
            foreach (EquipmentSlotType s in Enum.GetValues(typeof(EquipmentSlotType)))
                if (_equipment.IsSlotOccupied(s))
                    _equipment.UnequipItem(s);
            Debug.Log("[Bridge] Unequipped all");
        }

        public void AddAndEquip(string defID)
        {
            AddItem(defID);
            var list = GetItemsByDef(defID);
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[Bridge] AddAndEquip: {defID} not found in inventory after add");
                return;
            }

            EquipItem(list[^1].InstanceID);
        }

        public Dictionary<EquipmentSlotType, ItemInstance> GetAllEquipped()
            => _equipment?.GetAllEquippedItems() ?? new Dictionary<EquipmentSlotType, ItemInstance>();

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region Weapon

        public void EquipWeapon(string instanceID)
        {
            if (!Ready("Weapon")) return;
            _weapon.EquipWeapon(instanceID);
        }

        public void AddAndEquipWeapon(string defID, WeaponSlotType slot)
        {
            AddItem(defID);
            var list = GetItemsByDef(defID);
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[Bridge] AddAndEquipWeapon: {defID} not found");
                return;
            }

            EquipWeapon(list[^1].InstanceID);
            Debug.Log($"[Bridge] AddAndEquipWeapon {defID} → {slot}");
        }

        public void UnequipWeapon(WeaponSlotType slot)
        {
            if (!Ready("Weapon")) return;
            if (!_weapon.IsSlotOccupied(slot))
            {
                Debug.Log($"[Bridge] UnequipWeapon: slot {slot} is empty");
                return;
            }

            _weapon.UnequipWeapon(slot);
        }

        public void SelectWeapon(WeaponSlotType slot)
        {
            if (!Ready("Weapon")) return;
            if (!_weapon.IsSlotOccupied(slot))
            {
                Debug.LogWarning($"[Bridge] SelectWeapon: no weapon in slot {slot}");
                return;
            }

            _weapon.SelectWeapon(slot);
        }

        public void HolsterWeapon()
        {
            if (!Ready("Weapon")) return;
            _weapon.HolsterWeapon();
        }

        public void Reload(WeaponSlotType slot)
        {
            if (!Ready("Weapon")) return;
            if (!_weapon.IsSlotOccupied(slot))
            {
                Debug.LogWarning($"[Bridge] Reload: no weapon in slot {slot}");
                return;
            }

            _weapon.Reload(slot);
        }

        public Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons()
            => _weapon?.GetAllWeapons() ?? new Dictionary<WeaponSlotType, ItemInstance>();

        public WeaponSlotType? GetActiveSlot()
            => _weapon?.GetActiveWeaponSlot();

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region QuickSlot

        public void AssignToQuickSlot(string instanceID, int slotIndex)
        {
            if (!Ready("QuickSlot")) return;
            _quickSlot.AssignToQuickSlot(instanceID, slotIndex);
        }

        public void AddAndAssignQuickSlot(string defID, int slotIndex)
        {
            AddItem(defID);
            var list = GetItemsByDef(defID);
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning($"[Bridge] AddAndAssign: {defID} not in inventory");
                return;
            }

            AssignToQuickSlot(list[^1].InstanceID, slotIndex);
            Debug.Log($"[Bridge] AddAndAssign {defID} → QS[{slotIndex}]");
        }

        public void RemoveFromQuickSlot(int slotIndex)
        {
            if (!Ready("QuickSlot")) return;
            if (!_quickSlot.IsSlotOccupied(slotIndex))
            {
                Debug.Log($"[Bridge] RemoveFromQuickSlot: QS[{slotIndex}] already empty");
                return;
            }

            _quickSlot.RemoveFromQuickSlot(slotIndex);
        }

        public void UseQuickSlot(int slotIndex)
        {
            if (!Ready("QuickSlot")) return;
            if (!_quickSlot.CanUseQuickSlot(slotIndex))
            {
                Debug.LogWarning($"[Bridge] UseQuickSlot: QS[{slotIndex}] cannot be used (empty or not usable)");
                return;
            }

            _quickSlot.UseQuickSlot(slotIndex);
        }

        public void CancelItemUse() => _itemUse?.CancelUse();
        public void ExecuteThrow() => _itemUse?.ExecuteThrow();

        public ItemInstance[] GetAllQuickSlots()
            => _quickSlot?.GetAllQuickSlots() ?? Array.Empty<ItemInstance>();

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region Stats

        public float GetStat(PlayerStatType t) => _stat?.GetStat(t) ?? 0f;
        public float GetBaseStat(PlayerStatType t) => _stat?.GetBaseStat(t) ?? 0f;
        public float GetStatModifier(PlayerStatType t) => _stat?.GetStatModifier(t) ?? 0f;

        public Dictionary<PlayerStatType, float> GetAllStats()
            => _stat?.GetAllStats() ?? new Dictionary<PlayerStatType, float>();

        public float GetCurrentWeight() => _stat?.GetCurrentWeight() ?? 0f;
        public float GetWeightCapacity() => _stat?.GetWeightCapacity() ?? 0f;
        public float GetWeightPercent() => _stat?.GetWeightPercent() ?? 0f;
        public float GetMovementSpeedMultiplier() => _stat?.GetMovementSpeedMultiplier() ?? 1f;

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region Scenarios

        public void ScenarioFullLoadout()
        {
            ClearInventory();
            AddItem("weapon_ak47", 1);
            var weps = GetItemsByDef("weapon_ak47");
            if (weps?.Count > 0) EquipWeapon(weps[0].InstanceID);
            AddAndEquip("armor_vest");
            AddAndEquip("armor_helmet");
            AddAndEquip("armor_backpack");
            AddItem("consumable_medkit", 5);
            var meds = GetItemsByDef("consumable_medkit");
            if (meds?.Count > 0) AssignToQuickSlot(meds[0].InstanceID, 0);
            AddItem("attachment_reddot", 1);
            Debug.Log("<color=green>[Bridge] Full loadout applied!</color>");
        }

        public void ScenarioOverweight()
        {
            ClearInventory();
            for (int i = 0; i < 20; i++) AddItem("weapon_ak47", 1);
            var w = GetWeightInfo();
            Debug.Log($"<color=yellow>[Bridge] Overweight: {w.current:F1}/{w.capacity:F1} ({w.percent:P0})</color>");
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════

        #region Helper

        private bool Ready(string system)
        {
            if (_inventory == null && _equipment == null && _weapon == null)
            {
                Debug.LogWarning($"[Bridge] Not ready ({system})");
                return false;
            }

            return true;
        }

        #endregion
    }
}