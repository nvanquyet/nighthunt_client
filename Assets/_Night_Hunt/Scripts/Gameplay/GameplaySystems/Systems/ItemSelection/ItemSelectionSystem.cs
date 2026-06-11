using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.ItemUse;

namespace NightHunt.GameplaySystems.ItemSelection
{
    /// <summary>
    /// Server-authoritative item selection system.
    ///
    /// The player selects ONE item (consumable or throwable)
    /// at a time via the left/right filter panels adjacent to the weapon HUD.
    ///
    /// ARCHITECTURE:
    /// - SyncVar<string> _selectedInstanceId — synced to all clients
    /// - Delegates actual usage to IItemUseSystem
    /// - Continuous-use: after OnItemUseCompleted fires, if qty still > 0 tries UseItem again
    /// </summary>
    public class ItemSelectionSystem : NetworkBehaviour, IItemSelectionSystem
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystemComponent;
        [SerializeField] private ItemUseSystem _itemUseSystemComponent;

        #endregion

        #region SyncVars

        private readonly SyncVar<string> _selectedInstanceId = new SyncVar<string>(
            string.Empty, new SyncTypeSettings(WritePermission.ServerOnly));

        #endregion

        #region Runtime State

        private IInventorySystem _inventorySystem;
        private IItemUseSystem   _itemUseSystem;
        private IWeaponSystem    _weaponSystem;
        private WeaponSlotType?  _previousWeaponSlotForSelection;
        private bool             _weaponHolsteredForSelection;

        #endregion

        #region IItemSelectionSystem — Properties

        public ItemInstance SelectedItem
        {
            get
            {
                if (string.IsNullOrEmpty(_selectedInstanceId.Value)) return null;
                return _inventorySystem?.GetItemByInstanceID(_selectedInstanceId.Value);
            }
        }

        public bool HasSelection => !string.IsNullOrEmpty(_selectedInstanceId.Value);

        #endregion

        #region Events

        public event Action<ItemInstance> OnItemSelected;
        public event Action               OnItemDeselected;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Subscribe to SyncVar changes so events fire on ALL connections
            // (server via asServer=true, remote clients via asServer=false).
            // This replaces the old inline OnItemSelected / OnItemDeselected calls
            // which only ran inside server-only guards and never reached pure clients.
            _selectedInstanceId.OnChange += OnSelectedInstanceIdChanged;
            ValidateReferences();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ValidateReferences();
        }
