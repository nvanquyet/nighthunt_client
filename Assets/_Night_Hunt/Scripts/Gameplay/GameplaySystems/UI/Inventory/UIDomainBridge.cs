using System;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Gameplay.Spectator;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Bridge giữa gameplay backend (GameplaySystemsBridge) và UI Inventory/HUD.
    /// Lắng nghe event từ bridge và bắn event UI thân thiện với slot.
    /// </summary>
    public enum InventorySortMode
    {
        Default
    }

    public class UIDomainBridge : IDisposable
    {
        private NetworkPlayer _currentPlayer;
        private IGameplayBridge _bridge;

        public bool IsReady => _bridge != null && _bridge.IsReady;
        public IGameplayBridge Bridge => _bridge;

        #region UI Events

        public event Action<UISlotId, UISlotState> OnInventorySlotChanged;
        public event Action<UISlotId, UISlotState> OnEquipmentSlotChanged;
        public event Action<UISlotId, UISlotState> OnWeaponSlotChanged;
        public event Action<UISlotId, UISlotState> OnQuickSlotChanged;

        public event Action<PlayerStatType, float, float> OnStatChanged;
        public event Action<float, float> OnWeightChanged;

        #endregion

        #region Init / Dispose

        public void InitializeForCurrentPlayer()
        {
            var spectate = SpectateManager.Instance;
            if (spectate == null)
            {
                UnityEngine.Debug.LogWarning("[UIDomainBridge] SpectateManager not available.");
                return;
            }

            _currentPlayer = spectate.GetCurrentPlayer();
            if (_currentPlayer == null)
            {
                UnityEngine.Debug.LogWarning("[UIDomainBridge] Current player is null.");
                return;
            }

            _bridge = _currentPlayer.GamePlaySystemBridge;
            if (_bridge == null || !_bridge.IsReady)
            {
                UnityEngine.Debug.LogWarning("[UIDomainBridge] GameplaySystemsBridge not ready.");
                return;
            }

            WireEvents();

            // Push initial snapshot cho UI.
            PushInitialInventorySnapshot();
            PushInitialEquipmentSnapshot();
            PushInitialWeaponSnapshot();
            PushInitialQuickSlotSnapshot();
            PushInitialStatsSnapshot();
        }

        public void Dispose()
        {
            UnwireEvents();
            _bridge = null;
            _currentPlayer = null;
        }

        #endregion

        #region Event Wiring

        private void WireEvents()
        {
            if (_bridge == null) return;

            _bridge.OnItemAdded += HandleItemChangedInventory;
            _bridge.OnItemRemoved += HandleItemRemoved;
            _bridge.OnItemsSwapped += HandleItemsSwapped;
            _bridge.OnInventoryCleared += HandleInventoryCleared;

            _bridge.OnItemEquipped += HandleItemEquipped;
            _bridge.OnItemUnequipped += HandleItemUnequipped;

            _bridge.OnWeaponEquipped += HandleWeaponEquipped;
            _bridge.OnWeaponUnequipped += HandleWeaponUnequipped;
            _bridge.OnActiveWeaponChanged += HandleActiveWeaponChanged;

            _bridge.OnQuickSlotAssigned += HandleQuickSlotAssigned;
            _bridge.OnQuickSlotRemoved += HandleQuickSlotRemoved;

            _bridge.OnStatChanged += HandleStatChanged;
            _bridge.OnWeightChanged += HandleWeightChanged;
        }

        private void UnwireEvents()
        {
            if (_bridge == null) return;

            _bridge.OnItemAdded -= HandleItemChangedInventory;
            _bridge.OnItemRemoved -= HandleItemRemoved;
            _bridge.OnItemsSwapped -= HandleItemsSwapped;
            _bridge.OnInventoryCleared -= HandleInventoryCleared;

            _bridge.OnItemEquipped -= HandleItemEquipped;
            _bridge.OnItemUnequipped -= HandleItemUnequipped;

            _bridge.OnWeaponEquipped -= HandleWeaponEquipped;
            _bridge.OnWeaponUnequipped -= HandleWeaponUnequipped;
            _bridge.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

            _bridge.OnQuickSlotAssigned -= HandleQuickSlotAssigned;
            _bridge.OnQuickSlotRemoved -= HandleQuickSlotRemoved;

            _bridge.OnStatChanged -= HandleStatChanged;
            _bridge.OnWeightChanged -= HandleWeightChanged;
        }

        #endregion

        #region Initial Snapshots

        private void PushInitialInventorySnapshot()
        {
            if (_bridge == null || _bridge.Inventory == null) return;

            IReadOnlyList<ItemInstance> items = _bridge.GetAllItems();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null) continue;
                if (item.InventoryIndex < 0) continue;

                var id = UISlotId.Inventory(item.InventoryIndex);
                var state = BuildSlotStateFromItem(item);
                OnInventorySlotChanged?.Invoke(id, state);
            }
        }

        private void PushInitialEquipmentSnapshot()
        {
            if (_bridge == null || _bridge.Equipment == null) return;

            var dict = _bridge.GetAllEquipped();
            if (dict == null) return;

            foreach (var kvp in dict)
            {
                var id = UISlotId.Equipment(kvp.Key);
                var state = BuildSlotStateFromItem(kvp.Value);
                OnEquipmentSlotChanged?.Invoke(id, state);
            }
        }

        private void PushInitialWeaponSnapshot()
        {
            if (_bridge == null || _bridge.Weapon == null) return;

            var dict = _bridge.GetAllWeapons();
            if (dict == null) return;

            foreach (var kvp in dict)
            {
                var id = UISlotId.Weapon(kvp.Key);
                var state = BuildSlotStateFromItem(kvp.Value);
                OnWeaponSlotChanged?.Invoke(id, state);
            }
        }

        private void PushInitialQuickSlotSnapshot()
        {
            if (_bridge == null || _bridge.QuickSlot == null) return;

            var slots = _bridge.GetAllQuickSlots();
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                var item = slots[i];
                if (item == null) continue;

                var id = UISlotId.QuickSlot(i);
                var state = BuildSlotStateFromItem(item);
                OnQuickSlotChanged?.Invoke(id, state);
            }
        }

        private void PushInitialStatsSnapshot()
        {
            if (_bridge == null || _bridge.Stat == null) return;

            var all = _bridge.GetAllStats();
            if (all == null) return;

            foreach (var kvp in all)
            {
                OnStatChanged?.Invoke(kvp.Key, kvp.Value, kvp.Value);
            }

            float currentWeight = _bridge.GetCurrentWeight();
            float capacity = _bridge.GetWeightCapacity();
            OnWeightChanged?.Invoke(currentWeight, capacity);
        }

        #endregion

        #region Event Handlers

        private void HandleItemChangedInventory(ItemInstance item)
        {
            if (item == null || item.InventoryIndex < 0) return;

            var id = UISlotId.Inventory(item.InventoryIndex);
            var state = BuildSlotStateFromItem(item);
            OnInventorySlotChanged?.Invoke(id, state);
        }

        private void HandleItemRemoved(ItemInstance item, int removedQty)
        {
            if (item == null || item.InventoryIndex < 0) return;

            if (item.Quantity <= 0)
            {
                var id = UISlotId.Inventory(item.InventoryIndex);
                OnInventorySlotChanged?.Invoke(id, new UISlotState());
            }
            else
            {
                HandleItemChangedInventory(item);
            }
        }

        private void HandleItemsSwapped(ItemInstance item1, ItemInstance item2)
        {
            if (item1 != null && item1.InventoryIndex >= 0)
            {
                HandleItemChangedInventory(item1);
            }

            if (item2 != null && item2.InventoryIndex >= 0)
            {
                HandleItemChangedInventory(item2);
            }
        }

        private void HandleInventoryCleared()
        {
            // UI layer có thể tự clear tất cả cell khi nhận event này.
        }

        private void HandleItemEquipped(EquipmentSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Equipment(slot);
            var state = BuildSlotStateFromItem(item);
            OnEquipmentSlotChanged?.Invoke(id, state);
        }

        private void HandleItemUnequipped(EquipmentSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Equipment(slot);
            OnEquipmentSlotChanged?.Invoke(id, new UISlotState());
        }

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Weapon(slot);
            var state = BuildSlotStateFromItem(item);
            OnWeaponSlotChanged?.Invoke(id, state);
        }

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Weapon(slot);
            OnWeaponSlotChanged?.Invoke(id, new UISlotState());
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            // UI có thể highlight weapon đang active – ở đây chỉ phát lại state hiện có.
            if (oldSlot.HasValue)
            {
                var id = UISlotId.Weapon(oldSlot.Value);
                OnWeaponSlotChanged?.Invoke(id, new UISlotState());
            }

            if (newSlot.HasValue && _bridge != null && _bridge.Weapon != null)
            {
                var dict = _bridge.GetAllWeapons();
                if (dict != null && dict.TryGetValue(newSlot.Value, out var item))
                {
                    var id = UISlotId.Weapon(newSlot.Value);
                    var state = BuildSlotStateFromItem(item);
                    state.IsHighlight = true;
                    OnWeaponSlotChanged?.Invoke(id, state);
                }
            }
        }

        private void HandleQuickSlotAssigned(int slotIndex, ItemInstance item)
        {
            var id = UISlotId.QuickSlot(slotIndex);
            var state = BuildSlotStateFromItem(item);
            OnQuickSlotChanged?.Invoke(id, state);
        }

        private void HandleQuickSlotRemoved(int slotIndex)
        {
            var id = UISlotId.QuickSlot(slotIndex);
            OnQuickSlotChanged?.Invoke(id, new UISlotState());
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            OnStatChanged?.Invoke(type, oldValue, newValue);
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            OnWeightChanged?.Invoke(current, capacity);
        }

        #endregion

        #region Inventory Sorting

        /// <summary>
        /// Yêu cầu sắp xếp inventory trên server bằng cách gọi MoveItem theo index mới.
        /// </summary>
        public void RequestSortInventory(InventorySortMode mode = InventorySortMode.Default)
        {
            if (_bridge == null || !_bridge.IsReady || _bridge.Inventory == null)
                return;

            var items = _bridge.GetAllItems();
            if (items == null)
                return;

            // Lọc item đang nằm trong grid inventory
            var list = items
                .Where(i => i != null && i.InventoryIndex >= 0)
                .ToList();

            if (list.Count <= 1)
                return;

            // Sort đơn giản: theo ItemType rồi ItemID
            list.Sort((a, b) =>
            {
                var defA = ItemDatabase.GetDefinition(a.DefinitionID);
                var defB = ItemDatabase.GetDefinition(b.DefinitionID);

                int typeA = defA != null ? (int)defA.Type : 0;
                int typeB = defB != null ? (int)defB.Type : 0;

                int cmp = typeA.CompareTo(typeB);
                if (cmp != 0) return cmp;

                string idA = defA != null ? defA.ItemID : a.DefinitionID;
                string idB = defB != null ? defB.ItemID : b.DefinitionID;
                return string.Compare(idA, idB, StringComparison.Ordinal);
            });

            // Gửi chuỗi MoveItem theo thứ tự mới
            for (int newIndex = 0; newIndex < list.Count; newIndex++)
            {
                var item = list[newIndex];
                if (item.InventoryIndex == newIndex)
                    continue;

                _bridge.Inventory.MoveItem(item.InstanceID, newIndex);
            }
        }

        #endregion

        #region Helpers

        private UISlotState BuildSlotStateFromItem(ItemInstance item)
        {
            if (item == null)
                return new UISlotState();

            var def = ItemDatabase.GetDefinition(item.DefinitionID);

            var state = new UISlotState
            {
                Item = item,
                StackCount = item.Quantity,
                Icon = def != null ? def.Icon : null,
                BackgroundColor = UnityEngine.Color.white
            };

            return state;
        }

        #endregion
    }
}

