using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.QuickSlot
{
    /// <summary>
    /// Manages quick-slot bindings for a networked player, routing item use to
    /// <see cref="IItemUseSystem"/>. Auto-clears slots when bound items are consumed.
    /// </summary>
    public class QuickSlotSystem : NetworkBehaviour, IQuickSlotSystem, IDisposable
    {
        #region Serialized Fields

        [Header("Configuration")] [SerializeField]
        private InventoryConfig _inventoryConfig;

        [Header("References")] [SerializeField]
        private MonoBehaviour _inventorySystemComponent;

        [SerializeField] private MonoBehaviour _itemUseSystemComponent;
        private IInventorySystem _inventorySystem;
        private IItemUseSystem _itemUseSystem;

        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        #endregion

        #region Network Synced Data

        private readonly SyncList<string> _quickSlots = new SyncList<string>();

        #endregion

        #region Tracked Subscriptions

        private System.Collections.Generic.Dictionary<int, Action<ItemInstance>> _activeCompletionHandlers =
            new System.Collections.Generic.Dictionary<int, Action<ItemInstance>>();

        #endregion

        #region Events

        public event Action<int, ItemInstance> OnQuickSlotAssigned;
        public event Action<int> OnQuickSlotRemoved;
        public event Action<int, ItemInstance> OnQuickSlotUsed;

        #endregion

        #region NetworkBehaviour Lifecycle

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _quickSlots.OnChange += OnQuickSlotsChanged;

            if (IsServerInitialized)
                InitializeQuickSlots();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            _quickSlots.OnChange -= OnQuickSlotsChanged;

            // CRITICAL: Cleanup all event subscriptions
            CleanupAllEventHandlers();
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            // Unsubscribe from network events
            _quickSlots.OnChange -= OnQuickSlotsChanged;

            // Cleanup all event handlers
            CleanupAllEventHandlers();
        }

        #endregion

        #region Initialization

        private void Awake()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            // Get components and cast to interfaces
            if (_inventorySystemComponent != null)
                _inventorySystem = _inventorySystemComponent as IInventorySystem;

            if (_itemUseSystemComponent != null)
                _itemUseSystem = _itemUseSystemComponent as IItemUseSystem;

            // Auto-find if not assigned
            if (_inventorySystem == null)
            {
                var invSys = ComponentResolver.Find<IInventorySystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IInventorySystem not found")
                    .Resolve();
                if (invSys != null)
                {
                    _inventorySystemComponent = invSys as MonoBehaviour;
                    _inventorySystem = invSys;
                }
            }

            if (_itemUseSystem == null)
            {
                var itemUseSys = ComponentResolver.Find<IItemUseSystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IItemUseSystem not found")
                    .Resolve();
                if (itemUseSys != null)
                {
                    _itemUseSystemComponent = itemUseSys as MonoBehaviour;
                    _itemUseSystem = itemUseSys;
                }
            }

            if (_inventoryConfig == null)
                Debug.LogError("[QuickSlotSystem] InventoryConfig is null!");

            if (_inventorySystem == null)
                Debug.LogError("[QuickSlotSystem] IInventorySystem is null!");

            if (_itemUseSystem == null)
                Debug.LogWarning("[QuickSlotSystem] IItemUseSystem is null - item usage will not work!");
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate()
        {
            if (_inventorySystemComponent != null)
                _inventorySystem = _inventorySystemComponent as IInventorySystem;

            if (_itemUseSystemComponent != null)
                _itemUseSystem = _itemUseSystemComponent as IItemUseSystem;

            if (_inventorySystem == null)
            {
                var invSys = ComponentResolver.Find<IInventorySystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IInventorySystem not found")
                    .Resolve();
                if (invSys != null)
                {
                    _inventorySystemComponent = invSys as MonoBehaviour;
                    _inventorySystem = invSys;
                }
            }

            if (_itemUseSystem == null)
            {
                var itemUseSys = ComponentResolver.Find<IItemUseSystem>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] IItemUseSystem not found")
                    .Resolve();
                if (itemUseSys != null)
                {
                    _itemUseSystemComponent = itemUseSys as MonoBehaviour;
                    _itemUseSystem = itemUseSys;
                }
            }
        }
