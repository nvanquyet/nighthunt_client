using System.Collections;
using System.Collections.Generic;
using NightHunt.Gameplay.Spectator;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Trung tâm xử lý drag & drop, ghost, optimistic update.
    /// </summary>
    public class DragDropController : MonoBehaviour
    {
        public static DragDropController Instance { get; private set; }

        [Header("Ghost")]
        [SerializeField] private DragDropGhost _ghostPrefab;
        [SerializeField] private Canvas _dragCanvas;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs;

        private DragDropGhost _activeGhost;
        private ItemSlotView _sourceView;
        private UISlotId _sourceId;
        private UISlotState _sourceStateSnapshot;
        private UISlotState _targetStateSnapshot;

        private readonly DragDropValidator _validator = new DragDropValidator();
        private readonly Dictionary<UISlotId, ItemSlotView> _allSlots = new Dictionary<UISlotId, ItemSlotView>();

        [Header("Raycast")]
        [SerializeField] private GraphicRaycaster _raycaster;

        private ItemSlotView _currentHoverView;

        // FIX Bug 5: Guard flag – OnDrop fires before OnEndDrag in Unity EventSystem.
        // When NotifyDropTarget succeeds, we set this to prevent EndDrag from starting
        // AnimateCancelAndRestore which would restore the source slot visual incorrectly.
        private bool _dropHandled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void RegisterSlotView(ItemSlotView view)
        {
            if (view == null) return;
            _allSlots[view.SlotId] = view;
        }

        /// <summary>
        /// Reset toàn bộ state drag-drop (dùng khi đổi current player, reload UI,...).
        /// </summary>
        public void ResetAll()
        {
            if (_activeGhost != null)
            {
                Destroy(_activeGhost.gameObject);
            }

            _activeGhost = null;
            _sourceView = null;
            _sourceId = default;
            _sourceStateSnapshot = null;
            _targetStateSnapshot = null;
        }

        #region Drag Flow

        public void BeginDrag(ItemSlotView sourceView, PointerEventData eventData)
        {
            if (sourceView == null)
                return;

            var spectate = SpectateManager.Instance;
            if (spectate == null || !spectate.IsCurrentPlayerLocal())
            {
                Log("BeginDrag blocked – current player is not local.");
                return;
            }

            if (sourceView.State == null || sourceView.State.Item == null)
                return;

            _sourceView = sourceView;
            _sourceId = sourceView.SlotId;
            _sourceStateSnapshot = CloneState(sourceView.State);

            SpawnGhost(sourceView, eventData);
            sourceView.SetEmptyState(); // Clear về empty state (có thể restore nếu cancel)
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

            // Preview highlight target hợp lệ
            if (_raycaster != null)
            {
                var results = new List<RaycastResult>();
                _raycaster.Raycast(eventData, results);

                ItemSlotView hoverView = null;
                foreach (var r in results)
                {
                    hoverView = r.gameObject.GetComponentInParent<ItemSlotView>();
                    if (hoverView != null)
                        break;
                }

                if (hoverView != _currentHoverView)
                {
                    // Clear highlight cũ
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
                ClearState();
                return;
            }

            // FIX Bug 5: NotifyDropTarget already handled this drop (OnDrop fires BEFORE OnEndDrag).
            // Do not start a second coroutine – AnimateSuccessAndDestroy is already running.
            if (_dropHandled)
                return;

            // No valid drop target was notified – cancel drag and restore source
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

            // Mark drop as handled BEFORE coroutine – EndDrag checks this flag
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
            _activeGhost.SetupFromSlot(sourceView);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_dragCanvas.transform,
                eventData.position,
                _dragCanvas.worldCamera,
                out var localPoint);

            var rt = _activeGhost.RectTransform;
            rt.localPosition = localPoint;
            UITweenUtil.ScaleInstant(rt, 1f);

            // SetupFromSlot đã set alpha = 1f, không cần SetAlpha(0f) nữa
            var cg = _activeGhost.GetComponent<CanvasGroup>();
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
                
                var cg = _activeGhost.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    UITweenUtil.FadeCanvasGroupInstant(cg, 0f);
                }
                
                yield return null;
                
                // Check lại sau yield vì có thể bị destroy từ nơi khác
                if (_activeGhost != null)
                {
                    Destroy(_activeGhost.gameObject);
                }
            }

            // Restore source view state nếu còn tồn tại
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
                var cg = _activeGhost.GetComponent<CanvasGroup>();
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
            _activeGhost = null;
            _sourceView = null;
            _sourceStateSnapshot = null;
            _targetStateSnapshot = null;
            _dropHandled = false;  // FIX Bug 5: always reset for next drag cycle
            _currentHoverView = null;
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
                case DropActionType.EquipWeapon:
                case DropActionType.Unequip:
                case DropActionType.UnequipWeapon:
                case DropActionType.DropToWorld:
                case DropActionType.Attach:
                case DropActionType.Detach:
                case DropActionType.Trash:
                    // Backend events drive the final state.
                    // Only clear source so the item feels like it has left.
                    if (sourceView != null) sourceView.SetEmptyState();
                    break;

                case DropActionType.AssignQuickSlot:
                    // FIX Bug 2b: QuickSlot is a REFERENCE – the item physically stays in inventory.
                    // Do NOT clear the source inventory slot; just preview the item in the target QS.
                    if (targetView != null && sourceState != null)
                    {
                        var previewState = CloneState(sourceState);
                        targetView.SetState(previewState);
                    }
                    // Server confirmation (OnQuickSlotAssigned event) will finalize both slots.
                    break;

                case DropActionType.RemoveQuickSlot:
                    // QuickSlot chỉ là reference – item vẫn nằm trong inventory.
                    // Khi kéo từ QuickSlot → Inventory thì:
                    // - Clear QuickSlot source ngay (optimistic)
                    // - Preview: show item ở ô inventory target (chờ server confirm MoveItem)
                    if (sourceView != null)
                        sourceView.SetEmptyState();

                    if (action.Target.Type == UISlotType.Inventory && targetView != null && sourceState != null)
                    {
                        var movedState = CloneState(sourceState);
                        targetView.SetState(movedState);
                    }
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
                    // Stack items vào target
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
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][SwapItems] " +
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
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][SwapEquipment] " +
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
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][SwapWeapons] " +
                                      $"slot1={action.Source.WeaponSlot.Value} " +
                                      $"slot2={action.Target.WeaponSlot.Value} " +
                                      $"from={action.Source} to={action.Target}");
                        }
                        bridge.Weapon.SwapWeapons(
                            action.Source.WeaponSlot.Value,
                            action.Target.WeaponSlot.Value);
                    }
                    else if (action.Source.Type == UISlotType.QuickSlot &&
                        action.Target.Type == UISlotType.QuickSlot)
                    {
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][SwapQuickSlots] " +
                                      $"slot1={action.Source.Index} " +
                                      $"slot2={action.Target.Index} " +
                                      $"from={action.Source} to={action.Target}");
                        }
                        bridge.QuickSlot.SwapQuickSlots(
                            action.Source.Index,
                            action.Target.Index);
                    }
                    else if (action.Source.Type == UISlotType.Attachment &&
                             action.Target.Type == UISlotType.Attachment &&
                             !string.IsNullOrEmpty(action.Source.ParentInstanceID) &&
                             !string.IsNullOrEmpty(action.Target.ParentInstanceID) &&
                             targetState != null && targetState.Item != null)
                    {
                        // Swap attachments giữa 2 slots
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
                    if (_enableDebugLogs)
                    {
                        Debug.Log($"[DragDropController] [UI→Server][Equip] " +
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
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][EquipWeapon] "
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
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][Unequip] " +
                                      $"slot={action.Source.EquipmentSlot.Value} " +
                                      $"source={action.Source} target={action.Target}");
                        }
                        bridge.UnequipItem(action.Source.EquipmentSlot.Value);
                    }
                    break;

                case DropActionType.UnequipWeapon:
                    if (action.Source.WeaponSlot.HasValue)
                    {
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][UnequipWeapon] " +
                                      $"slot={action.Source.WeaponSlot.Value} " +
                                      $"source={action.Source} target={action.Target}");
                        }
                        bridge.UnequipWeapon(action.Source.WeaponSlot.Value);
                    }
                    break;

                case DropActionType.AssignQuickSlot:
                    if (action.Target.Type == UISlotType.QuickSlot)
                    {
                        bridge.AssignToQuickSlot(item.InstanceID, action.Target.Index);
                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][AssignQuickSlot] " +
                                      $"item={item.DefinitionID} ({item.InstanceID}) " +
                                      $"to QuickSlot[{action.Target.Index}] from={action.Source}");
                        }
                    }
                    break;

                case DropActionType.RemoveQuickSlot:
                    if (action.Source.Type == UISlotType.QuickSlot)
                    {
                        // Nếu target là inventory cell → vừa clear quickslot, vừa move item tới index mới.
                        if (action.Target.Type == UISlotType.Inventory)
                        {
                            if (_enableDebugLogs)
                            {
                                Debug.Log($"[DragDropController] [UI→Server][QuickSlot→Inventory] " +
                                          $"item={item.DefinitionID} ({item.InstanceID}) " +
                                          $"QS[{action.Source.Index}] → Inventory[{action.Target.Index}]");
                            }

                            bridge.Inventory.MoveItem(item.InstanceID, action.Target.Index);
                        }

                        bridge.RemoveFromQuickSlot(action.Source.Index);

                        if (_enableDebugLogs)
                        {
                            Debug.Log($"[DragDropController] [UI→Server][RemoveQuickSlot] " +
                                      $"QuickSlot[{action.Source.Index}] cleared (source={action.Source}, target={action.Target})");
                        }
                    }
                    break;

                case DropActionType.DropToWorld:
                    // BUG 4 FIX: support drop-to-world from any source slot.
                    // For non-inventory sources, unequip/detach first then drop.
                    switch (action.Source.Type)
                    {
                        case UISlotType.Inventory:
                            bridge.DropItem(item.InstanceID, item.Quantity);
                            break;

                        case UISlotType.Equipment:
                            if (action.Source.EquipmentSlot.HasValue)
                            {
                                // Unequip (detaches attachments) then drop from inventory
                                bridge.UnequipItem(action.Source.EquipmentSlot.Value);
                                bridge.DropItem(item.InstanceID, item.Quantity);
                            }
                            break;

                        case UISlotType.Weapon:
                            if (action.Source.WeaponSlot.HasValue)
                            {
                                bridge.UnequipWeapon(action.Source.WeaponSlot.Value);
                                bridge.DropItem(item.InstanceID, item.Quantity);
                            }
                            break;

                        case UISlotType.QuickSlot:
                            // QuickSlot is a reference; item lives in inventory – just drop it
                            bridge.RemoveFromQuickSlot(action.Source.Index);
                            bridge.DropItem(item.InstanceID, item.Quantity);
                            break;

                        case UISlotType.Attachment:
                            // Detach to inventory first, then drop
                            var attachSys = GetAttachmentSystem();
                            if (attachSys != null && !string.IsNullOrEmpty(action.Source.ParentInstanceID))
                                attachSys.DetachItem(action.Source.ParentInstanceID, action.Source.Index);
                            bridge.DropItem(item.InstanceID, item.Quantity);
                            break;

                        default:
                            Log($"[DropToWorld] Unhandled source type={action.Source.Type}");
                            break;
                    }
                    break;

                case DropActionType.Attach:
                    // Attach item vào attachment slot
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
                    // Detach attachment về inventory
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
                    // Trash cần xử lý khác nhau tuỳ nguồn:
                    // - Inventory: xoá trực tiếp khỏi inventory.
                    // - Equipment / Weapon: unequip trước rồi xoá khỏi inventory.
                    // - QuickSlot: xoá khỏi inventory + clear quickslot reference.
                    // - Attachment: detach về inventory (nếu có system) – user có thể trash tiếp từ inventory.
                    switch (action.Source.Type)
                    {
                        case UISlotType.Inventory:
                            if (_enableDebugLogs)
                            {
                                Debug.Log($"[DragDropController] [UI→Server][Trash Inventory] " +
                                          $"item={item.DefinitionID} ({item.InstanceID}) " +
                                          $"from={action.Source} qty={item.Quantity}");
                            }
                            bridge.RemoveItem(item.InstanceID, item.Quantity);
                            break;

                        case UISlotType.Equipment:
                            if (action.Source.EquipmentSlot.HasValue)
                            {
                                var slot = action.Source.EquipmentSlot.Value;
                                if (_enableDebugLogs)
                                {
                                    Debug.Log($"[DragDropController] [UI→Server][Trash Equipment] " +
                                              $"slot={slot}, item={item.DefinitionID} ({item.InstanceID}) " +
                                              $"from={action.Source}");
                                }

                                // 1) Unequip để trả item về inventory.
                                bridge.UnequipItem(slot);
                                // 2) Xoá instance khỏi inventory.
                                bridge.RemoveItem(item.InstanceID, item.Quantity);
                            }
                            break;

                        case UISlotType.Weapon:
                            if (action.Source.WeaponSlot.HasValue)
                            {
                                var slot = action.Source.WeaponSlot.Value;
                                if (_enableDebugLogs)
                                {
                                    Debug.Log($"[DragDropController] [UI→Server][Trash Weapon] " +
                                              $"slot={slot}, item={item.DefinitionID} ({item.InstanceID}) " +
                                              $"from={action.Source}");
                                }

                                // 1) Unequip weapon để trả về inventory.
                                bridge.UnequipWeapon(slot);
                                // 2) Xoá instance khỏi inventory.
                                bridge.RemoveItem(item.InstanceID, item.Quantity);
                            }
                            break;

                        case UISlotType.QuickSlot:
                            // QuickSlot chỉ giữ reference → cần xoá item khỏi inventory + clear quickslot.
                            if (_enableDebugLogs)
                            {
                                Debug.Log($"[DragDropController] [UI→Server][Trash QuickSlot] " +
                                          $"QS[{action.Source.Index}], item={item.DefinitionID} ({item.InstanceID}) " +
                                          $"from={action.Source}");
                            }

                            bridge.RemoveItem(item.InstanceID, item.Quantity);
                            bridge.RemoveFromQuickSlot(action.Source.Index);
                            break;

                        case UISlotType.Attachment:
                            // Đơn giản hoá: detach attachment về inventory,
                            // sau đó player có thể trash tiếp từ inventory nếu muốn.
                            var attachmentSystem = GetAttachmentSystem();
                            if (attachmentSystem != null &&
                                !string.IsNullOrEmpty(action.Source.ParentInstanceID))
                            {
                                if (_enableDebugLogs)
                                {
                                    Debug.Log($"[DragDropController] [UI→Server][Trash Attachment → Detach] " +
                                              $"parent={action.Source.ParentInstanceID}, slotIndex={action.Source.Index}, " +
                                              $"attachmentItem={item.DefinitionID} ({item.InstanceID})");
                                }

                                attachmentSystem.DetachItem(
                                    action.Source.ParentInstanceID,
                                    action.Source.Index);
                            }
                            break;

                        default:
                            if (_enableDebugLogs)
                            {
                                Debug.Log($"[DragDropController] [UI→Server][Trash] Unsupported source type={action.Source.Type} " +
                                          $"item={item.DefinitionID} ({item.InstanceID}) from={action.Source}");
                            }
                            break;
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
            
            return player.GetComponent<IAttachmentSystem>();
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
            if (_enableDebugLogs)
                Debug.Log("[DragDropController] " + msg);
        }
    }
}

