using FishNet.Object;
using System;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Stat;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Attachment
{
    /// <summary>
    /// Server-authoritative attachment management system.
    ///
    /// Manages attachment slots (scopes, grips, pouches, plates, etc.) for a networked player.
    /// Attachment state is stored inside <see cref="ItemInstance.AttachedItems"/> and replicated
    /// via the parent item's entry in InventorySystem's SyncList.
    ///
    /// RULES:
    ///   - All mutations are server-authoritative.
    ///   - Clients send requests via [ServerRpc(RequireOwnership = true)].
    ///   - Auto-recovery returns attachments to inventory when the parent item is removed
    ///     (dropped, cleared, etc.) — controlled by <see cref="InventoryConfig.ReturnAttachmentsToInventoryOnDrop"/>.
    /// </summary>
    public class AttachmentSystem : NetworkBehaviour, IAttachmentSystem, IDisposable
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private MonoBehaviour _inventorySystemOverride;

        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;

        #endregion

        #region Events

        public event Action<string, int, ItemInstance> OnAttachmentAttached;
        public event Action<string, int, ItemInstance> OnAttachmentDetached;

        #endregion

        #region Private fields

        // Resolved as interface — the concrete type is irrelevant to this system.
        private IInventorySystem _inventorySystem;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                .UseExisting(_inventorySystemOverride as IInventorySystem)
                .OnSelf().InChildren().InParent()
                .OrLogError("[AttachmentSystem] IInventorySystem not found!")
                .Resolve();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate() => ValidateReferences();
#endif

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to inventory events for auto-recovery of attachments on parent removal.
            // Always subscribe — actual recovery decision deferred to InventoryConfig.ReturnAttachmentsToInventoryOnDrop.
            if (_inventorySystem != null)
                _inventorySystem.OnItemRemoved += OnParentItemRemoved;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            if (_inventorySystem != null)
                _inventorySystem.OnItemRemoved -= OnParentItemRemoved;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_inventorySystem != null)
                _inventorySystem.OnItemRemoved -= OnParentItemRemoved;
        }

        #endregion

        #region IAttachmentSystem — Getters

        public ItemInstance GetAttachment(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null)
                return null;

            string attachmentID = parent.GetAttachment(slotIndex);
            if (string.IsNullOrEmpty(attachmentID))
                return null;

            return _inventorySystem.GetItemByInstanceID(attachmentID)
                ?? ItemDatabase.GetInstance(attachmentID);
        }

        public ItemInstance[] GetAllAttachments(string parentInstanceID)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null || parent.AttachedItems == null)
                return new ItemInstance[0];

            var attachments = new ItemInstance[parent.AttachedItems.Length];
            for (int i = 0; i < parent.AttachedItems.Length; i++)
                attachments[i] = GetAttachment(parentInstanceID, i);

            return attachments;
        }

        public bool IsSlotOccupied(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            return parent != null && parent.HasAttachment(slotIndex);
        }

        public bool CanAttach(string attachmentInstanceID, string parentInstanceID, int slotIndex)
        {
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentInstanceID)
                          ?? ItemDatabase.GetInstance(attachmentInstanceID);
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID)
                      ?? ItemDatabase.GetInstance(parentInstanceID);

            if (attachment == null || parent == null)
                return false;

            var attachmentDef = ItemDatabase.GetDefinition(attachment.DefinitionID);
            var parentDef     = ItemDatabase.GetDefinition(parent.DefinitionID);

            if (attachmentDef == null || parentDef == null)
                return false;

            // Slot index must be within the parent's declared attachment sockets.
            if (parentDef.AttachmentSlots == null
                || slotIndex < 0
                || slotIndex >= parentDef.AttachmentSlots.Length)
                return false;

            var slotType = parentDef.AttachmentSlots[slotIndex];
            return attachmentDef.CanAttachToSlot(slotType);
        }

        public AttachmentSlotType GetSlotType(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID)
                      ?? ItemDatabase.GetInstance(parentInstanceID);
            if (parent == null)
                return AttachmentSlotType.None;

            var parentDef = ItemDatabase.GetDefinition(parent.DefinitionID);
            if (parentDef == null || parentDef.AttachmentSlots == null)
                return AttachmentSlotType.None;

            if (slotIndex < 0 || slotIndex >= parentDef.AttachmentSlots.Length)
                return AttachmentSlotType.None;

            return parentDef.AttachmentSlots[slotIndex];
        }

        #endregion

        #region IAttachmentSystem — Attach / Detach (public API with client→server routing)

        /// <summary>
        /// Attach an item to a parent item's attachment slot.
        /// Routes to server via ServerRpc when called from the owning client.
        /// </summary>
        public void AttachItem(string attachmentInstanceID, string parentInstanceID, int slotIndex)
        {
            if (IsServerInitialized) { AttachItemServer(attachmentInstanceID, parentInstanceID, slotIndex); return; }
            if (IsOwner) AttachItemServerRpc(attachmentInstanceID, parentInstanceID, slotIndex);
            else Debug.LogWarning("[AttachmentSystem] AttachItem: caller does not own this object.");
        }

        [ServerRpc(RequireOwnership = true)]
        private void AttachItemServerRpc(string attachmentInstanceID, string parentInstanceID, int slotIndex)
            => AttachItemServer(attachmentInstanceID, parentInstanceID, slotIndex);

        /// <summary>
        /// Detach an attachment from a specific slot and return it to inventory.
        /// Routes to server via ServerRpc when called from the owning client.
        /// </summary>
        public void DetachItem(string parentInstanceID, int slotIndex)
        {
            if (IsServerInitialized) { DetachItemServer(parentInstanceID, slotIndex); return; }
            if (IsOwner) DetachItemServerRpc(parentInstanceID, slotIndex);
            else Debug.LogWarning("[AttachmentSystem] DetachItem: caller does not own this object.");
        }

        [ServerRpc(RequireOwnership = true)]
        private void DetachItemServerRpc(string parentInstanceID, int slotIndex)
            => DetachItemServer(parentInstanceID, slotIndex);

        /// <summary>
        /// Swap attachments between two parent items / slots.
        /// Routes to server via ServerRpc when called from the owning client.
        /// </summary>
        public void SwapAttachments(string parentID1, int slotIndex1, string parentID2, int slotIndex2)
        {
            if (IsServerInitialized) { SwapAttachmentsServer(parentID1, slotIndex1, parentID2, slotIndex2); return; }
            if (IsOwner) SwapAttachmentsServerRpc(parentID1, slotIndex1, parentID2, slotIndex2);
            else Debug.LogWarning("[AttachmentSystem] SwapAttachments: caller does not own this object.");
        }

        [ServerRpc(RequireOwnership = true)]
        private void SwapAttachmentsServerRpc(string parentID1, int slotIndex1, string parentID2, int slotIndex2)
            => SwapAttachmentsServer(parentID1, slotIndex1, parentID2, slotIndex2);

        /// <summary>
        /// Detach all attachments from a parent item and return them to inventory.
        /// Routes to server via ServerRpc when called from the owning client.
        /// </summary>
        public void DetachAllFromItem(string parentInstanceID)
        {
            if (IsServerInitialized) { DetachAllFromItemServer(parentInstanceID, false); return; }
            if (IsOwner) DetachAllFromItemServerRpc(parentInstanceID);
            else Debug.LogWarning("[AttachmentSystem] DetachAllFromItem: caller does not own this object.");
        }

        [ServerRpc(RequireOwnership = true)]
        private void DetachAllFromItemServerRpc(string parentInstanceID)
            => DetachAllFromItemServer(parentInstanceID, false);

        #endregion

        #region Server Implementations

        [Server]
        private void AttachItemServer(string attachmentInstanceID, string parentInstanceID, int slotIndex)
        {
            if (!CanAttach(attachmentInstanceID, parentInstanceID, slotIndex))
            {
                Debug.LogWarning("[AttachmentSystem] AttachItemServer: incompatible or invalid attachment.");
                return;
            }

            // Parent may be in inventory slots or in equipment/weapon slot (InventoryIndex = -1);
            // use ItemDatabase as fallback for items that are outside the inventory grid.
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentInstanceID)
                          ?? ItemDatabase.GetInstance(attachmentInstanceID);
            var parent     = _inventorySystem.GetItemByInstanceID(parentInstanceID)
                          ?? ItemDatabase.GetInstance(parentInstanceID);

            if (attachment == null || parent == null)
            {
                Debug.LogWarning("[AttachmentSystem] AttachItemServer: one or both instances not found.");
                return;
            }

            // If the target slot is already occupied, detach the existing attachment first.
            if (parent.HasAttachment(slotIndex))
                DetachItemServer(parentInstanceID, slotIndex);

            // Link attachment to parent slot.
            parent.SetAttachment(slotIndex, attachmentInstanceID);

            // Remove attachment from the inventory grid (keeps instance in ItemDatabase
            // so ItemStatSystem can look up its modifiers).
            _inventorySystem.RemoveItemFromSlotsOnly(attachmentInstanceID);

            // Recalculate combined stats so attachment modifiers are immediately reflected.
            RecomputeItemStats(parentInstanceID);

            OnAttachmentAttached?.Invoke(parentInstanceID, slotIndex, attachment);

            if (_enableDebugLogs)
            {
                var attachDef = ItemDatabase.GetDefinition(attachment.DefinitionID);
                var parentDef = ItemDatabase.GetDefinition(parent.DefinitionID);
                Debug.Log($"[AttachmentSystem] Attached '{attachDef?.DisplayName}' → '{parentDef?.DisplayName}'[{slotIndex}]");
            }
        }

        [Server]
        private void DetachItemServer(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID)
                      ?? ItemDatabase.GetInstance(parentInstanceID);

            if (parent == null || !parent.HasAttachment(slotIndex))
            {
                Debug.LogWarning("[AttachmentSystem] DetachItemServer: no attachment found in slot.");
                return;
            }

            string attachmentID = parent.GetAttachment(slotIndex);

            // Attachment was removed from inventory grid on attach, so look up via ItemDatabase.
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentID)
                          ?? ItemDatabase.GetInstance(attachmentID);

            // Clear the slot BEFORE restoring to inventory to avoid circular reference.
            parent.ClearAttachment(slotIndex);

            if (attachment != null)
            {
                _inventorySystem.RestoreItemToSlots(attachment);
                OnAttachmentDetached?.Invoke(parentInstanceID, slotIndex, attachment);
            }
            else
            {
                Debug.LogWarning($"[AttachmentSystem] DetachItemServer: attachment instance '{attachmentID}' not found — slot cleared without restore.");
            }

            // Recalculate stats — removed attachment modifiers are no longer applied.
            RecomputeItemStats(parentInstanceID);

            if (_enableDebugLogs)
                Debug.Log($"[AttachmentSystem] Detached attachment from '{parentInstanceID}'[{slotIndex}]");
        }

        [Server]
        private void SwapAttachmentsServer(string parentID1, int slotIndex1, string parentID2, int slotIndex2)
        {
            var parent1 = _inventorySystem.GetItemByInstanceID(parentID1) ?? ItemDatabase.GetInstance(parentID1);
            var parent2 = _inventorySystem.GetItemByInstanceID(parentID2) ?? ItemDatabase.GetInstance(parentID2);

            if (parent1 == null || parent2 == null)
            {
                Debug.LogWarning("[AttachmentSystem] SwapAttachmentsServer: one or both parent items not found.");
                return;
            }

            string attach1 = parent1.GetAttachment(slotIndex1);
            string attach2 = parent2.GetAttachment(slotIndex2);

            // Validate cross-compatibility before mutating state.
            if (!string.IsNullOrEmpty(attach1) && !string.IsNullOrEmpty(attach2))
            {
                if (!CanAttach(attach1, parentID2, slotIndex2) || !CanAttach(attach2, parentID1, slotIndex1))
                {
                    Debug.LogWarning("[AttachmentSystem] SwapAttachmentsServer: cross-incompatible attachment types.");
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(attach1) && !CanAttach(attach1, parentID2, slotIndex2))
            {
                Debug.LogWarning("[AttachmentSystem] SwapAttachmentsServer: attach1 incompatible with target slot.");
                return;
            }
            else if (!string.IsNullOrEmpty(attach2) && !CanAttach(attach2, parentID1, slotIndex1))
            {
                Debug.LogWarning("[AttachmentSystem] SwapAttachmentsServer: attach2 incompatible with target slot.");
                return;
            }

            parent1.SetAttachment(slotIndex1, attach2);
            parent2.SetAttachment(slotIndex2, attach1);

            // Recompute stats for both parents after swap.
            RecomputeItemStats(parentID1);
            RecomputeItemStats(parentID2);

            if (_enableDebugLogs)
                Debug.Log($"[AttachmentSystem] Swapped '{parentID1}'[{slotIndex1}] ↔ '{parentID2}'[{slotIndex2}]");
        }

        [Server]
        private void DetachAllFromItemServer(string parentInstanceID, bool dropWithItem = false)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID)
                      ?? ItemDatabase.GetInstance(parentInstanceID);

            if (parent == null || parent.AttachedItems == null)
                return;

            // Determine whether to return attachments to the inventory grid.
            // When dropping with the parent item, attachments stay on the item (no recovery).
            bool returnToInventory = !dropWithItem
                && (_inventoryConfig == null || _inventoryConfig.ReturnAttachmentsToInventoryOnDrop);

            for (int i = 0; i < parent.AttachedItems.Length; i++)
            {
                if (!parent.HasAttachment(i))
                    continue;

                if (returnToInventory)
                    DetachItemServer(parentInstanceID, i);
                // else: attachments remain on parent and will drop with it
            }
        }

        #endregion

        #region Auto-Recovery on Parent Removal

        /// <summary>
        /// Recovers all attachments to the inventory when the parent item is fully removed
        /// (e.g., on drop, destruction, or ClearInventory).
        /// </summary>
        [Server]
        private void OnParentItemRemoved(ItemInstance parentItem, int quantity)
        {
            if (parentItem == null || parentItem.AttachedItems == null)
                return;

            // Only recover when the item is completely gone (quantity reached zero).
            var remaining = _inventorySystem.GetItemByInstanceID(parentItem.InstanceID);
            if (remaining != null)
                return;

            // Skip recovery if config says attachments drop with the item.
            bool shouldRecover = _inventoryConfig == null
                              || _inventoryConfig.ReturnAttachmentsToInventoryOnDrop;
            if (!shouldRecover)
                return;

            int recoveredCount = 0;
            for (int i = 0; i < parentItem.AttachedItems.Length; i++)
            {
                string attachmentID = parentItem.AttachedItems[i];
                if (string.IsNullOrEmpty(attachmentID))
                    continue;

                var attachment = ItemDatabase.GetInstance(attachmentID);
                if (attachment == null)
                    continue;

                // FIX (BUG-03): Restore the original instance (preserving all runtime state:
                // CustomData, ammo, durability, InstanceID, etc.) instead of creating a new
                // item via AddItem() which would lose all runtime state and produce a ghost instance.
                _inventorySystem.RestoreItemToSlots(attachment);
                recoveredCount++;

                if (_enableDebugLogs)
                {
                    var def = ItemDatabase.GetDefinition(attachment.DefinitionID);
                    Debug.Log($"[AttachmentSystem] Auto-recovered attachment: '{def?.DisplayName}' (ID: {attachmentID})");
                }
            }

            if (recoveredCount > 0 && _enableDebugLogs)
            {
                var parentDef = ItemDatabase.GetDefinition(parentItem.DefinitionID);
                Debug.Log($"[AttachmentSystem] Recovered {recoveredCount} attachment(s) from '{parentDef?.DisplayName}'");
            }
        }

        #endregion

        #region Helpers

        /// <summary>Recompute the combined base + attachment stats for the given item instance.</summary>
        private void RecomputeItemStats(string instanceID)
        {
            var item = _inventorySystem?.GetItemByInstanceID(instanceID)
                    ?? ItemDatabase.GetInstance(instanceID);
            if (item != null)
                ItemStatComputer.Compute(item);
        }

        #endregion

        #region Debug

        [ContextMenu("Log Attachment State")]
        public void LogAttachmentState()
        {
            Debug.Log($"=== AttachmentSystem [{gameObject.name}] ===");
            var allItems = _inventorySystem != null ? _inventorySystem.GetAllItems() : null;
            if (allItems == null) { Debug.Log("  No inventory system."); return; }

            bool found = false;
            foreach (var item in allItems)
            {
                if (item.AttachedItems == null || item.AttachedItems.Length == 0) continue;
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                Debug.Log($"  {def?.DisplayName ?? item.DefinitionID} [{item.InstanceID[..8]}...]");
                for (int i = 0; i < item.AttachedItems.Length; i++)
                {
                    var attID = item.AttachedItems[i];
                    if (string.IsNullOrEmpty(attID))
                        Debug.Log($"    [{i}] — empty");
                    else
                    {
                        var att    = ItemDatabase.GetInstance(attID);
                        var attDef = att != null ? ItemDatabase.GetDefinition(att.DefinitionID) : null;
                        Debug.Log($"    [{i}] {attDef?.DisplayName ?? attID}");
                    }
                }
                found = true;
            }
            if (!found) Debug.Log("  No items with attachment slots.");
        }

        #endregion
    }
}