#endif

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_itemUseSystem != null)
                _itemUseSystem.OnItemUseCompleted += HandleItemUseCompleted;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (_itemUseSystem != null)
                _itemUseSystem.OnItemUseCompleted -= HandleItemUseCompleted;
        }

        #endregion

        #region IItemSelectionSystem — Operations

        /// <summary>
        /// Select an item and begin using it.
        /// If the same item is already selected, cancels the selection (toggle).
        /// </summary>
        public void SelectItem(string instanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.Log($"[NH_FLOW][24][ItemSelection.SelectIgnored] reason=not-server instance='{instanceID}' owner={Owner?.ClientId}");
                return;
            }

            Debug.Log($"[ITEM_FLOW] [04][Server.Select] instance='{instanceID}' current='{_selectedInstanceId.Value}' owner={Owner?.ClientId}");
            Debug.Log($"[NH_FLOW][24][ItemSelection.Select] instance='{instanceID}' current='{_selectedInstanceId.Value}' owner={Owner?.ClientId} inventory={(_inventorySystem != null ? "ok" : "null")} itemUse={(_itemUseSystem != null ? "ok" : "null")}");
            Debug.Log($"[ItemSelectionSystem] SelectItem('{instanceID}') — current='{_selectedInstanceId.Value}'");

            // Re-selecting current item is a no-op. Cancel is explicit.
            if (_selectedInstanceId.Value == instanceID && !string.IsNullOrEmpty(instanceID))
            {
                Debug.Log($"[ItemSelectionSystem] SelectItem: same item already selected → CancelSelection");
                return;
            }

            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[NH_FLOW][24][ItemSelection.SelectRejected] reason=item-not-found instance='{instanceID}' current='{_selectedInstanceId.Value}'");
                Debug.LogWarning($"[ItemSelectionSystem] SelectItem: instance '{instanceID}' not found in inventory.");
                return;
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                Debug.LogWarning($"[NH_FLOW][24][ItemSelection.SelectRejected] reason=def-not-found instance='{instanceID}' def='{item.DefinitionID}'");
                Debug.LogWarning($"[ItemSelectionSystem] SelectItem: definition '{item.DefinitionID}' not found.");
                return;
            }

            if (def.Type != ItemType.Consumable && def.Type != ItemType.Throwable && def.Type != ItemType.Deployable)
            {
                Debug.LogWarning($"[NH_FLOW][24][ItemSelection.SelectRejected] reason=unsupported-type instance='{instanceID}' def={def.ItemID} type={def.Type}");
                Debug.LogWarning($"[ItemSelectionSystem] SelectItem: item type {def.Type} is not selectable.");
                return;
            }

            // Cancel any previous use before starting new one.
            if (!string.IsNullOrEmpty(_selectedInstanceId.Value))
            {
                Debug.Log($"[ItemSelectionSystem] SelectItem: cancelling previous use of '{_selectedInstanceId.Value}'");
                _itemUseSystem?.CancelUse();
            }

            HolsterWeaponForSelection();

            Debug.Log($"[ItemSelectionSystem] SelectItem: selecting '{item.InstanceID}' ({def.DisplayName})");
            Debug.Log($"[NH_FLOW][25][ItemSelection.SelectedSet] instance='{item.InstanceID}' def={def.ItemID} type={def.Type} qty={item.Quantity}");
            _selectedInstanceId.Value = instanceID;
            // OnSelectedInstanceIdChanged callback fires automatically via SyncVar.OnChange.
        }

        /// <summary>
        /// Begin using the currently selected item without changing the selection state.
        /// Consumables: immediately applies effect.
        /// Throwables/Deployables: starts aim/arm animation; fire input then executes.
        /// </summary>
        public void UseSelectedItem()
        {
            if (!IsServerInitialized)
            {
                Debug.Log($"[NH_FLOW][26][ItemSelection.UseIgnored] reason=not-server selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId}");
                return;
            }
            var item = SelectedItem;
            if (item == null)
            {
                Debug.LogWarning($"[ITEM_SELECT_FLOW] UseSelectedItem failed: no selected item. selectedId='{_selectedInstanceId.Value}' owner={Owner?.ClientId}");
                Debug.LogWarning($"[NH_FLOW][26][ItemSelection.UseRejected] reason=no-selected-item selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId}");
                Debug.LogWarning($"[ItemSelectionSystem] UseSelectedItem: no item selected (SelectedItem = null).");
                return;
            }
            if (_itemUseSystem?.IsUsingItem == true)
            {
                Debug.LogWarning($"[ITEM_SELECT_FLOW] UseSelectedItem blocked: already using '{_itemUseSystem.CurrentItem?.InstanceID ?? "null"}'.");
                Debug.LogWarning($"[NH_FLOW][26][ItemSelection.UseRejected] reason=already-using selected='{_selectedInstanceId.Value}' current='{_itemUseSystem.CurrentItem?.InstanceID ?? "null"}'");
                Debug.LogWarning($"[ItemSelectionSystem] UseSelectedItem: already using an item, ignoring.");
                return;
            }
            Debug.Log($"[ITEM_FLOW] [06][Server.UseSelected] item={item.InstanceID} def={item.DefinitionID} qty={item.Quantity}");
            Debug.Log($"[NH_FLOW][26][ItemSelection.UseSelected] item={item.InstanceID} def={item.DefinitionID} qty={item.Quantity} owner={Owner?.ClientId} itemUse={(_itemUseSystem != null ? "ok" : "null")}");
            Debug.Log($"[ItemSelectionSystem] UseSelectedItem: '{item.InstanceID}'");
            _itemUseSystem?.UseItem(item);
        }

        public void DeselectItem(bool restorePreviousWeapon = true)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(_selectedInstanceId.Value)) return;

            Debug.Log($"[ItemSelectionSystem] DeselectItem: clearing '{_selectedInstanceId.Value}'");
            Debug.Log($"[NH_FLOW][27][ItemSelection.Deselect] selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId} restoreWeapon={restorePreviousWeapon}");
            ClearSelection(restorePreviousWeapon, "deselect");
        }

        public void CancelSelection(bool restorePreviousWeapon = true)
        {
            if (!IsServerInitialized) return;

            Debug.Log($"[ItemSelectionSystem] CancelSelection: cancelling use + clearing '{_selectedInstanceId.Value}'");
            Debug.Log($"[NH_FLOW][27][ItemSelection.Cancel] selected='{_selectedInstanceId.Value}' currentUse='{_itemUseSystem?.CurrentItem?.InstanceID ?? "null"}' using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} owner={Owner?.ClientId} restoreWeapon={restorePreviousWeapon}");
            _itemUseSystem?.CancelUse();
            ClearSelection(restorePreviousWeapon, "cancel");
        }

        // ─── ServerRpc Wrappers ───────────────────────────────────────────────────
        // Safe to call from client UI. RequireOwnership = true ensures only the
        // owning player can trigger selection changes on their own character.

        /// <summary>ServerRpc: select an item by instance ID from client UI.</summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestSelectItem(string instanceID)
        {
            Debug.Log($"[ITEM_FLOW] [04][Rpc.Select] instance='{instanceID}' owner={Owner?.ClientId}");
            Debug.Log($"[NH_FLOW][23][Rpc.SelectItem] instance='{instanceID}' owner={Owner?.ClientId} asServer={IsServerInitialized} asOwner={IsOwner}");
            Debug.Log($"[ItemSelectionSystem] RequestSelectItem('{instanceID}') received on server");
            SelectItem(instanceID);
        }

        /// <summary>ServerRpc: deselect without cancelling active use.</summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestDeselectItem()
        {
            Debug.Log($"[NH_FLOW][23][Rpc.DeselectItem] selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId} asServer={IsServerInitialized} asOwner={IsOwner}");
            Debug.Log($"[ItemSelectionSystem] RequestDeselectItem received on server");
            DeselectItem();
        }

        /// <summary>ServerRpc: arm the currently selected item.</summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestUseSelectedItem()
        {
            Debug.Log($"[ITEM_FLOW] [05][Rpc.UseSelected] selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId}");
            Debug.Log($"[NH_FLOW][23][Rpc.UseSelectedItem] selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId} asServer={IsServerInitialized} asOwner={IsOwner}");
            Debug.Log($"[ItemSelectionSystem] RequestUseSelectedItem received on server");
            UseSelectedItem();
        }

        /// <summary>ServerRpc: cancel active use and deselect.</summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestCancelSelection(bool restorePreviousWeapon = true)
        {
            Debug.Log($"[NH_FLOW][23][Rpc.CancelSelection] selected='{_selectedInstanceId.Value}' owner={Owner?.ClientId} asServer={IsServerInitialized} asOwner={IsOwner} restoreWeapon={restorePreviousWeapon}");
            Debug.Log($"[ItemSelectionSystem] RequestCancelSelection received on server");
            CancelSelection(restorePreviousWeapon);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called by FishNet whenever _selectedInstanceId syncs.
        /// • asServer = true  → value was set locally on the server (also fires on host).
        /// • asServer = false → value arrived via network on a remote client.
        /// Replacing the old inline OnItemSelected/OnItemDeselected calls so events
        /// now reach ALL connections, not just the server.
        /// </summary>
        private void OnSelectedInstanceIdChanged(string prev, string next, bool asServer)
        {
            Debug.Log($"[ITEM_FLOW] [07][Sync.Selected] '{prev}' -> '{next}' asServer={asServer}");
            Debug.Log($"[NH_FLOW][28][Sync.Selected] prev='{prev}' next='{next}' asServer={asServer} owner={Owner?.ClientId} inventory={(_inventorySystem != null ? "ok" : "null")}");
            Debug.Log($"[ItemSelectionSystem] SyncVar changed: '{prev}' → '{next}'  asServer={asServer}");
            if (string.IsNullOrEmpty(next))
            {
                OnItemDeselected?.Invoke();
            }
            else
            {
                var item = _inventorySystem?.GetItemByInstanceID(next);
                if (item != null)
                    OnItemSelected?.Invoke(item);
                else
                    Debug.LogWarning($"[ItemSelectionSystem] OnChange: item '{next}' not found in inventory on this connection.");
            }
        }

        /// <summary>
        /// After a use completes: if the item is fully depleted, clear the selection;
        /// otherwise keep the selection so the player can fire again to use another charge.
        /// </summary>
        private void HandleItemUseCompleted(ItemInstance completedItem)
        {
            if (!IsServerInitialized) return;
            if (completedItem == null || string.IsNullOrEmpty(_selectedInstanceId.Value)) return;
            if (completedItem.InstanceID != _selectedInstanceId.Value) return;

            // Re-fetch from inventory to get current quantity after consumption.
            var updated = _inventorySystem?.GetItemByInstanceID(completedItem.InstanceID);
            if (updated == null || updated.Quantity <= 0)
            {
                Debug.Log($"[ItemSelectionSystem] HandleItemUseCompleted: '{completedItem.InstanceID}' depleted → clearing selection");
                // Item fully depleted — clear selection (UI will auto-switch).
                // OnSelectedInstanceIdChanged callback fires automatically.
                ClearSelection(restorePreviousWeapon: true, "depleted");
            }
            // else: quantity remains — keep selection, player fires again to reuse.
        }

        #endregion

        #region Private Helpers

        private void ValidateReferences()
        {
            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                .UseExisting(_inventorySystemComponent)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning("[ItemSelectionSystem] IInventorySystem not found")
                .Resolve();

            if (_inventorySystem is InventorySystem invConcrete)
                _inventorySystemComponent = invConcrete;

            _itemUseSystem = ComponentResolver.Find<IItemUseSystem>(this)
                .UseExisting(_itemUseSystemComponent)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning("[ItemSelectionSystem] IItemUseSystem not found")
                .Resolve();

            if (_itemUseSystem is ItemUseSystem useConcrete)
                _itemUseSystemComponent = useConcrete;

            _weaponSystem = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();
        }

        private void HolsterWeaponForSelection()
        {
            if (_weaponSystem == null)
                return;

            WeaponSlotType? activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (!activeSlot.HasValue)
                return;

            if (!_weaponHolsteredForSelection)
                _previousWeaponSlotForSelection = activeSlot;

            _weaponHolsteredForSelection = true;
            Debug.Log($"[NH_FLOW][25][ItemSelection.HolsterWeaponForSelection] active={activeSlot.Value} previous={_previousWeaponSlotForSelection?.ToString() ?? "none"} owner={Owner?.ClientId}");
            _weaponSystem.HolsterWeapon();
        }

        private void ClearSelection(bool restorePreviousWeapon, string reason)
        {
            if (!string.IsNullOrEmpty(_selectedInstanceId.Value))
                _selectedInstanceId.Value = string.Empty;

            if (restorePreviousWeapon)
                RestoreWeaponAfterSelection(reason);
            else
                ForgetSelectionWeaponRestore(reason);
        }

        private void RestoreWeaponAfterSelection(string reason)
        {
            if (!_weaponHolsteredForSelection)
                return;

            WeaponSlotType? previousSlot = _previousWeaponSlotForSelection;
            _weaponHolsteredForSelection = false;
            _previousWeaponSlotForSelection = null;

            if (_weaponSystem == null || !previousSlot.HasValue)
                return;

            WeaponSlotType slot = previousSlot.Value;
            WeaponSlotType? activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (activeSlot.HasValue)
            {
                Debug.Log($"[NH_FLOW][27][ItemSelection.RestoreWeaponSkipped] reason={reason} previous={slot} active={activeSlot.Value} owner={Owner?.ClientId}");
                return;
            }

            if (!_weaponSystem.IsSlotOccupied(slot))
            {
                Debug.Log($"[NH_FLOW][27][ItemSelection.RestoreWeaponSkipped] reason={reason} previous={slot} slotEmpty=True owner={Owner?.ClientId}");
                return;
            }

            Debug.Log($"[NH_FLOW][27][ItemSelection.RestoreWeapon] reason={reason} slot={slot} owner={Owner?.ClientId}");
            _weaponSystem.SelectWeapon(slot);
        }

        private void ForgetSelectionWeaponRestore(string reason)
        {
            if (!_weaponHolsteredForSelection && !_previousWeaponSlotForSelection.HasValue)
                return;

            Debug.Log($"[NH_FLOW][27][ItemSelection.ForgetWeaponRestore] reason={reason} previous={_previousWeaponSlotForSelection?.ToString() ?? "none"} owner={Owner?.ClientId}");
            _weaponHolsteredForSelection = false;
            _previousWeaponSlotForSelection = null;
        }

        #endregion
    }
}