#endif

        [Server]
        private void InitializeQuickSlots()
        {
            int slotCount = _inventoryConfig != null ? _inventoryConfig.QuickSlotConfig.SlotCount : 4;

            if (slotCount <= 0)
            {
                Debug.LogError("[QuickSlotSystem] SlotCount = 0 after config lookup! " +
                               "Fix: open your InventoryConfig ScriptableObject â†’ QuickSlotConfig â†’ SlotCount " +
                               "and set it to the number of quickslot buttons on your HUD (e.g. 3). Falling back to 4.");
                slotCount = 4;
            }

            _quickSlots.Clear();
            for (int i = 0; i < slotCount; i++)
                _quickSlots.Add(string.Empty);

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[QuickSlotSystem] InitializeQuickSlots: initialised {slotCount} slots.");
        }

        #endregion

        #region IQuickSlotSystem - Getters

        public ItemInstance GetQuickSlotItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
                return null;

            string instanceID = _quickSlots[slotIndex];
            if (string.IsNullOrEmpty(instanceID))
                return null;

            return _inventorySystem?.GetItemByInstanceID(instanceID);
        }

        public ItemInstance[] GetAllQuickSlots()
        {
            var slots = new ItemInstance[_quickSlots.Count];

            for (int i = 0; i < _quickSlots.Count; i++)
                slots[i] = GetQuickSlotItem(i);

            return slots;
        }

        public bool IsSlotOccupied(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
                return false;

            return !string.IsNullOrEmpty(_quickSlots[slotIndex]);
        }

        public bool CanPlaceInQuickSlot(string itemDefinitionID)
        {
            if (_inventoryConfig == null)
                return false;

            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return false;

            // Also require the item definition to explicitly allow QuickSlot placement.
            // This keeps data consistent with ValidSlots in ItemDefinition assets.
            if (!itemDef.CanPlaceInSlot(SlotLocationType.QuickSlot))
                return false;

            var allowedTypes = _inventoryConfig.QuickSlotConfig.AllowedTypes;
            if (allowedTypes == null || allowedTypes.Length == 0)
                return false;

            return allowedTypes.Contains(itemDef.Type);
        }

        public int GetQuickSlotCount()
        {
            return _quickSlots.Count;
        }

        #endregion

        #region IQuickSlotSystem - Assign/Remove

        public void AssignToQuickSlot(string instanceID, int slotIndex)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[QuickSlotSystem][QS] AssignToQuickSlot: instanceID={instanceID}, slot={slotIndex}, IsServer={IsServerInitialized}, IsOwner={IsOwner}");
            if (IsServerInitialized)
                AssignToQuickSlotServer(instanceID, slotIndex);
            else
                AssignToQuickSlotServerRpc(instanceID, slotIndex);
        }

        [ServerRpc(RequireOwnership = true)]
        private void AssignToQuickSlotServerRpc(string instanceID, int slotIndex)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[QuickSlotSystem][QS] AssignToQuickSlotServerRpc received: instanceID={instanceID}, slot={slotIndex}");
            AssignToQuickSlotServer(instanceID, slotIndex);
        }

        private void AssignToQuickSlotServer(string instanceID, int slotIndex)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[QuickSlotSystem][QS] AssignToQuickSlotServer entered: instanceID={instanceID}, slot={slotIndex}, count={_quickSlots.Count}");
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
            {
                Debug.LogWarning(
                    $"[QuickSlotSystem][QS] Server: Invalid slot index: {slotIndex} (count={_quickSlots.Count})");
                return;
            }

            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[QuickSlotSystem][QS] Server: Item not found in inventory: {instanceID}");
                return;
            }

            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!CanPlaceInQuickSlot(item.DefinitionID))
            {
                Debug.LogWarning(
                    $"[QuickSlotSystem][QS] Server: Item type not allowed in quickslot: {itemDef?.Type} (defID={item.DefinitionID})");
                return;
            }

            _quickSlots[slotIndex] = instanceID;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[QuickSlotSystem][QS] Server: Assigned '{itemDef?.DisplayName}' ({instanceID}) â†’ slot {slotIndex} â€” SyncList will propagate to clients");
        }

        public void RemoveFromQuickSlot(int slotIndex)
        {
            if (IsServerInitialized)
            {
                RemoveFromQuickSlotServer(slotIndex);
                return;
            }

            if (IsOwner)
                RemoveFromQuickSlotServerRpc(slotIndex);
        }

        [ServerRpc(RequireOwnership = true)]
        private void RemoveFromQuickSlotServerRpc(int slotIndex)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[QuickSlotSystem][QS] RemoveFromQuickSlotServerRpc received: slot={slotIndex}");
            RemoveFromQuickSlotServer(slotIndex);
        }

        [Server]
        private void RemoveFromQuickSlotServer(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
            {
                Debug.LogWarning($"[QuickSlotSystem] Invalid slot index: {slotIndex}");
                return;
            }

            _quickSlots[slotIndex] = string.Empty;

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[QuickSlotSystem] Removed item from slot {slotIndex}");
        }

        public void SwapQuickSlots(int slotIndex1, int slotIndex2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] SwapQuickSlots: server-only!");
                return;
            }

            SwapQuickSlotsServer(slotIndex1, slotIndex2);
        }

        [Server]
        private void SwapQuickSlotsServer(int slotIndex1, int slotIndex2)
        {
            if (slotIndex1 < 0 || slotIndex1 >= _quickSlots.Count ||
                slotIndex2 < 0 || slotIndex2 >= _quickSlots.Count)
            {
                Debug.LogWarning("[QuickSlotSystem] Invalid slot indices");
                return;
            }

            string temp = _quickSlots[slotIndex1];
            _quickSlots[slotIndex1] = _quickSlots[slotIndex2];
            _quickSlots[slotIndex2] = temp;
        }

        public void ClearAllQuickSlots()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] ClearAllQuickSlots: server-only!");
                return;
            }

            ClearAllQuickSlotsServer();
        }

        [Server]
        private void ClearAllQuickSlotsServer()
        {
            for (int i = 0; i < _quickSlots.Count; i++)
                _quickSlots[i] = string.Empty;

            // Cleanup all event handlers
            CleanupAllEventHandlers();
        }

        #endregion

        #region IQuickSlotSystem - Usage (OPTIMIZED WITH AUTO-CLEANUP)

        public void UseQuickSlot(int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] UseQuickSlot: server-only!");
                return;
            }

            UseQuickSlotServer(slotIndex);
        }

        [Server]
        private void UseQuickSlotServer(int slotIndex)
        {
            if (!CanUseQuickSlot(slotIndex))
            {
                Debug.LogWarning($"[QuickSlotSystem] Cannot use slot: {slotIndex}");
                return;
            }

            string instanceID = _quickSlots[slotIndex];
            var item = _inventorySystem.GetItemByInstanceID(instanceID);

            if (item == null)
            {
                Debug.LogWarning($"[QuickSlotSystem] Item not found: {instanceID}");
                RemoveFromQuickSlotServer(slotIndex);
                return;
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);

            // Handle throwable re-press logic
            if (_itemUseSystem != null && _itemUseSystem.IsUsingItem)
            {
                bool sameItem = _itemUseSystem.CurrentItem?.InstanceID == instanceID;
                if (sameItem && def is ThrowableDefinition)
                {
                    // Same slot pressed again while in throw-mode â†’ cancel
                    _itemUseSystem.CancelUse();
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log($"[QuickSlotSystem] Cancelled throw for slot {slotIndex}");
                }
                else
                {
                    Debug.LogWarning("[QuickSlotSystem] Already using an item");
                }

                return;
            }

            // Route through ItemUseSystem for consumables/throwables
            if (_itemUseSystem != null &&
                (def is ConsumableDefinition || def is ThrowableDefinition))
            {
                bool started = _itemUseSystem.UseItem(item);
                if (started)
                {
                    OnQuickSlotUsed?.Invoke(slotIndex, item);

                    // CRITICAL FIX: Auto-cleanup slot when item fully consumed
                    RegisterAutoCleanupHandler(slotIndex, instanceID);
                }

                return;
            }

            // Deployable items (beacons, traps, etc.):
            // Do NOT consume the item here. Instead tell the owning client to
            // start the placement preview flow via IDeployableHandler.
            // The item is consumed server-side only after the placement ServerRpc
            // succeeds (see BeaconPlaceable.CmdRequestPlaceBeacon).
            if (def != null && def.Type == ItemType.Deployable)
            {
                OnQuickSlotUsed?.Invoke(slotIndex, item); // update HUD immediately
                RpcBeginDeployment(Owner, item.InstanceID, item.DefinitionID);
                return;
            }

            // Fallback: Direct consume (for legacy items)
            OnQuickSlotUsed?.Invoke(slotIndex, item);
            _inventorySystem.RemoveItem(instanceID, 1);

            var updated = _inventorySystem.GetItemByInstanceID(instanceID);
            if (updated == null)
                RemoveFromQuickSlotServer(slotIndex);

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[QuickSlotSystem] Used {def?.DisplayName} from slot {slotIndex}");
        }

        public bool CanUseQuickSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
                return false;

            string instanceID = _quickSlots[slotIndex];
            if (string.IsNullOrEmpty(instanceID))
                return false;

            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            return item != null && item.Quantity > 0;
        }

        /// <summary>
        /// Fires on the OWNING CLIENT to start a deployable item placement flow.
        /// Resolves <see cref="IDeployableHandler"/> from the same GameObject
        /// (e.g. <see cref="NightHunt.Gameplay.Beacon.BeaconPlaceable"/>).
        /// </summary>
        [TargetRpc]
        private void RpcBeginDeployment(
            FishNet.Connection.NetworkConnection conn,
            string instanceId,
            string definitionId)
        {
            if (!IsOwner) return; // extra safety: only the owner acts

            var item = _inventorySystem?.GetItemByInstanceID(instanceId);
            var def = ItemDatabase.GetDefinition(definitionId);
            if (item == null || def == null)
            {
                Debug.LogWarning(
                    $"[QuickSlotSystem] RpcBeginDeployment: item or def not found ({instanceId}/{definitionId})");
                return;
            }

            var handler = ComponentResolver.Find<IDeployableHandler>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IDeployableHandler not found")
                .Resolve();
            if (handler == null)
            {
                Debug.LogWarning("[QuickSlotSystem] No IDeployableHandler found on player GO.");
                return;
            }

            handler.BeginDeploy(item, def);
        }

        #endregion

        #region Auto-Cleanup System - CRITICAL FIX

        /// <summary>
        /// CRITICAL: Register handler to auto-remove slot when item consumed
        /// This prevents ghost references to deleted items
        /// </summary>
        [Server]
        private void RegisterAutoCleanupHandler(int slotIndex, string instanceID)
        {
            if (_itemUseSystem == null)
                return;

            // Remove existing handler for this slot if any
            if (_activeCompletionHandlers.ContainsKey(slotIndex))
            {
                _itemUseSystem.OnItemUseCompleted -= _activeCompletionHandlers[slotIndex];
                _activeCompletionHandlers.Remove(slotIndex);
            }

            // Create new cleanup handler
            Action<ItemInstance> cleanupHandler = null;
            cleanupHandler = (usedItem) =>
            {
                if (usedItem.InstanceID == instanceID)
                {
                    // Check if item still exists in inventory
                    var remaining = _inventorySystem.GetItemByInstanceID(instanceID);
                    if (remaining == null)
                    {
                        RemoveFromQuickSlotServer(slotIndex);

                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                            Debug.Log($"[QuickSlotSystem] Auto-removed consumed item from slot {slotIndex}");
                    }

                    // Cleanup this handler
                    _itemUseSystem.OnItemUseCompleted -= cleanupHandler;
                    _activeCompletionHandlers.Remove(slotIndex);
                }
            };

            // Register handler
            _itemUseSystem.OnItemUseCompleted += cleanupHandler;
            _activeCompletionHandlers[slotIndex] = cleanupHandler;

            // Also handle cancellation
            Action<ItemInstance> cancelHandler = null;
            cancelHandler = (cancelledItem) =>
            {
                if (cancelledItem.InstanceID == instanceID)
                {
                    _itemUseSystem.OnItemUseCompleted -= cleanupHandler;
                    _itemUseSystem.OnItemUseCancelled -= cancelHandler;
                    _activeCompletionHandlers.Remove(slotIndex);
                }
            };

            _itemUseSystem.OnItemUseCancelled += cancelHandler;
        }

        /// <summary>
        /// Cleanup all active event handlers (called on stop/clear)
        /// </summary>
        [Server]
        private void CleanupAllEventHandlers()
        {
            if (_itemUseSystem == null)
                return;

            foreach (var handler in _activeCompletionHandlers.Values)
                _itemUseSystem.OnItemUseCompleted -= handler;

            _activeCompletionHandlers.Clear();
        }

        #endregion

        #region Network Callbacks

        private void OnQuickSlotsChanged(SyncListOperation op, int index, string oldValue, string newValue,
            bool asServer)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log(
                $"[QuickSlotSystem][QS] OnQuickSlotsChanged: op={op}, index={index}, old='{oldValue}', new='{newValue}', asServer={asServer}");
            if (asServer)
                return;

            // Both Add (initial full-state sync on connect) and Set (live change) carry
            // the same semantics: slot[index] now holds newValue.
            if (op == SyncListOperation.Add || op == SyncListOperation.Set)
            {
                if (!string.IsNullOrEmpty(newValue))
                {
                    var item = _inventorySystem?.GetItemByInstanceID(newValue);
                    if (item != null)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                            Debug.Log(
                            $"[QuickSlotSystem][QS] Firing OnQuickSlotAssigned: slot={index}, item={item.DefinitionID}");
                        OnQuickSlotAssigned?.Invoke(index, item);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[QuickSlotSystem][QS] Item '{newValue}' not yet in inventory cache â€” deferring...");
                        StartCoroutine(DeferredQuickSlotAssigned(index, newValue));
                    }
                }
                else if (op == SyncListOperation.Set)
                {
                    // Set to empty string means slot was cleared.
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log($"[QuickSlotSystem][QS] Slot {index} cleared â€” firing OnQuickSlotRemoved");
                    OnQuickSlotRemoved?.Invoke(index);
                }
            }
            else if (op == SyncListOperation.Clear)
            {
                for (int i = 0; i < _quickSlots.Count; i++)
                    OnQuickSlotRemoved?.Invoke(i);
            }
        }

        /// <summary>
        /// Wait up to a few frames for the inventory cache to populate this item,
        /// then fire OnQuickSlotAssigned. Handles the race where the QS SyncList
        /// callback arrives before the inventory SyncList callback.
        /// </summary>
        private System.Collections.IEnumerator DeferredQuickSlotAssigned(int slotIndex, string instanceID)
        {
            // Poll for up to 10 frames.
            int maxFrames = 10;
            for (int f = 0; f < maxFrames; f++)
            {
                yield return null; // wait one frame

                // Verify the slot still maps to this instanceID (user may have changed it).
                if (slotIndex >= _quickSlots.Count || _quickSlots[slotIndex] != instanceID)
                {
                    Debug.LogWarning(
                        $"[QuickSlotSystem][QS] Deferred: slot {slotIndex} changed before item arrived, aborting");
                    yield break;
                }

                var item = _inventorySystem?.GetItemByInstanceID(instanceID);
                if (item != null)
                {
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log(
                        $"[QuickSlotSystem][QS] Deferred fire OnQuickSlotAssigned after {f + 1} frame(s): slot={slotIndex}, item={item.DefinitionID}");
                    OnQuickSlotAssigned?.Invoke(slotIndex, item);
                    yield break;
                }
            }

            Debug.LogWarning(
                $"[QuickSlotSystem][QS] Deferred: item '{instanceID}' NEVER arrived in inventory cache after {maxFrames} frames");
        }

        #endregion

        #region Debug

        [ContextMenu("Log QuickSlot State")]
        public void LogQuickSlotState()
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"=== QuickSlots ({_quickSlots.Count}) ===");

            for (int i = 0; i < _quickSlots.Count; i++)
            {
                var item = GetQuickSlotItem(i);
                if (item != null)
                {
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log($"  [{i}] {def?.DisplayName} x{item.Quantity}");
                }
                else
                {
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        Debug.Log($"  [{i}] [Empty]");
                }
            }

            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"Active handlers: {_activeCompletionHandlers.Count}");
        }

        #endregion
    }
}
