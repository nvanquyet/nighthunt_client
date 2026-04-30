using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.UI.Inventory;
using NightHunt.Networking.Player;

namespace NightHunt.UI
{
    /// <summary>
    /// UIPlayerContext — single event bridge between gameplay systems and all UI panels.
    ///
    /// REPLACES: <see cref="NightHunt.GameplaySystems.UI.Inventory.UIDomainBridge"/> (now obsolete).
    ///
    /// KEY IMPROVEMENTS over UIDomainBridge:
    ///   • Explicit Bind/Unbind lifecycle — no hidden SpectateManager dependency.
    ///   • Exposes all IGameplayBridge events, including item-use progress + active-weapon-changed.
    ///   • Drop-in compatible: exposes the same slot events as UIDomainBridge, plus extras.
    ///
    /// USAGE (by GameHUDController):
    ///   context.Bind(player);           // on local player spawn or spectate switch
    ///   // panels subscribe to context events in their Bind() / Initialize() calls
    ///   context.Unbind();               // before switching to a different player
    ///   context.Dispose();              // on destroy (same as Unbind)
    ///
    /// THREAD SAFETY: All events fire on the Unity main thread (guaranteed by FishNet).
    /// </summary>
    public sealed class UIPlayerContext : IDisposable
    {
        private NetworkPlayer _player;
        private IGameplayBridge _bridge;

        // ── Public read-only state ───────────────────────────────────────────

        /// <summary>True when a player is bound and the gameplay bridge is ready.</summary>
        public bool IsReady => _bridge != null && _bridge.IsReady;

        /// <summary>Direct access to the underlying gameplay bridge for advanced operations.</summary>
        public IGameplayBridge Bridge => _bridge;

        /// <summary>Currently bound player (may be local player or spectated player).</summary>
        public NetworkPlayer Player => _player;

        /// <summary>True when the bound player is owned by the local machine (not spectating).</summary>
        public bool IsOwner => _player != null && _player.IsOwner;

        // ── Inventory / Equipment / Weapon slot events (same API as UIDomainBridge) ──

        /// <summary>
        /// Fires when any inventory slot changes (item added, removed, moved, swapped).
        /// Parameters: (slotId, newState) — newState.Item is null when slot is cleared.
        /// </summary>
        public event Action<UISlotId, UISlotState> OnInventorySlotChanged;

        /// <summary>Fires when any equipment slot changes (equip/unequip).</summary>
        public event Action<UISlotId, UISlotState> OnEquipmentSlotChanged;

        /// <summary>
        /// Fires when any weapon slot changes, including IsHighlight when active weapon changes.
        /// </summary>
        public event Action<UISlotId, UISlotState> OnWeaponSlotChanged;

        // ── Item selection events ────────────────────────────────────────────

        /// <summary>An item was selected for use (consumable / throwable activated).</summary>
        public event Action<ItemInstance> OnItemSelected;

        /// <summary>The selected item was deselected without use.</summary>
        public event Action OnItemDeselected;

        // ── Item use progress events (NEW vs UIDomainBridge) ─────────────────

        /// <summary>Item use started — show progress bar. Parameters: item being used.</summary>
        public event Action<ItemInstance> OnItemUseStarted;

        /// <summary>
        /// Item use progress tick. Parameters: (item, progress 0→1).
        /// Fire at ~10 Hz from the server; drive progress bar fill.
        /// </summary>
        public event Action<ItemInstance, float> OnItemUseProgress;

        /// <summary>Item use completed successfully — hide progress bar.</summary>
        public event Action<ItemInstance> OnItemUseCompleted;

        /// <summary>Item use cancelled by player or server — hide progress bar.</summary>
        public event Action<ItemInstance> OnItemUseCancelled;

        // ── Stat events ──────────────────────────────────────────────────────

        /// <summary>A player stat changed. Parameters: (type, oldValue, newValue).</summary>
        public event Action<PlayerStatType, float, float> OnStatChanged;

        /// <summary>Carry weight changed. Parameters: (currentWeight, maxCapacity).</summary>
        public event Action<float, float> OnWeightChanged;

        /// <summary>
        /// Computed: overweight threshold crossed.
        /// Parameters: (isOverweight, ratio = currentWeight/maxCapacity).
        /// </summary>
        public event Action<bool, float> OnOverweightChanged;

        // ── Weapon events (NEW vs UIDomainBridge) ────────────────────────────

