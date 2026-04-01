using System.Collections;
using System.Collections.Generic;
using NightHunt.Core;
using NightHunt.Gameplay.Spectator;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Trung tÃ¢m xá»­ lÃ½ drag & drop, ghost, optimistic update.
    /// </summary>
    public class DragDropController : Singleton<DragDropController>
    {

        [Header("Ghost")] [SerializeField] private DragDropGhost _ghostPrefab;
        [SerializeField] private Canvas _dragCanvas;

        [Header("Dialogs")] [SerializeField] private DropQuantityDialog _dropQuantityDialog;

        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        private DragDropGhost _activeGhost;
        private ItemSlotView _sourceView;
        private UISlotId _sourceId;
        private UISlotState _sourceStateSnapshot;
        private UISlotState _targetStateSnapshot;

        private readonly DragDropValidator _validator = new DragDropValidator();
        private readonly Dictionary<UISlotId, ItemSlotView> _allSlots = new Dictionary<UISlotId, ItemSlotView>();

        [Header("Raycast")] [SerializeField] private GraphicRaycaster _raycaster;

        private ItemSlotView _currentHoverView;

        // FIX Bug 5: Guard flag â€“ OnDrop fires before OnEndDrag in Unity EventSystem.
        // When NotifyDropTarget succeeds, we set this to prevent EndDrag from starting
        // AnimateCancelAndRestore which would restore the source slot visual incorrectly.
        private bool _dropHandled;

private DropQuantityDialog GetDropQuantityDialog()
        {
            if (_dropQuantityDialog == null)
                Debug.LogError("[DragDropController] DropQuantityDialog is not assigned in the inspector!");

            return _dropQuantityDialog;
        }

        public void RegisterSlotView(ItemSlotView view)
        {
            if (view == null) return;
            _allSlots[view.SlotId] = view;
        }

        /// <summary>
        /// Reset toÃ n bá»™ state drag-drop (dÃ¹ng khi Ä‘á»•i current player, reload UI,...).
        /// </summary>
        public void ResetAll()
        {
            // Destroy active ghost immediately
            if (_activeGhost != null)
                Destroy(_activeGhost.gameObject);

            // Restore source slot if a drag was in progress
            // (e.g. player switches spectate target mid-drag â€“ don't leave the slot blank)
            if (_sourceView != null && _sourceStateSnapshot != null)
                _sourceView.SetState(_sourceStateSnapshot);

            // Clear hover highlight so the last-hovered slot doesn't stay highlighted
            if (_currentHoverView != null)
            {
                var clearState = CloneState(_currentHoverView.State);
                if (clearState != null)
                {
                    clearState.IsValidDropTarget = false;
                    _currentHoverView.SetState(clearState);
                }
            }

            _activeGhost = null;
            _sourceView = null;
            _sourceId = default;
            _sourceStateSnapshot = null;
            _targetStateSnapshot = null;
            _dropHandled = false;
            _currentHoverView = null;
        }

        #region Drag Flow

        public void BeginDrag(ItemSlotView sourceView, PointerEventData eventData)
        {
            if (sourceView == null)
                return;

            var spectate = SpectateManager.Instance;
            if (spectate == null || !spectate.IsCurrentPlayerLocal())
            {
                Log("BeginDrag blocked â€“ current player is not local.");
                return;
            }

            if (sourceView.State == null || sourceView.State.Item == null)
                return;

            _sourceView = sourceView;
            _sourceId = sourceView.SlotId;
            _sourceStateSnapshot = CloneState(sourceView.State);

            SpawnGhost(sourceView, eventData);
            sourceView.SetEmptyState(); // Clear vá» empty state (cÃ³ thá»ƒ restore náº¿u cancel)
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (_activeGhost == null || _dragCanvas == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_dragCanvas.transform,
                eventData.position,
                _dragCanvas.worldCamera,
                out var localPoint);

            _activeGhost.RectTransform.localPosition = localPoint;

            // Preview highlight target há»£p lá»‡
            if (_raycaster != null)
            {
                var results = new List<RaycastResult>();
                _raycaster.Raycast(eventData, results);

                ItemSlotView hoverView = null;
                foreach (var r in results)
                {
                    hoverView = ComponentResolver.Find<ItemSlotView>(r.gameObject)
                        .InParent()
                        .InRootChildren()
                        .OrLogWarning("[Auto] ItemSlotView not found")
                        .Resolve();
                    if (hoverView != null)
                        break;
                }

                if (hoverView != _currentHoverView)
                {
                    // Clear highlight cÅ©
                    if (_currentHoverView != null)
                    {
                        var oldState = CloneState(_currentHoverView.State);
                        if (oldState != null)
                        {
                            oldState.IsValidDropTarget = false;
                            _currentHoverView.SetState(oldState);
                        }
                    }

                    _currentHoverView = hoverView;

                    if (_currentHoverView != null && _sourceView != null)
                    {
                        var previewState = CloneState(_currentHoverView.State);
                        if (previewState != null)
                        {
                            bool canDrop = _validator.CanDrop(
                                _sourceId,
                                _currentHoverView.SlotId,
                                _sourceStateSnapshot,
                                _currentHoverView.State,
                                out _);
                            previewState.IsValidDropTarget = canDrop;
                            _currentHoverView.SetState(previewState);
                        }
                    }
                }
            }
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (_activeGhost == null)
            {
                // Ghost was never spawned or already destroyed â€“ restore source visual
                _sourceView?.SetState(_sourceStateSnapshot);
                ClearState();
                return;
            }

            // FIX Bug 5: NotifyDropTarget already handled this drop (OnDrop fires BEFORE OnEndDrag).
            // Do not start a second coroutine â€“ AnimateSuccessAndDestroy is already running.
            if (_dropHandled)
                return;

            // No valid drop target was notified â€“ cancel drag and restore source
            StartCoroutine(AnimateCancelAndRestore());
        }

        public void NotifyDropTarget(ItemSlotView targetView)
        {
            if (_sourceView == null || _activeGhost == null)
                return;

            var sourceState = _sourceStateSnapshot;
            var targetId = targetView.SlotId;
            var targetState = CloneState(targetView.State);

            _targetStateSnapshot = targetState;

            if (!_validator.CanDrop(_sourceId, targetId, sourceState, targetState, out var action))
            {
                StartCoroutine(AnimateCancelAndRestore());
                return;
            }

            // Mark drop as handled BEFORE coroutine â€“ EndDrag checks this flag
            // (OnDrop fires before OnEndDrag per Unity EventSystem spec)
            _dropHandled = true;

            // Optimistic local apply
            ApplyLocalAction(action, sourceState, targetState);

            // Animate ghost to target then destroy
            StartCoroutine(AnimateSuccessAndDestroy(targetView.transform.position));

            // Send to backend
            ApplyBackendAction(action, sourceState, targetState);
        }

        #endregion

        #region Ghost helpers

        private void SpawnGhost(ItemSlotView sourceView, PointerEventData eventData)
        {
            if (_ghostPrefab == null || _dragCanvas == null)
                return;

            _activeGhost = Instantiate(_ghostPrefab, _dragCanvas.transform);
            if (_activeGhost == null)
                return;
            _activeGhost.gameObject.SetActive(true);
            _activeGhost.SetupFromSlot(sourceView);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_dragCanvas.transform,
                eventData.position,
                _dragCanvas.worldCamera,
                out var localPoint);

            var rt = _activeGhost.RectTransform;
            rt.localPosition = localPoint;
            UITweenUtil.ScaleInstant(rt, 1f);

            // SetupFromSlot Ä‘Ã£ set alpha = 1f, khÃ´ng cáº§n SetAlpha(0f) ná»¯a
            var cg = ComponentResolver.Find<CanvasGroup>(_activeGhost)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CanvasGroup not found")
                .Resolve();
            if (cg != null)
            {
                UITweenUtil.FadeCanvasGroupInstant(cg, 1f);
            }
        }

        private IEnumerator AnimateCancelAndRestore()
        {
            if (_activeGhost != null && _sourceView != null)
            {
                var rt = _activeGhost.RectTransform;
                if (rt != null && _sourceView.transform != null)
                {
                    rt.position = _sourceView.transform.position;
                }

                var cg = ComponentResolver.Find<CanvasGroup>(_activeGhost)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] CanvasGroup not found")
                    .Resolve();
                if (cg != null)
                {
                    UITweenUtil.FadeCanvasGroupInstant(cg, 0f);
                }

                yield return null;

                // Check láº¡i sau yield vÃ¬ cÃ³ thá»ƒ bá»‹ destroy tá»« nÆ¡i khÃ¡c
                if (_activeGhost != null)
                {
                    Destroy(_activeGhost.gameObject);
                }
            }

            // Restore source view state náº¿u cÃ²n tá»“n táº¡i
            if (_sourceView != null && _sourceStateSnapshot != null)
            {
                _sourceView.SetState(_sourceStateSnapshot);
            }

            ClearState();
        }

        private IEnumerator AnimateSuccessAndDestroy(Vector3 targetWorldPos)
        {
            if (_activeGhost != null)
            {
                var rt = _activeGhost.RectTransform;
                rt.position = targetWorldPos;
                var cg = ComponentResolver.Find<CanvasGroup>(_activeGhost)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] CanvasGroup not found")
                    .Resolve();
                if (cg != null)
                {
                    UITweenUtil.FadeCanvasGroupInstant(cg, 0f);
                }

                yield return null;
                Destroy(_activeGhost.gameObject);
            }

            ClearState();
        }

        private void ClearState()
        {
            // Clear IsValidDropTarget highlight on the last hovered slot BEFORE nulling the reference.
            // Without this the slot keeps its green highlight after drag ends (cancel or success).
            if (_currentHoverView != null)
            {
                var clearState = CloneState(_currentHoverView.State);
                if (clearState != null)
                {
                    clearState.IsValidDropTarget = false;
                    _currentHoverView.SetState(clearState);
                }

                _currentHoverView = null;
            }

            _activeGhost = null;
            _sourceView = null;
            _sourceStateSnapshot = null;
            _targetStateSnapshot = null;
            _dropHandled = false; // FIX Bug 5: always reset for next drag cycle
        }

        #endregion

        #region Local + Backend actions

        private void ApplyLocalAction(DropAction action, UISlotState sourceState, UISlotState targetState)
        {
            if (!_allSlots.TryGetValue(action.Source, out var sourceView))
                sourceView = _sourceView;

            _allSlots.TryGetValue(action.Target, out var targetView);

            switch (action.Type)
            {
                case DropActionType.Move:
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null) targetView.SetState(sourceState);
                    break;

                case DropActionType.Swap:
                    if (sourceView != null) sourceView.SetState(targetState);
                    if (targetView != null) targetView.SetState(sourceState);
                    break;

                case DropActionType.Stack:
                    // Optimistic: Clear source, update target quantity
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && targetState != null && sourceState != null)
                    {
                        var newState = CloneState(targetState);
                        var sourceDef = ItemDatabase.GetDefinition(sourceState.Item.DefinitionID);
                        if (sourceDef != null)
                        {
                            int availableSpace = sourceDef.MaxStackSize - targetState.Item.Quantity;
                            int amountToStack = Mathf.Min(availableSpace, sourceState.Item.Quantity);
                            newState.StackCount = targetState.Item.Quantity + amountToStack;
                            targetView.SetState(newState);
                        }
                    }

                    break;

                case DropActionType.Equip:
                    // Optimistic: clear inventory source, immediately preview item in equipment slot.
                    // Server OnItemEquipped confirms and may override if validation fails.
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.EquipWeapon:
                    // Optimistic: clear inventory source, immediately preview weapon in weapon slot.
                    // If slot was already occupied, OnWeaponUnequipped(old) will fire and restore displaced weapon.
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.Unequip:
                    // Optimistic: clear equipment source, show item in target inventory slot.
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.UnequipWeapon:
                    // Optimistic: clear weapon slot source, show weapon in target inventory slot.
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.Detach:
                    // Optimistic: clear attachment slot source, preview item in target inventory slot.
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.Attach:
                case DropActionType.DropToWorld:
                case DropActionType.Trash:
                    // Backend events fully drive these states (world spawn, item deletion).
                    // Only clear source so the item feels like it has left.
                    if (sourceView != null) sourceView.SetEmptyState();
                    break;

                
            }
        }

        private void ApplyBackendAction(DropAction action, UISlotState sourceState, UISlotState targetState)
        {
            var spectate = SpectateManager.Instance;
            var player = spectate != null ? spectate.GetCurrentPlayer() : null;
            var bridge = player != null ? player.GamePlaySystemBridge : null;
            if (bridge == null || !bridge.IsReady)
            {
                Log("Backend bridge not ready â€“ cannot apply drop action.");
                return;
            }

            var item = sourceState.Item;
            if (item == null)
                return;

            switch (action.Type)
            {
                case DropActionType.Move:
                    if (action.Target.Type == UISlotType.Inventory)
                        bridge.Inventory.MoveItem(item.InstanceID, action.Target.Index);
                    break;

                case DropActionType.Stack:
                    // Stack items vÃ o target
                    if (action.Source.Type == UISlotType.Inventory &&
                        action.Target.Type == UISlotType.Inventory &&
                        targetState != null && targetState.Item != null)
                    {
                        bridge.Inventory.StackItems(targetState.Item.InstanceID, item.InstanceID);
                    }

                    break;

                case DropActionType.Swap:
                    if (action.Source.Type == UISlotType.Inventory &&
                        action.Target.Type == UISlotType.Inventory &&
                        targetState != null && targetState.Item != null)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][SwapItems] " +
                                      $"item1={item.DefinitionID} ({item.InstanceID}) " +
                                      $"item2={targetState.Item.DefinitionID} ({targetState.Item.InstanceID}) " +
                                      $"from={action.Source} to={action.Target}");
                        }

                        bridge.SwapItems(item.InstanceID, targetState.Item.InstanceID);
                    }
                    else if (action.Source.Type == UISlotType.Equipment &&
                             action.Target.Type == UISlotType.Equipment &&
                             action.Source.EquipmentSlot.HasValue &&
                             action.Target.EquipmentSlot.HasValue)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][SwapEquipment] " +
                                      $"slot1={action.Source.EquipmentSlot.Value} " +
                                      $"slot2={action.Target.EquipmentSlot.Value} " +
                                      $"from={action.Source} to={action.Target}");
                        }

                        bridge.Equipment.SwapEquipment(
                            action.Source.EquipmentSlot.Value,
                            action.Target.EquipmentSlot.Value);
                    }
                    else if (action.Source.Type == UISlotType.Weapon &&
                             action.Target.Type == UISlotType.Weapon &&
                             action.Source.WeaponSlot.HasValue &&
                             action.Target.WeaponSlot.HasValue)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][SwapWeapons] " +
                                      $"slot1={action.Source.WeaponSlot.Value} " +
                                      $"slot2={action.Target.WeaponSlot.Value} " +
                                      $"from={action.Source} to={action.Target}");
                        }

                        bridge.Weapon.SwapWeapons(
                            action.Source.WeaponSlot.Value,
                            action.Target.WeaponSlot.Value);
                    }
                    else if (action.Source.Type == UISlotType.Attachment &&
                             action.Target.Type == UISlotType.Attachment &&
                             !string.IsNullOrEmpty(action.Source.ParentInstanceID) &&
                             !string.IsNullOrEmpty(action.Target.ParentInstanceID) &&
                             targetState != null && targetState.Item != null)
                    {
                        // Swap attachments giá»¯a 2 slots
                        var attachmentSystem = GetAttachmentSystem();
                        if (attachmentSystem != null)
                        {
                            attachmentSystem.SwapAttachments(
                                action.Source.ParentInstanceID,
                                action.Source.Index,
                                action.Target.ParentInstanceID,
                                action.Target.Index);
                        }
                    }

                    break;

                case DropActionType.Equip:
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                            Debug.Log($"[DragDropController] [UIâ†’Server][Equip] " +
                                  $"item={item.DefinitionID} ({item.InstanceID}) " +
                                  $"from={action.Source} to={action.Target}");
                    }

                    bridge.EquipItem(item.InstanceID);
                    break;

                case DropActionType.EquipWeapon:
                    // FIX Bug 2a: use EquipWeaponToSlot so the weapon lands in the exact
                    // slot the player dragged to, not the first available slot.
                    if (action.Target.WeaponSlot.HasValue)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][EquipWeapon] "
                                      + $"item={item.DefinitionID} ({item.InstanceID}) "
                                      + $"from={action.Source} to={action.Target} slot={action.Target.WeaponSlot.Value}");
                        }

                        bridge.EquipWeaponToSlot(item.InstanceID, action.Target.WeaponSlot.Value);
                    }
                    else
                    {
                        // Fallback: auto-select slot (no specific target slot info)
                        bridge.EquipWeapon(item.InstanceID);
                    }

                    break;

                case DropActionType.Unequip:
                    if (action.Source.EquipmentSlot.HasValue)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][Unequip] " +
                                      $"slot={action.Source.EquipmentSlot.Value} " +
                                      $"source={action.Source} target={action.Target}");
                        }

                        bridge.UnequipItem(action.Source.EquipmentSlot.Value);
                    }

                    break;

                case DropActionType.UnequipWeapon:
                    if (action.Source.WeaponSlot.HasValue)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][UnequipWeapon] " +
                                      $"slot={action.Source.WeaponSlot.Value} " +
                                      $"source={action.Source} target={action.Target}");
                        }

                        bridge.UnequipWeapon(action.Source.WeaponSlot.Value);
                    }

                    break;

                

                case DropActionType.DropToWorld:
                    // Náº¿u stack Ä‘á»§ lá»›n (>=3), má»Ÿ dialog chá»n sá»‘ lÆ°á»£ng drop thay vÃ¬ luÃ´n drop full stack.
                    if (item.Quantity > 2 && TryShowDropQuantityDialog(item, qty =>
                            ExecuteDropToWorld(bridge, action.Source, item, qty)))
                    {
                        return;
                    }

                    ExecuteDropToWorld(bridge, action.Source, item, item.Quantity);
                    break;

                case DropActionType.Attach:
                    // Attach item vÃ o attachment slot
                    if (action.Target.Type == UISlotType.Attachment)
                    {
                        var attachmentSystem = GetAttachmentSystem();
                        if (attachmentSystem != null && !string.IsNullOrEmpty(action.Target.ParentInstanceID))
                        {
                            attachmentSystem.AttachItem(
                                item.InstanceID,
                                action.Target.ParentInstanceID,
                                action.Target.Index);
                        }
                    }

                    break;

                case DropActionType.Detach:
                    // Detach attachment vá» inventory
                    if (action.Source.Type == UISlotType.Attachment)
                    {
                        var attachmentSystem = GetAttachmentSystem();
                        if (attachmentSystem != null && !string.IsNullOrEmpty(action.Source.ParentInstanceID))
                        {
                            attachmentSystem.DetachItem(
                                action.Source.ParentInstanceID,
                                action.Source.Index);
                        }
                    }

                    break;

                case DropActionType.Trash:
                    // Khi trash stack lá»›n (>=3), cho phÃ©p ngÆ°á»i chÆ¡i chá»n sá»‘ lÆ°á»£ng xoÃ¡.
                    if (item.Quantity > 2 && TryShowDropQuantityDialog(item, qty =>
                            ExecuteTrash(bridge, action.Source, item, qty)))
                    {
                        return;
                    }

                    ExecuteTrash(bridge, action.Source, item, item.Quantity);
                    break;
            }
        }

        /// <summary>
        /// Hiá»ƒn thá»‹ DropQuantityDialog (náº¿u cÃ³) vÃ  thá»±c thi callback khi ngÆ°á»i chÆ¡i confirm.
        /// Tráº£ vá» true náº¿u dialog Ä‘Ã£ Ä‘Æ°á»£c hiá»ƒn thá»‹, false náº¿u khÃ´ng cÃ³ dialog.
        /// </summary>
        private bool TryShowDropQuantityDialog(ItemInstance item, System.Action<int> onConfirmed)
        {
            var dialog = GetDropQuantityDialog();
            if (dialog == null || item == null || onConfirmed == null)
                return false;

            void HandleConfirmed(ItemInstance confirmedItem, int quantity)
            {
                dialog.OnDropConfirmed -= HandleConfirmed;
                dialog.OnCanceled -= HandleCanceled;

                // Chá»‰ xá»­ lÃ½ náº¿u Ä‘Ãºng instance.
                if (confirmedItem != null && confirmedItem.InstanceID == item.InstanceID)
                {
                    onConfirmed.Invoke(quantity);
                }
            }

            void HandleCanceled()
            {
                dialog.OnDropConfirmed -= HandleConfirmed;
                dialog.OnCanceled -= HandleCanceled;
            }

            dialog.OnDropConfirmed += HandleConfirmed;
            dialog.OnCanceled += HandleCanceled;
            dialog.Show(item);

            return true;
        }

        /// <summary>
        /// Thá»±c thi logic DropToWorld cho má»™t lÆ°á»£ng cá»¥ thá»ƒ.
        /// </summary>
        private void ExecuteDropToWorld(IGameplayBridge bridge, UISlotId source, ItemInstance item, int quantity)
        {
            if (bridge == null || item == null || quantity <= 0)
                return;

            // BUG 4 FIX: support drop-to-world from any source slot.
            // For non-inventory sources, unequip/detach first then drop.
            switch (source.Type)
            {
                case UISlotType.Inventory:
                    bridge.DropItem(item.InstanceID, quantity);
                    break;

                case UISlotType.Equipment:
                    if (source.EquipmentSlot.HasValue)
                    {
                        // Unequip (detaches attachments) then drop from inventory
                        bridge.UnequipItem(source.EquipmentSlot.Value);
                        bridge.DropItem(item.InstanceID, quantity);
                    }

                    break;

                case UISlotType.Weapon:
                    if (source.WeaponSlot.HasValue)
                    {
                        bridge.UnequipWeapon(source.WeaponSlot.Value);
                        bridge.DropItem(item.InstanceID, quantity);
                    }

                    break;

                case UISlotType.Attachment:
                    // Detach to inventory first, then drop
                    var attachSys = GetAttachmentSystem();
                    if (attachSys != null && !string.IsNullOrEmpty(source.ParentInstanceID))
                        attachSys.DetachItem(source.ParentInstanceID, source.Index);
                    bridge.DropItem(item.InstanceID, quantity);
                    break;

                default:
                    Log($"[DropToWorld] Unhandled source type={source.Type}");
                    break;
            }
        }

        /// <summary>
        /// Thá»±c thi logic Trash cho má»™t lÆ°á»£ng cá»¥ thá»ƒ.
        /// </summary>
        private void ExecuteTrash(IGameplayBridge bridge, UISlotId source, ItemInstance item, int quantity)
        {
            if (bridge == null || item == null || quantity <= 0)
                return;

            // Trash cáº§n xá»­ lÃ½ khÃ¡c nhau tuá»³ nguá»“n:
            // - Inventory: xoÃ¡ trá»±c tiáº¿p khá»i inventory.
            // - Equipment / Weapon: unequip trÆ°á»›c rá»“i xoÃ¡ khá»i inventory.
            // - Attachment: detach vá» inventory (náº¿u cÃ³ system) â€“ user cÃ³ thá»ƒ trash tiáº¿p tá»« inventory.
            switch (source.Type)
            {
                case UISlotType.Inventory:
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                            Debug.Log($"[DragDropController] [UIâ†’Server][Trash Inventory] " +
                                  $"item={item.DefinitionID} ({item.InstanceID}) " +
                                  $"from={source} qty={quantity}");
                    }

                    bridge.RemoveItem(item.InstanceID, quantity);
                    break;

                case UISlotType.Equipment:
                    if (source.EquipmentSlot.HasValue)
                    {
                        var slot = source.EquipmentSlot.Value;
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][Trash Equipment] " +
                                      $"slot={slot}, item={item.DefinitionID} ({item.InstanceID}) " +
                                      $"from={source} qty={quantity}");
                        }

                        // 1) Unequip Ä‘á»ƒ tráº£ item vá» inventory.
                        bridge.UnequipItem(slot);
                        // 2) XoÃ¡ instance khá»i inventory.
                        bridge.RemoveItem(item.InstanceID, quantity);
                    }

                    break;

                case UISlotType.Weapon:
                    if (source.WeaponSlot.HasValue)
                    {
                        var slot = source.WeaponSlot.Value;
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][Trash Weapon] " +
                                      $"slot={slot}, item={item.DefinitionID} ({item.InstanceID}) " +
                                      $"from={source} qty={quantity}");
                        }

                        // 1) Unequip weapon Ä‘á»ƒ tráº£ vá» inventory.
                        bridge.UnequipWeapon(slot);
                        // 2) XoÃ¡ instance khá»i inventory.
                        bridge.RemoveItem(item.InstanceID, quantity);
                    }

                    break;

                case UISlotType.Attachment:
                    // ÄÆ¡n giáº£n hoÃ¡: detach attachment vá» inventory,
                    // sau Ä‘Ã³ player cÃ³ thá»ƒ trash tiáº¿p tá»« inventory náº¿u muá»‘n.
                    var attachmentSystem = GetAttachmentSystem();
                    if (attachmentSystem != null &&
                        !string.IsNullOrEmpty(source.ParentInstanceID))
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                        {
                            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                                Debug.Log($"[DragDropController] [UIâ†’Server][Trash Attachment â†’ Detach] " +
                                      $"parent={source.ParentInstanceID}, slotIndex={source.Index}, " +
                                      $"attachmentItem={item.DefinitionID} ({item.InstanceID})");
                        }

                        attachmentSystem.DetachItem(
                            source.ParentInstanceID,
                            source.Index);
                    }

                    break;

                default:
                    if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    {
                        if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                            Debug.Log($"[DragDropController] [UIâ†’Server][Trash] Unsupported source type={source.Type} " +
                                  $"item={item.DefinitionID} ({item.InstanceID}) from={source} qty={quantity}");
                    }

                    break;
            }
        }

        private IAttachmentSystem GetAttachmentSystem()
        {
            var spectate = SpectateManager.Instance;
            if (spectate == null) return null;

            var player = spectate.GetCurrentPlayer();
            if (player == null) return null;

            return ComponentResolver.Find<IAttachmentSystem>(player)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IAttachmentSystem not found")
                .Resolve();
        }

        #endregion

        private static UISlotState CloneState(UISlotState state)
        {
            if (state == null) return null;
            return new UISlotState
            {
                Item = state.Item,
                IsLocked = state.IsLocked,
                IsHighlight = state.IsHighlight,
                IsValidDropTarget = state.IsValidDropTarget,
                Icon = state.Icon,
                BackgroundColor = state.BackgroundColor,
                StackCount = state.StackCount
            };
        }

        private void Log(string msg)
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log("[DragDropController] " + msg);
        }
    }
}

