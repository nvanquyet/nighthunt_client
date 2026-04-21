using System;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Gameplay.Spectator;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Bridge giữa gameplay backend (GameplaySystemsBridge) và UI Inventory/HUD.
    /// Listens for event từ bridge và bắn event UI thân thiện với slot.
    /// </summary>
    public enum InventorySortMode
    {
        Default
    }

    public class UIDomainBridge : IDisposable
    {
        private NetworkPlayer _currentPlayer;
        private IGameplayBridge _bridge;

        private bool _enableDebugLogs = false;

        public bool IsReady => _bridge != null && _bridge.IsReady;
        public IGameplayBridge Bridge => _bridge;
        public NetworkPlayer CurrentPlayer => _currentPlayer;

        /// <summary>
        /// True when the currently viewed player is owned by the local machine.
        /// Used by InventoryScreen to block all interactions in spectator mode.
        /// </summary>
        public bool IsCurrentPlayerOwner => _currentPlayer != null && _currentPlayer.IsOwner;

        #region UI Events

        public event Action<UISlotId, UISlotState> OnInventorySlotChanged;
        public event Action<UISlotId, UISlotState> OnEquipmentSlotChanged;
        public event Action<UISlotId, UISlotState> OnWeaponSlotChanged;
        public event Action<ItemInstance> OnItemSelected;
        public event Action OnItemDeselected;

        public event Action<PlayerStatType, float, float> OnStatChanged;
        public event Action<float, float> OnWeightChanged;

        /// <summary>
        /// Fires whenever the overweight state changes.
        /// Parameters: (isOverweight, currentRatio)
        /// Subscribe in PlayerStatPanel to update the weight bar color and tier label.
        /// </summary>
        public event Action<bool, float> OnOverweightChanged;

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
            PushInitialStatsSnapshot();
        }

        public void Dispose()
        {
            UnwireEvents();
            _bridge = null;
            _currentPlayer = null;
        }

        /// <summary>
        /// ROOT CAUSE D FIX: Re-push all current backend state to UI slots.
        /// Call this after any operation that clears UI (e.g. RefreshAllSlots / sort reset)
        /// because events only fire on CHANGES, not on demand.
        /// </summary>
        public void Refresh()
        {
            PushInitialInventorySnapshot();
            PushInitialEquipmentSnapshot();
            PushInitialWeaponSnapshot();
        }

        #endregion

        #region Event Wiring

        private void WireEvents()
        {
            if (_bridge == null) return;

            _bridge.OnItemAdded += HandleItemChangedInventory;
            _bridge.OnItemRemoved += HandleItemRemoved;
            _bridge.OnItemsSwapped += HandleItemsSwapped;
            _bridge.OnItemMoved += HandleItemMoved;
            _bridge.OnInventoryCleared += HandleInventoryCleared;
            _bridge.OnInventorySlotCleared += HandleInventorySlotCleared;

            _bridge.OnItemEquipped += HandleItemEquipped;
            _bridge.OnItemUnequipped += HandleItemUnequipped;

            _bridge.OnWeaponEquipped += HandleWeaponEquipped;
            _bridge.OnWeaponUnequipped += HandleWeaponUnequipped;
            _bridge.OnActiveWeaponChanged += HandleActiveWeaponChanged;

            _bridge.OnItemSelected  += HandleItemSelected;
            _bridge.OnItemDeselected += HandleItemDeselected;

            _bridge.OnStatChanged += HandleStatChanged;
            _bridge.OnWeightChanged += HandleWeightChanged;
        }

        private void UnwireEvents()
        {
            if (_bridge == null) return;

            _bridge.OnItemAdded -= HandleItemChangedInventory;
            _bridge.OnItemRemoved -= HandleItemRemoved;
            _bridge.OnItemsSwapped -= HandleItemsSwapped;
            _bridge.OnItemMoved -= HandleItemMoved;
            _bridge.OnInventoryCleared -= HandleInventoryCleared;
            _bridge.OnInventorySlotCleared -= HandleInventorySlotCleared;

            _bridge.OnItemEquipped -= HandleItemEquipped;
            _bridge.OnItemUnequipped -= HandleItemUnequipped;

            _bridge.OnWeaponEquipped -= HandleWeaponEquipped;
            _bridge.OnWeaponUnequipped -= HandleWeaponUnequipped;
            _bridge.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

            _bridge.OnItemSelected  -= HandleItemSelected;
            _bridge.OnItemDeselected -= HandleItemDeselected;

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

        private void PushInitialStatsSnapshot()
        {
            if (_bridge == null || _bridge.Stat == null)
            {
                if (_enableDebugLogs)
                    UnityEngine.Debug.LogWarning("[UIDomainBridge] PushInitialStatsSnapshot: bridge or stat system is null");
                return;
            }

            var all = _bridge.GetAllStats();
            if (all == null)
            {
                if (_enableDebugLogs)
                    UnityEngine.Debug.LogWarning("[UIDomainBridge] PushInitialStatsSnapshot: GetAllStats returned null");
                return;
            }

            if (_enableDebugLogs)
                UnityEngine.Debug.Log($"[UIDomainBridge] PushInitialStatsSnapshot: pushing {all.Count} stats");

            foreach (var kvp in all)
            {
                OnStatChanged?.Invoke(kvp.Key, kvp.Value, kvp.Value);
            }

            float currentWeight = _bridge.GetCurrentWeight();
            float capacity = _bridge.GetWeightCapacity();
            if (_enableDebugLogs)
                UnityEngine.Debug.Log($"[UIDomainBridge] PushInitialStatsSnapshot: Weight = {currentWeight:F1}/{capacity:F1}");
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

            // Check via the cache: RemoveItemCompletely already evicted the item before firing
            // OnItemRemoved, so GetItemByInstanceID returns null for a full removal.
            // item.Quantity is NOT decremented in the full-removal path, so checking
            // item.Quantity <= 0 would be incorrect — always use the cache presence instead.
            bool fullyRemoved = _bridge.Inventory?.GetItemByInstanceID(item.InstanceID) == null;
            if (fullyRemoved)
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
            // Clear all currently-known inventory slots so UI doesn't show stale items
            // after a sort or bulk-remove. The sort flow fires OnItemAdded immediately
            // after for all items that moved, so this is a safe two-pass approach.
            if (_bridge?.Inventory == null) return;

            var items = _bridge.GetAllItems();
            if (items == null) return;

            var seenIndices = new System.Collections.Generic.HashSet<int>();
            foreach (var item in items)
            {
                if (item == null || item.InventoryIndex < 0) continue;
                if (!seenIndices.Add(item.InventoryIndex)) continue;

                var id = UISlotId.Inventory(item.InventoryIndex);
                OnInventorySlotChanged?.Invoke(id, new UISlotState());
            }
        }

        /// <summary>
        /// BUG 7 FIX: Clear inventory slot when item moves out of inventory grid
        /// (equipped to Equipment/Weapon slot, or attached to another item).
        /// </summary>
        private void HandleInventorySlotCleared(int index)
        {
            var id = UISlotId.Inventory(index);
            OnInventorySlotChanged?.Invoke(id, new UISlotState());
        }

        private void HandleItemEquipped(EquipmentSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Equipment(slot);
            var state = BuildSlotStateFromItem(item);
            OnEquipmentSlotChanged?.Invoke(id, state);

            if (_enableDebugLogs)
            {
                UnityEngine.Debug.Log($"[UIDomainBridge] [Server→UI][ItemEquipped] " +
                                      $"slot={slot}, item={item?.DefinitionID ?? "null"} ({item?.InstanceID ?? "null"})");
            }

            // Spawn / attach equipment model on character when character model system is ready.
            // e.g. CharacterAppearance.AttachEquipment(slot, item.DefinitionID);
        }

        private void HandleItemUnequipped(EquipmentSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Equipment(slot);
            OnEquipmentSlotChanged?.Invoke(id, new UISlotState());

            // FIX: Update inventory slot nếu item được add vào inventory
            if (item != null && item.InventoryIndex >= 0)
            {
                HandleItemChangedInventory(item);
            }

            if (_enableDebugLogs)
            {
                UnityEngine.Debug.Log($"[UIDomainBridge] [Server→UI][ItemUnequipped] " +
                                      $"slot={slot}, returnedToInventoryIndex={item?.InventoryIndex.ToString() ?? "null"} " +
                                      $"item={item?.DefinitionID ?? "null"} ({item?.InstanceID ?? "null"})");
            }

            // Despawn / detach equipment model when character model system is ready.
        }

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Weapon(slot);
            var state = BuildSlotStateFromItem(item);
            OnWeaponSlotChanged?.Invoke(id, state);

            if (_enableDebugLogs)
            {
                UnityEngine.Debug.Log($"[UIDomainBridge] [Server→UI][WeaponEquipped] " +
                                      $"slot={slot}, item={item?.DefinitionID ?? "null"} ({item?.InstanceID ?? "null"})");
            }

            // Spawn weapon viewmodel / world model when weapon visual system is ready.
        }

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance item)
        {
            var id = UISlotId.Weapon(slot);
            OnWeaponSlotChanged?.Invoke(id, new UISlotState());

            // FIX: Update inventory slot nếu item được add vào inventory
            if (item != null && item.InventoryIndex >= 0)
            {
                HandleItemChangedInventory(item);
            }

            if (_enableDebugLogs)
            {
                UnityEngine.Debug.Log($"[UIDomainBridge] [Server→UI][WeaponUnequipped] " +
                                      $"slot={slot}, returnedToInventoryIndex={item?.InventoryIndex.ToString() ?? "null"} " +
                                      $"item={item?.DefinitionID ?? "null"} ({item?.InstanceID ?? "null"})");
            }

            // Despawn weapon viewmodel / world model when weapon visual system is ready.
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            // Re-broadcast old slot WITHOUT highlight – do NOT send new UISlotState() which erases the item.
            if (oldSlot.HasValue)
            {
                var id = UISlotId.Weapon(oldSlot.Value);
                var allWeapons = _bridge?.GetAllWeapons();
                UISlotState oldState;
                if (allWeapons != null && allWeapons.TryGetValue(oldSlot.Value, out var oldItem) && oldItem != null)
                {
                    // Keep the weapon visible, just remove the active highlight.
                    oldState = BuildSlotStateFromItem(oldItem);
                    oldState.IsHighlight = false;
                }
                else
                {
                    // Slot is genuinely empty now.
                    oldState = new UISlotState();
                }
                OnWeaponSlotChanged?.Invoke(id, oldState);
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

        private void HandleItemSelected(ItemInstance item) => OnItemSelected?.Invoke(item);
        private void HandleItemDeselected() => OnItemDeselected?.Invoke();

        /// <summary>
        /// Handle item moved event - update cả old và new slot
        /// </summary>
        private void HandleItemMoved(ItemInstance item, int oldIndex, int newIndex)
        {
            // Update old slot về empty
            if (oldIndex >= 0)
            {
                var oldId = UISlotId.Inventory(oldIndex);
                OnInventorySlotChanged?.Invoke(oldId, new UISlotState());
            }

            // Update new slot với item data
            if (newIndex >= 0 && item != null)
            {
                HandleItemChangedInventory(item);
            }
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (_enableDebugLogs || type == PlayerStatType.CurrentWeight || type == PlayerStatType.WeightCapacity)
            {
                UnityEngine.Debug.Log($"[UIDomainBridge #{GetHashCode()}] HandleStatChanged: {type} {oldValue:F1} -> {newValue:F1}");
            }

            OnStatChanged?.Invoke(type, oldValue, newValue);
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            OnWeightChanged?.Invoke(current, capacity);

            // Fire overweight event so UI weight bar can react to threshold changes.
            float ratio = capacity > 0f ? current / capacity : 0f;
            bool isOverweight = ratio >= 1.0f;
            OnOverweightChanged?.Invoke(isOverweight, ratio);
        }

        #endregion

        #region Inventory Sorting

        /// <summary>
        /// Yêu cầu sắp xếp inventory theo ItemType rồi DefinitionID trên server.
        /// Routes qua ServerRpc để hoạt động từ cả client lẫn host.
        /// </summary>
        public void RequestSortInventory(InventorySortMode mode = InventorySortMode.Default)
        {
            if (_bridge == null || !_bridge.IsReady || _bridge.Inventory == null)
                return;

            _bridge.Inventory.RequestSortByType();
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