        /// <summary>
        /// Raw active-weapon-changed event forwarded from IGameplayBridge.
        /// Parameters: (oldSlot nullable, newSlot nullable).
        /// WeaponHUDPanel uses this to set/clear the active highlight.
        /// </summary>
        public event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;

        // ── Lifecycle ────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this context to a player. Subscribes to all bridge events and
        /// immediately pushes the current state snapshot to all listeners.
        /// Safe to call again to re-bind to a different player (auto-unbinds first).
        /// </summary>
        public void Bind(NetworkPlayer player)
        {
            if (_player != null) Unbind();

            _player = player;
            if (player == null) return;

            _bridge = player.GamePlaySystemBridge;
            if (_bridge == null || !_bridge.IsReady)
            {
                UnityEngine.Debug.LogWarning($"[UIPlayerContext] Bridge not ready for player '{player.name}'. " +
                                             "UI will stay empty until next Bind.");
                _bridge = null;
                return;
            }

            WireEvents();
            PushSnapshot();
        }

        /// <summary>
        /// Unbind from the current player. Unsubscribes all bridge events.
        /// Does NOT clear listener subscriptions on this context — panels keep their subscriptions.
        /// </summary>
        public void Unbind()
        {
            UnwireEvents();
            _player = null;
            _bridge = null;
        }

        /// <inheritdoc cref="Unbind"/>
        public void Dispose() => Unbind();

        // ── Snapshot API (used by panels after layout rebuild) ───────────────

        /// <summary>
        /// Re-fire the full current state to all listeners.
        /// Call this after any panel clears its slots (e.g. after inventory sort).
        /// </summary>
        public void PushSnapshot()
        {
            PushInventorySnapshot();
            PushEquipmentSnapshot();
            PushWeaponSnapshot();
            PushStatsSnapshot();
        }

