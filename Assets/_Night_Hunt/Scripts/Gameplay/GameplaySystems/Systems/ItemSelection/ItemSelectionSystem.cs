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
            ValidateReferences();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
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
            if (!IsServerInitialized) return;

            // Toggle: selecting the current item cancels it.
            if (_selectedInstanceId.Value == instanceID && !string.IsNullOrEmpty(instanceID))
            {
                CancelSelection();
                return;
            }

            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[ItemSelectionSystem] SelectItem: instance '{instanceID}' not found in inventory.");
                return;
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                Debug.LogWarning($"[ItemSelectionSystem] SelectItem: definition '{item.DefinitionID}' not found.");
                return;
            }

            if (def.Type != ItemType.Consumable && def.Type != ItemType.Throwable && def.Type != ItemType.Deployable)
            {
                Debug.LogWarning($"[ItemSelectionSystem] SelectItem: item type {def.Type} is not selectable.");
                return;
            }

            // Cancel any previous use before starting new one.
            if (!string.IsNullOrEmpty(_selectedInstanceId.Value))
                _itemUseSystem?.CancelUse();

            _selectedInstanceId.Value = instanceID;
            OnItemSelected?.Invoke(item);
        }

        /// <summary>
        /// Begin using the currently selected item without changing the selection state.
        /// Consumables: immediately applies effect.
        /// Throwables/Deployables: starts aim/arm animation; fire input then executes.
        /// </summary>
        public void UseSelectedItem()
        {
            if (!IsServerInitialized) return;
            var item = SelectedItem;
            if (item == null) return;
            if (_itemUseSystem?.IsUsingItem == true) return;
            _itemUseSystem?.UseItem(item);
        }

        public void DeselectItem()
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(_selectedInstanceId.Value)) return;

            _selectedInstanceId.Value = string.Empty;
            OnItemDeselected?.Invoke();
        }

        public void CancelSelection()
        {
            if (!IsServerInitialized) return;

            _itemUseSystem?.CancelUse();
            _selectedInstanceId.Value = string.Empty;
            OnItemDeselected?.Invoke();
        }

        #endregion

        #region Event Handlers

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
                // Item fully depleted — clear selection (UI will auto-switch).
                _selectedInstanceId.Value = string.Empty;
                OnItemDeselected?.Invoke();
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
        }

        #endregion
    }
}
