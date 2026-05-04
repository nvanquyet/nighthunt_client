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
using NightHunt.UI;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Central controller for drag-and-drop, ghost preview, and optimistic UI updates.
    ///
    /// FIXES applied:
    ///   - Ghost pointer offset now correctly computed so item doesn't jump on drag start.
    ///   - _allSlots cleaned up properly when views are unregistered.
    ///   - ClearState() no longer leaves stale slot references.
    /// </summary>
    public class DragDropController : Singleton<DragDropController>
    {
        private const string DragLogPrefix = "[DRAG_SLOT]";

        [Header("Ghost")] [SerializeField] private DragDropGhost _ghostPrefab;
        [SerializeField] private Canvas _dragCanvas;

        [Header("Dialogs")] [SerializeField] private DropQuantityDialog _dropQuantityDialog;


        private DragDropGhost _activeGhost;
        private ItemSlotView  _sourceView;
        private UISlotId      _sourceId;
        private UISlotState   _sourceStateSnapshot;
        private UISlotState   _targetStateSnapshot;

        private readonly DragDropValidator _validator = new DragDropValidator();
        private readonly Dictionary<UISlotId, ItemSlotView> _allSlots = new Dictionary<UISlotId, ItemSlotView>();

        /// <summary>
        /// Quick accessor for the local player's IWeaponSystem.
        /// Used by DragDropValidator to call CanEquipInSlot during drag-over validation.
        /// </summary>
        public IWeaponSystem WeaponSystem
        {
            get
            {
                if (_uiContext?.Bridge?.Weapon != null)
                    return _uiContext.Bridge.Weapon;

                var player = SpectateManager.Instance?.GetCurrentPlayer();
                return player?.GamePlaySystemBridge?.Weapon;
            }
        }

        [Header("Raycast")] [SerializeField] private GraphicRaycaster _raycaster;

        private ItemSlotView _currentHoverView;
        private UIPlayerContext _uiContext;

        // FIX: Ghost pointer offset — stores the delta between the slot centre and the actual
        // pointer position in canvas-local space so the ghost doesn't jump on drag start.
        private Vector2 _ghostPointerOffset;

        // FIX Bug 5: Guard flag – OnDrop fires before OnEndDrag in Unity EventSystem.
        // When NotifyDropTarget succeeds, we set this to prevent EndDrag from starting
        // AnimateCancelAndRestore which would restore the source slot visual incorrectly.
        private bool _dropHandled;

        private DropQuantityDialog GetDropQuantityDialog()
        {
            if (_dropQuantityDialog == null)
            {
                // Scene-level UI singleton — acceptable lazy fallback for a missing Inspector assignment.
                _dropQuantityDialog = FindFirstObjectByType<DropQuantityDialog>(FindObjectsInactive.Include);
            }

            return _dropQuantityDialog;
        }

        public void BindContext(UIPlayerContext context)
        {
            _uiContext = context;
            Log($"{DragLogPrefix} BindContext owner={context?.IsOwner.ToString() ?? "null"} ready={context?.IsReady.ToString() ?? "null"} player={context?.Player?.name ?? "null"}");
        }

        public void RegisterSlotView(ItemSlotView view)
        {
            if (view == null) return;
            EnsureSlotInput(view);
            _allSlots[view.SlotId] = view;
            Log($"{DragLogPrefix} RegisterSlot id={view.SlotId} view={view.name} input={(view.GetComponent<ItemSlotInput>() != null ? "ok" : "missing")}");
        }

        /// <summary>
        /// Remove a slot from the drag-drop registry.
        /// Call when a card is hidden or destroyed (e.g. weapon unequipped).
        /// A drag in progress targeting this slot is safely ignored on EndDrag.
        /// </summary>
        public void UnregisterSlotView(ItemSlotView view)
        {
            if (view == null) return;

            // FIX: Only remove the entry if it still points to THIS view instance.
            // Without this guard, re-registering a new view for the same SlotId
            // (e.g. after teardown/rebuild) and then unregistering the old view
            // would accidentally remove the freshly registered entry.
            if (_allSlots.TryGetValue(view.SlotId, out var registered) && registered == view)
                _allSlots.Remove(view.SlotId);
        }

        /// <summary>
        /// Reset all drag-drop state (call when switching players, reloading UI, etc.).
        /// </summary>
        public void ResetAll()
        {
            // Destroy active ghost immediately
            if (_activeGhost != null)
                Destroy(_activeGhost.gameObject);

            // Restore source slot if a drag was in progress
            // (e.g. player switches spectate target mid-drag – don't leave the slot blank)
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
            _sourceId = default;
        }

        #region Drag Flow

        public void BeginDrag(ItemSlotView sourceView, PointerEventData eventData)
        {
            if (sourceView == null)
                return;

            if (sourceView.IsLocked)
            {
                Log($"{DragLogPrefix} BeginDrag blocked - source locked source={sourceView.SlotId}");
                return;
            }

            if (_uiContext != null && !_uiContext.IsOwner)
            {
                Log($"{DragLogPrefix} BeginDrag blocked - UI context is not owner source={sourceView.SlotId} player={_uiContext.Player?.name ?? "null"}");
                return;
            }

            var spectate = SpectateManager.Instance;
            if (_uiContext == null && spectate != null && !spectate.IsCurrentPlayerLocal())
            {
                Log("BeginDrag blocked – current player is not local.");
                return;
            }

            if (sourceView.State == null || sourceView.State.Item == null)
            {
                Log($"{DragLogPrefix} BeginDrag ignored - empty source={sourceView.SlotId}");
                return;
            }

            _sourceView          = sourceView;
            _sourceId            = sourceView.SlotId;
            _sourceStateSnapshot = CloneState(sourceView.State);

            SpawnGhost(sourceView, eventData);
            if (_activeGhost == null)
            {
                Log($"{DragLogPrefix} BeginDrag blocked - no drag canvas/ghost available.");
                ClearState();
                return;
            }

            sourceView.SetEmptyState();
            Log($"{DragLogPrefix} BeginDrag source={_sourceId} pointer={eventData.position}");

            // Hide tooltip at drag start.
            FindFirstObjectByType<ItemTooltip>(FindObjectsInactive.Include)?.HideIfNotDragVisible();
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (_activeGhost == null || _dragCanvas == null)
                return;

            MoveGhostToPointer(eventData);

            // Preview-highlight the valid drop target.
            if (_raycaster != null)
            {
                var results = new List<RaycastResult>();
                _raycaster.Raycast(eventData, results);

                ItemSlotView hoverView = ResolveSlotFromPointer(eventData, results);

                if (hoverView != _currentHoverView)
                {
                    // Clear the previous highlight.
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
                // Ghost was never spawned or already destroyed – restore source visual
                _sourceView?.SetState(_sourceStateSnapshot);
                Log($"{DragLogPrefix} EndDrag without ghost -> restore source={_sourceId}");
                ClearState();
                return;
            }

            // FIX Bug 5: NotifyDropTarget already handled this drop (OnDrop fires BEFORE OnEndDrag).
            if (_dropHandled)
                return;

            var raycastResults = RaycastUi(eventData);
            var fallbackTarget = ResolveSlotFromPointer(eventData, raycastResults);
            if (fallbackTarget != null && fallbackTarget != _sourceView)
            {
                Log($"{DragLogPrefix} EndDrag fallback target source={_sourceId} target={fallbackTarget.SlotId} hits={FormatRaycastHits(raycastResults, 6)}");
                NotifyDropTarget(fallbackTarget);
                return;
            }

            // --- WORLD DROP LOGIC ---
            bool droppedOutsideUI = true;
            if (raycastResults.Count > 0)
            {
                foreach (var r in raycastResults)
                {
                    if (r.gameObject.GetComponentInParent<InventoryScreen>() != null ||
                        r.gameObject.GetComponentInParent<WeaponEquipmentPanel>() != null)
                    {
                        droppedOutsideUI = false;
                        break;
                    }
                }
            }

            if (droppedOutsideUI && _sourceView != null && _sourceStateSnapshot?.Item != null)
            {
                var action = new DropAction { Source = _sourceId, Target = _sourceId, Type = DropActionType.DropToWorld };
                _dropHandled = true;
                Log($"{DragLogPrefix} EndDrag -> world drop source={_sourceId} pointer={eventData.position}");

                ApplyLocalAction(action, _sourceStateSnapshot, null);
                StartCoroutine(AnimateSuccessAndDestroy(_activeGhost.transform.position));
                ApplyBackendAction(action, _sourceStateSnapshot, null);
                return;
            }
            // ------------------------

            Log($"{DragLogPrefix} EndDrag -> cancel source={_sourceId} pointer={eventData.position} hits={FormatRaycastHits(raycastResults, 6)}");
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
                _dropHandled = true;
                Log($"{DragLogPrefix} Invalid drop source={_sourceId} target={targetId} sourceItem={sourceState?.Item?.DefinitionID ?? "null"} targetItem={targetState?.Item?.DefinitionID ?? "null"}");
                ShowInvalidDropToast(sourceState?.Item, targetId);
                StartCoroutine(AnimateCancelAndRestore());
                return;
            }

            _dropHandled = true;
            Log($"{DragLogPrefix} NotifyDropTarget source={_sourceId} target={targetId} action={action.Type}");

            ApplyLocalAction(action, sourceState, targetState);
            StartCoroutine(AnimateSuccessAndDestroy(targetView.transform.position));
            ApplyBackendAction(action, sourceState, targetState);
        }

        #endregion

        #region Ghost helpers

        private void SpawnGhost(ItemSlotView sourceView, PointerEventData eventData)
        {
            if (_dragCanvas == null)
                _dragCanvas = ResolveDragCanvas(sourceView);

            if (_dragCanvas == null)
                return;

            if (_raycaster == null)
                _raycaster = _dragCanvas.GetComponent<GraphicRaycaster>() ?? _dragCanvas.rootCanvas?.GetComponent<GraphicRaycaster>();

            _activeGhost = _ghostPrefab != null
                ? Instantiate(_ghostPrefab, _dragCanvas.transform)
                : CreateRuntimeGhost(_dragCanvas.transform);
            if (_activeGhost == null)
                return;
            _activeGhost.gameObject.SetActive(true);
            _activeGhost.SetupFromSlot(sourceView);

            var canvasRect = (RectTransform)_dragCanvas.transform;
            var rt = _activeGhost.RectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = ResolveGhostIconSize(sourceView.RectTransform.rect.size);

            // Compute the canvas-local pointer position for the icon-only ghost.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out var pointerLocalPoint);

            // Icon-only ghost follows the pointer center. Full card/card background sizes
            // are intentionally not cloned into the drag visual.
            _ghostPointerOffset = Vector2.zero;
            rt.anchoredPosition = pointerLocalPoint + _ghostPointerOffset;

            UITweenUtil.ScaleInstant(rt, 1f);

            var cg = ComponentResolver.Find<CanvasGroup>(_activeGhost)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[DragDropController] CanvasGroup not found on ghost")
                .Resolve();
            if (cg != null)
            {
                UITweenUtil.FadeCanvasGroupInstant(cg, 1f);
            }
        }

        private static Canvas ResolveDragCanvas(ItemSlotView sourceView)
        {
            if (sourceView == null)
                return null;

            var canvas = sourceView.GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

            return FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        private static DragDropGhost CreateRuntimeGhost(Transform parent)
        {
            var go = new GameObject("Runtime Drag Ghost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(DragDropGhost));
            go.transform.SetParent(parent, false);
            return go.GetComponent<DragDropGhost>();
        }

        private void MoveGhostToPointer(PointerEventData eventData)
        {
            if (_activeGhost == null || _dragCanvas == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_dragCanvas.transform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                _activeGhost.RectTransform.anchoredPosition = localPoint + _ghostPointerOffset;
            }
        }

        private IEnumerator AnimateCancelAndRestore()
        {
            if (_activeGhost != null && _sourceView != null)
            {
                var config = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance;
                float snapDuration = config?.GhostSnapBackDuration ?? 0.18f;
                yield return _activeGhost.SnapBackToOrigin(
                    _sourceView.transform as RectTransform, snapDuration);
            }

            if (_sourceView != null && _sourceStateSnapshot != null)
                _sourceView.SetState(_sourceStateSnapshot);

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
                    .OrLogWarning("[DragDropController] CanvasGroup not found on ghost")
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

            _activeGhost          = null;
            _sourceView           = null;
            _sourceId             = default;
            _sourceStateSnapshot  = null;
            _targetStateSnapshot  = null;
            _dropHandled          = false;
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
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.EquipWeapon:
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.Unequip:
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.UnequipWeapon:
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.Detach:
                    if (sourceView != null) sourceView.SetEmptyState();
                    if (targetView != null && sourceState != null) targetView.SetState(CloneState(sourceState));
                    break;

                case DropActionType.Attach:
                case DropActionType.LootToInventory:
                case DropActionType.DropToWorld:
                    if (sourceView != null) sourceView.SetEmptyState();
                    break;
            }
        }

        private void ApplyBackendAction(DropAction action, UISlotState sourceState, UISlotState targetState)
        {
            var bridge = ResolveGameplayBridge();
            if (bridge == null || !bridge.IsReady)
            {
                Log("Backend bridge not ready – cannot apply drop action.");
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
                        LogDebug($"[UI→Server][SwapItems] item1={item.DefinitionID} ({item.InstanceID}) " +
                                 $"item2={targetState.Item.DefinitionID} ({targetState.Item.InstanceID}) " +
                                 $"from={action.Source} to={action.Target}");
                        bridge.SwapItems(item.InstanceID, targetState.Item.InstanceID);
                    }
                    else if (action.Source.Type == UISlotType.Equipment &&
                             action.Target.Type == UISlotType.Equipment &&
                             action.Source.EquipmentSlot.HasValue &&
                             action.Target.EquipmentSlot.HasValue)
                    {
                        LogDebug($"[UI→Server][SwapEquipment] slot1={action.Source.EquipmentSlot.Value} " +
                                 $"slot2={action.Target.EquipmentSlot.Value}");
                        bridge.Equipment.SwapEquipment(
                            action.Source.EquipmentSlot.Value,
                            action.Target.EquipmentSlot.Value);
                    }
                    else if (action.Source.Type == UISlotType.Weapon &&
                             action.Target.Type == UISlotType.Weapon &&
                             action.Source.WeaponSlot.HasValue &&
                             action.Target.WeaponSlot.HasValue)
                    {
                        LogDebug($"[UI→Server][SwapWeapons] slot1={action.Source.WeaponSlot.Value} " +
                                 $"slot2={action.Target.WeaponSlot.Value}");
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
                    LogDebug($"[UI→Server][Equip] item={item.DefinitionID} ({item.InstanceID}) from={action.Source} to={action.Target}");
                    bridge.EquipItem(item.InstanceID);
                    break;

                case DropActionType.EquipWeapon:
                    if (action.Target.WeaponSlot.HasValue)
                    {
                        LogDebug($"[UI→Server][EquipWeapon] item={item.DefinitionID} ({item.InstanceID}) slot={action.Target.WeaponSlot.Value}");
                        bridge.EquipWeaponToSlot(item.InstanceID, action.Target.WeaponSlot.Value);
                    }
                    else
                    {
                        bridge.EquipWeapon(item.InstanceID);
                    }
                    break;

                case DropActionType.Unequip:
                    if (action.Source.EquipmentSlot.HasValue)
                    {
                        LogDebug($"[UI→Server][Unequip] slot={action.Source.EquipmentSlot.Value}");
                        bridge.UnequipItem(action.Source.EquipmentSlot.Value);
                    }
                    break;

                case DropActionType.UnequipWeapon:
                    if (action.Source.WeaponSlot.HasValue)
                    {
                        LogDebug($"[UI→Server][UnequipWeapon] slot={action.Source.WeaponSlot.Value}");
                        bridge.UnequipWeapon(action.Source.WeaponSlot.Value);
                    }
                    break;

                case DropActionType.DropToWorld:
                    if (item.Quantity > 2 && TryShowDropQuantityDialog(item, qty =>
                            ExecuteDropToWorld(bridge, action.Source, item, qty)))
                    {
                        return;
                    }
                    ExecuteDropToWorld(bridge, action.Source, item, item.Quantity);
                    break;

                case DropActionType.Attach:
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

                case DropActionType.LootToInventory:
                    if (action.Source.Type == UISlotType.Loot)
                    {
                        LootContainerUI.Instance?.TakeItem(action.Source.Index, item.Quantity);
                    }
                    break;
            }
        }

        private bool TryShowDropQuantityDialog(ItemInstance item, System.Action<int> onConfirmed)
        {
            var dialog = GetDropQuantityDialog();
            if (dialog == null || item == null || onConfirmed == null)
                return false;

            var capturedSourceView  = _sourceView;
            var capturedSourceState = _sourceStateSnapshot != null
                ? CloneState(_sourceStateSnapshot)
                : null;

            dialog.Show(
                item,
                qty =>
                {
                    Log($"Drop confirmed: {item.DefinitionID} x{qty}");
                    onConfirmed.Invoke(qty);
                },
                () =>
                {
                    if (capturedSourceView != null && capturedSourceState != null)
                    {
                        capturedSourceView.SetState(capturedSourceState);
                        Log($"Drop cancelled — restored slot for {item.DefinitionID}");
                    }
                });

            return true;
        }

        private void ExecuteDropToWorld(IGameplayBridge bridge, UISlotId source, ItemInstance item, int quantity)
        {
            if (bridge == null || item == null || quantity <= 0)
                return;

            switch (source.Type)
            {
                case UISlotType.Inventory:
                    bridge.DropItem(item.InstanceID, quantity);
                    break;

                case UISlotType.Equipment:
                    if (source.EquipmentSlot.HasValue)
                    {
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

        private void ExecuteTrash(IGameplayBridge bridge, UISlotId source, ItemInstance item, int quantity)
        {
            if (bridge == null || item == null || quantity <= 0)
                return;

            switch (source.Type)
            {
                case UISlotType.Inventory:
                    LogDebug($"[UI→Server][Trash Inventory] item={item.DefinitionID} ({item.InstanceID}) qty={quantity}");
                    bridge.RemoveItem(item.InstanceID, quantity);
                    break;

                case UISlotType.Equipment:
                    if (source.EquipmentSlot.HasValue)
                    {
                        var slot = source.EquipmentSlot.Value;
                        LogDebug($"[UI→Server][Trash Equipment] slot={slot}, item={item.DefinitionID} ({item.InstanceID}) qty={quantity}");
                        bridge.UnequipItem(slot);
                        bridge.RemoveItem(item.InstanceID, quantity);
                    }
                    break;

                case UISlotType.Weapon:
                    if (source.WeaponSlot.HasValue)
                    {
                        var slot = source.WeaponSlot.Value;
                        LogDebug($"[UI→Server][Trash Weapon] slot={slot}, item={item.DefinitionID} ({item.InstanceID}) qty={quantity}");
                        bridge.UnequipWeapon(slot);
                        bridge.RemoveItem(item.InstanceID, quantity);
                    }
                    break;

                case UISlotType.Attachment:
                    var attachmentSystem = GetAttachmentSystem();
                    if (attachmentSystem != null && !string.IsNullOrEmpty(source.ParentInstanceID))
                    {
                        LogDebug($"[UI→Server][Trash Attachment→Detach] parent={source.ParentInstanceID}, idx={source.Index}");
                        attachmentSystem.DetachItem(source.ParentInstanceID, source.Index);
                    }
                    break;

                default:
                    LogDebug($"[UI→Server][Trash] Unsupported source type={source.Type} item={item.DefinitionID} ({item.InstanceID}) qty={quantity}");
                    break;
            }
        }

        private IAttachmentSystem GetAttachmentSystem()
        {
            if (_uiContext?.Bridge?.Attachment != null)
                return _uiContext.Bridge.Attachment;

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

        private IGameplayBridge ResolveGameplayBridge()
        {
            if (_uiContext?.IsReady == true && _uiContext.IsOwner)
                return _uiContext.Bridge;

            var spectate = SpectateManager.Instance;
            var player = spectate != null ? spectate.GetCurrentPlayer() : null;
            return player != null ? player.GamePlaySystemBridge : null;
        }

        private List<RaycastResult> RaycastUi(PointerEventData eventData)
        {
            var results = new List<RaycastResult>();
            if (_raycaster == null && _dragCanvas != null)
                _raycaster = _dragCanvas.GetComponent<GraphicRaycaster>() ?? _dragCanvas.rootCanvas?.GetComponent<GraphicRaycaster>();

            if (_raycaster != null)
                _raycaster.Raycast(eventData, results);
            else if (EventSystem.current != null)
                EventSystem.current.RaycastAll(eventData, results);

            return results;
        }

        private static Vector2 ResolveGhostIconSize(Vector2 sourceSize)
        {
            float minAxis = Mathf.Min(Mathf.Abs(sourceSize.x), Mathf.Abs(sourceSize.y));
            float size = minAxis > 1f ? Mathf.Clamp(minAxis, 48f, 96f) : 72f;
            return new Vector2(size, size);
        }

        private ItemSlotView ResolveSlotFromRaycastResults(List<RaycastResult> results)
        {
            if (results == null) return null;

            foreach (var r in results)
            {
                if (r.gameObject == null) continue;
                if (IsActiveGhostHit(r.gameObject)) continue;
                if (IsTrashAreaHit(r.gameObject))
                {
                    Log($"{DragLogPrefix} Raycast target blocked by TrashArea hit={r.gameObject.name}");
                    return null;
                }

                var slot = r.gameObject.GetComponentInParent<ItemSlotView>();
                if (slot != null &&
                    slot.gameObject.activeInHierarchy &&
                    _allSlots.TryGetValue(slot.SlotId, out var registered) &&
                    registered == slot)
                {
                    return slot;
                }
            }

            return null;
        }

        private ItemSlotView ResolveSlotFromPointer(PointerEventData eventData, List<RaycastResult> results)
        {
            ItemSlotView raycastSlot = ResolveSlotFromRaycastResults(results);
            if (raycastSlot != null || HasTrashAreaHit(results))
                return raycastSlot;

            return ResolveRegisteredSlotAtPointer(eventData);
        }

        private ItemSlotView ResolveRegisteredSlotAtPointer(PointerEventData eventData)
        {
            if (eventData == null || _allSlots.Count == 0)
                return null;

            Camera cam = eventData.pressEventCamera ?? eventData.enterEventCamera;
            if (cam == null && _dragCanvas != null && _dragCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = _dragCanvas.worldCamera ?? _dragCanvas.rootCanvas?.worldCamera;

            ItemSlotView best = null;
            float bestArea = float.PositiveInfinity;

            foreach (var kvp in _allSlots)
            {
                var slot = kvp.Value;
                if (slot == null || !slot.gameObject.activeInHierarchy)
                    continue;

                RectTransform rect = slot.RectTransform;
                if (rect == null ||
                    !RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, cam))
                    continue;

                Vector2 size = rect.rect.size;
                float area = Mathf.Abs(size.x * size.y);
                if (area < bestArea)
                {
                    best = slot;
                    bestArea = area;
                }
            }

            if (best != null)
                Log($"{DragLogPrefix} Geometry fallback target={best.SlotId} pointer={eventData.position}");

            return best;
        }

        private bool HasTrashAreaHit(List<RaycastResult> results)
        {
            if (results == null)
                return false;

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject != null && IsTrashAreaHit(results[i].gameObject))
                    return true;
            }

            return false;
        }

        private bool IsActiveGhostHit(GameObject go)
        {
            return _activeGhost != null &&
                   go != null &&
                   (go == _activeGhost.gameObject || go.transform.IsChildOf(_activeGhost.transform));
        }

        private static bool IsTrashAreaHit(GameObject go)
        {
            for (Transform t = go != null ? go.transform : null; t != null; t = t.parent)
            {
                if (t.name.ToLowerInvariant().Contains("trash"))
                    return true;
            }

            return false;
        }

        private static string FormatRaycastHits(List<RaycastResult> results, int max)
        {
            if (results == null || results.Count == 0)
                return "none";

            int count = Mathf.Min(max, results.Count);
            string text = string.Empty;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) text += " > ";
                text += results[i].gameObject != null ? results[i].gameObject.name : "null";
            }

            if (results.Count > count)
                text += $" (+{results.Count - count})";

            return text;
        }

        private void EnsureSlotInput(ItemSlotView view)
        {
            if (view == null || view.GetComponent<ItemSlotInput>() != null)
                return;

            view.gameObject.AddComponent<ItemSlotInput>();
            Log($"{DragLogPrefix} Added missing ItemSlotInput id={view.SlotId} view={view.name}");
        }

        private static void ShowInvalidDropToast(ItemInstance item, UISlotId target)
        {
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            string itemName = !string.IsNullOrEmpty(def?.DisplayName) ? def.DisplayName : item?.DefinitionID ?? "Item";
            string targetName = target.Type switch
            {
                UISlotType.Equipment => target.EquipmentSlot.HasValue ? $"{target.EquipmentSlot.Value} slot" : "equipment slot",
                UISlotType.Weapon => target.WeaponSlot.HasValue ? $"{target.WeaponSlot.Value} weapon slot" : "weapon slot",
                UISlotType.Attachment => "attachment slot",
                UISlotType.Inventory => "inventory slot",
                _ => "target slot"
            };

            var toast = PersistentUICanvas.Instance != null
                ? PersistentUICanvas.Instance.ToastService
                : ToastService.Instance;
            toast?.Show("Invalid Drop", $"{itemName} cannot be placed in this {targetName}.");
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
                Background = state.Background,
                StackCount = state.StackCount
            };
        }

        private void Log(string msg)
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log("[DragDropController] " + msg);
        }

        private void LogDebug(string msg)
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log("[DragDropController] " + msg);
        }
    }
}