        /// <summary>Re-fire only inventory slot states (e.g. after grid rebuild).</summary>
        public void PushInventorySnapshot()
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[INV] PushInventorySnapshot: not ready. bridge={(_bridge != null ? "ok" : "NULL")} bridge.IsReady={_bridge?.IsReady}");
                return;
            }
            var items = _bridge.GetAllItems();
            if (items == null)
            {
                Debug.LogWarning("[INV] PushInventorySnapshot: GetAllItems() returned null.");
                return;
            }
            int count = 0;
            foreach (var item in items)
            {
                if (item == null || item.InventoryIndex < 0) continue;
                OnInventorySlotChanged?.Invoke(UISlotId.Inventory(item.InventoryIndex), BuildSlotState(item));
                count++;
            }
            Debug.Log($"[INV] PushInventorySnapshot: pushed {count} item(s) to slots.");
        }

        /// <summary>Route an inventory sort request to the server.</summary>
        public void RequestSortInventory()
        {
            _bridge?.Inventory?.RequestSortByType();
        }

        // ── Event Wiring ─────────────────────────────────────────────────────

        private void WireEvents()
        {
            if (_bridge == null) return;

            // Inventory
            _bridge.OnItemAdded             += HandleItemAdded;
            _bridge.OnItemRemoved           += HandleItemRemoved;
            _bridge.OnItemsSwapped          += HandleItemsSwapped;
            _bridge.OnItemMoved             += HandleItemMoved;
            _bridge.OnInventoryCleared      += HandleInventoryCleared;
            _bridge.OnInventorySlotCleared  += HandleInventorySlotCleared;

            // Equipment
            _bridge.OnItemEquipped          += HandleItemEquipped;
            _bridge.OnItemUnequipped        += HandleItemUnequipped;

            // Weapon
            _bridge.OnWeaponEquipped        += HandleWeaponEquipped;
            _bridge.OnWeaponUnequipped      += HandleWeaponUnequipped;
            _bridge.OnActiveWeaponChanged   += HandleActiveWeaponChanged;

            // Item selection
            _bridge.OnItemSelected          += HandleItemSelected;
            _bridge.OnItemDeselected        += HandleItemDeselected;

            // Item use
            _bridge.OnItemUseStarted        += HandleItemUseStarted;
            _bridge.OnItemUseProgress       += HandleItemUseProgress;
            _bridge.OnItemUseCompleted      += HandleItemUseCompleted;
            _bridge.OnItemUseCancelled      += HandleItemUseCancelled;

            // Stats
            _bridge.OnStatChanged           += HandleStatChanged;
            _bridge.OnWeightChanged         += HandleWeightChanged;
        }

        private void UnwireEvents()
        {
            if (_bridge == null) return;

            _bridge.OnItemAdded             -= HandleItemAdded;
            _bridge.OnItemRemoved           -= HandleItemRemoved;
            _bridge.OnItemsSwapped          -= HandleItemsSwapped;
            _bridge.OnItemMoved             -= HandleItemMoved;
            _bridge.OnInventoryCleared      -= HandleInventoryCleared;
            _bridge.OnInventorySlotCleared  -= HandleInventorySlotCleared;

            _bridge.OnItemEquipped          -= HandleItemEquipped;
            _bridge.OnItemUnequipped        -= HandleItemUnequipped;

            _bridge.OnWeaponEquipped        -= HandleWeaponEquipped;
            _bridge.OnWeaponUnequipped      -= HandleWeaponUnequipped;
            _bridge.OnActiveWeaponChanged   -= HandleActiveWeaponChanged;

            _bridge.OnItemSelected          -= HandleItemSelected;
            _bridge.OnItemDeselected        -= HandleItemDeselected;

            _bridge.OnItemUseStarted        -= HandleItemUseStarted;
            _bridge.OnItemUseProgress       -= HandleItemUseProgress;
            _bridge.OnItemUseCompleted      -= HandleItemUseCompleted;
            _bridge.OnItemUseCancelled      -= HandleItemUseCancelled;

            _bridge.OnStatChanged           -= HandleStatChanged;
            _bridge.OnWeightChanged         -= HandleWeightChanged;
        }

        // ── Snapshot helpers ─────────────────────────────────────────────────

        private void PushEquipmentSnapshot()
        {
            if (!IsReady) return;
            var dict = _bridge.GetAllEquipped();
            if (dict == null) return;
            foreach (var kvp in dict)
                OnEquipmentSlotChanged?.Invoke(UISlotId.Equipment(kvp.Key), BuildSlotState(kvp.Value));
        }

        private void PushWeaponSnapshot()
        {
            if (!IsReady) return;
            var dict = _bridge.GetAllWeapons();
            if (dict == null) return;
            var activeSlot = _bridge.GetActiveSlot();
            foreach (var kvp in dict)
            {
                var state = BuildSlotState(kvp.Value);
                state.IsHighlight = activeSlot.HasValue && activeSlot.Value == kvp.Key;
                OnWeaponSlotChanged?.Invoke(UISlotId.Weapon(kvp.Key), state);
            }
        }

        public void PushStatsSnapshot()
        {
            if (!IsReady || _bridge.Stat == null) return;
            var all = _bridge.GetAllStats();
            if (all == null) return;
            foreach (var kvp in all)
                OnStatChanged?.Invoke(kvp.Key, kvp.Value, kvp.Value);
            OnWeightChanged?.Invoke(_bridge.GetCurrentWeight(), _bridge.GetWeightCapacity());
        }

        // ── Inventory handlers ───────────────────────────────────────────────

        private void HandleItemAdded(ItemInstance item)
        {
            if (item == null || item.InventoryIndex < 0) return;
            OnInventorySlotChanged?.Invoke(UISlotId.Inventory(item.InventoryIndex), BuildSlotState(item));
        }

        private void HandleItemRemoved(ItemInstance item, int qty)
        {
            if (item == null || item.InventoryIndex < 0) return;
            bool fullyRemoved = _bridge.Inventory?.GetItemByInstanceID(item.InstanceID) == null;
            OnInventorySlotChanged?.Invoke(
                UISlotId.Inventory(item.InventoryIndex),
                fullyRemoved ? new UISlotState() : BuildSlotState(item));
        }

        private void HandleItemsSwapped(ItemInstance a, ItemInstance b)
        {
            if (a != null && a.InventoryIndex >= 0)
                OnInventorySlotChanged?.Invoke(UISlotId.Inventory(a.InventoryIndex), BuildSlotState(a));
            if (b != null && b.InventoryIndex >= 0)
                OnInventorySlotChanged?.Invoke(UISlotId.Inventory(b.InventoryIndex), BuildSlotState(b));
        }

        private void HandleItemMoved(ItemInstance item, int oldIdx, int newIdx)
        {
            if (oldIdx >= 0)
                OnInventorySlotChanged?.Invoke(UISlotId.Inventory(oldIdx), new UISlotState());
            if (newIdx >= 0 && item != null)
                HandleItemAdded(item);
        }

        private void HandleInventoryCleared()
        {
            if (_bridge?.Inventory == null) return;
            var items = _bridge.GetAllItems();
            if (items == null) return;
            var seen = new HashSet<int>();
            foreach (var item in items)
            {
                if (item == null || item.InventoryIndex < 0 || !seen.Add(item.InventoryIndex)) continue;
                OnInventorySlotChanged?.Invoke(UISlotId.Inventory(item.InventoryIndex), new UISlotState());
            }
        }

        private void HandleInventorySlotCleared(int index)
        {
            OnInventorySlotChanged?.Invoke(UISlotId.Inventory(index), new UISlotState());
        }

        // ── Equipment handlers ───────────────────────────────────────────────

        private void HandleItemEquipped(EquipmentSlotType slot, ItemInstance item)
        {
            OnEquipmentSlotChanged?.Invoke(UISlotId.Equipment(slot), BuildSlotState(item));
        }

        private void HandleItemUnequipped(EquipmentSlotType slot, ItemInstance item)
        {
            OnEquipmentSlotChanged?.Invoke(UISlotId.Equipment(slot), new UISlotState());
            if (item != null && item.InventoryIndex >= 0) HandleItemAdded(item);
        }

        // ── Weapon handlers ──────────────────────────────────────────────────

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance item)
        {
            OnWeaponSlotChanged?.Invoke(UISlotId.Weapon(slot), BuildSlotState(item));
        }

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance item)
        {
            OnWeaponSlotChanged?.Invoke(UISlotId.Weapon(slot), new UISlotState());
            if (item != null && item.InventoryIndex >= 0) HandleItemAdded(item);
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            // De-highlight old slot (keep item visible, just clear highlight)
            if (oldSlot.HasValue)
            {
                var weapons = _bridge?.GetAllWeapons();
                UISlotState oldState;
                if (weapons != null && weapons.TryGetValue(oldSlot.Value, out var oldItem) && oldItem != null)
                {
                    oldState = BuildSlotState(oldItem);
                    oldState.IsHighlight = false;
                }
                else
                {
                    oldState = new UISlotState();
                }
                OnWeaponSlotChanged?.Invoke(UISlotId.Weapon(oldSlot.Value), oldState);
            }

            // Highlight new slot
            if (newSlot.HasValue)
            {
                var weapons = _bridge?.GetAllWeapons();
                if (weapons != null && weapons.TryGetValue(newSlot.Value, out var newItem) && newItem != null)
                {
                    var state = BuildSlotState(newItem);
                    state.IsHighlight = true;
                    OnWeaponSlotChanged?.Invoke(UISlotId.Weapon(newSlot.Value), state);
                }
            }

            OnActiveWeaponChanged?.Invoke(oldSlot, newSlot);
        }

        // ── Item selection/use handlers ──────────────────────────────────────

        private void HandleItemSelected(ItemInstance item)           => OnItemSelected?.Invoke(item);
        private void HandleItemDeselected()                          => OnItemDeselected?.Invoke();
        private void HandleItemUseStarted(ItemInstance item)         => OnItemUseStarted?.Invoke(item);
        private void HandleItemUseProgress(ItemInstance item, float p) => OnItemUseProgress?.Invoke(item, p);
        private void HandleItemUseCompleted(ItemInstance item)       => OnItemUseCompleted?.Invoke(item);
        private void HandleItemUseCancelled(ItemInstance item)       => OnItemUseCancelled?.Invoke(item);

        // ── Stat handlers ────────────────────────────────────────────────────

        private void HandleStatChanged(PlayerStatType type, float oldVal, float newVal)
        {
            OnStatChanged?.Invoke(type, oldVal, newVal);
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            OnWeightChanged?.Invoke(current, capacity);
            float ratio = capacity > 0f ? current / capacity : 0f;
            OnOverweightChanged?.Invoke(ratio >= 1f, ratio);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static UISlotState BuildSlotState(ItemInstance item)
        {
            if (item == null) return new UISlotState();
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            return new UISlotState
            {
                Item             = item,
                StackCount       = item.Quantity,
                Icon             = def?.Icon,
                Background  = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance.DefaultSlotBackground,
            };
        }
    }
}
