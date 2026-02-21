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
                // Không có ghost: chỉ cần restore visual source.
                _sourceView?.SetState(_sourceStateSnapshot);
                ClearState();
                return;
            }

            // Nếu không có target nào báo về, coi như cancel.
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

            // Optimistic local apply
            ApplyLocalAction(action, sourceState, targetState);

            // Animate ghost tới target rồi destroy.
            StartCoroutine(AnimateSuccessAndDestroy(targetView.transform.position));

            // Gọi backend logic
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
                case DropActionType.Unequip:
                case DropActionType.AssignQuickSlot:
                case DropActionType.RemoveQuickSlot:
                case DropActionType.DropToWorld:
                case DropActionType.Attach:
                case DropActionType.Detach:
                    // Để backend events quyết định state cuối,
                    // ở đây chỉ clear source để cảm giác item đã rời khỏi ô.
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
                        bridge.SwapItems(item.InstanceID, targetState.Item.InstanceID);
                    }
                    else if (action.Source.Type == UISlotType.Equipment &&
                             action.Target.Type == UISlotType.Equipment &&
                             action.Source.EquipmentSlot.HasValue &&
                             action.Target.EquipmentSlot.HasValue)
                    {
                        bridge.Equipment.SwapEquipment(
                            action.Source.EquipmentSlot.Value,
                            action.Target.EquipmentSlot.Value);
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
                    bridge.EquipItem(item.InstanceID);
                    break;

                case DropActionType.Unequip:
                    if (action.Source.EquipmentSlot.HasValue)
                        bridge.UnequipItem(action.Source.EquipmentSlot.Value);
                    break;

                case DropActionType.AssignQuickSlot:
                    if (action.Target.Type == UISlotType.QuickSlot)
                        bridge.AssignToQuickSlot(item.InstanceID, action.Target.Index);
                    break;

                case DropActionType.RemoveQuickSlot:
                    if (action.Source.Type == UISlotType.QuickSlot)
                        bridge.RemoveFromQuickSlot(action.Source.Index);
                    break;

                case DropActionType.DropToWorld:
                    bridge.DropItem(item.InstanceID, item.Quantity);
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

