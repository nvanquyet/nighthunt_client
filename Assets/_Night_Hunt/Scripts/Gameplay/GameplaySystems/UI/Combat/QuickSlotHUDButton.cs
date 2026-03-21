using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// HUD button for a single quick slot (consumable / throwable).
    ///
    /// Displays:
    ///   • Item icon from ItemDefinition.Icon.
    ///   • Stack-count badge (TextMeshProUGUI) – hidden when count ≤ 1.
    ///   • Greyed-out state when slot is empty.
    ///   • Cooldown ring while the item is on cooldown (driven externally or
    ///     by a configurable per-use delay).
    ///
    /// Usage:
    ///   Call Bind(slotIndex, quickSlotSystem) once the local player is ready.
    ///   Call Unbind() on destroy.
    /// </summary>
    public class QuickSlotHUDButton : ActionButton, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Quick Slot UI")]
        [SerializeField] private TextMeshProUGUI _stackCountText;
        [SerializeField] private Image           _selectedHighlight;  // optional ring/glow

        [Header("Slot Config")]
        [SerializeField] private int             _slotIndex;

        [Header("Quickslot Aim (optional)")]
        [Tooltip("Assign the scene QuickSlotAimController. When present, throwable items " +
                 "enter aim mode instead of being used immediately.")]
        [SerializeField] private QuickSlotAimController _aimController;

        [Header("MOBA Visual Feedback")]
        [Tooltip("2D ring pulse around this button. Auto-found on the same GO.")]
        [SerializeField] private ButtonPulseRing _pulseRing;

        [Tooltip("World-space range indicator (show while aiming). " +
                 "Assign via BindRangeIndicator() after player spawns.")]
        [SerializeField] private RangeIndicator _rangeIndicator;

        [Header("Mobile Virtual Joystick")]
        [Tooltip("VariableJoystick on a child GO — must be DISABLED in the scene by default. " +
                 "Set mode = Floating. QuickSlotHUDButton enables it after the hold delay and disables it on release.")]
        [SerializeField] private VariableJoystick _joystick;
        [Tooltip("Hold duration in seconds before the joystick visual appears. Short taps use the item directly.")]
        [SerializeField] private float _holdDelay = 0.25f;

        // ── Runtime joystick state ────────────────────────────────────────────
        private Coroutine        _holdTimer;
        private bool             _joystickStarted;
        private PointerEventData _pressEventData;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IQuickSlotSystem    _quickSlotSystem;
        private CombatInputHandler  _combatInputHandler;  // notified on press to block concurrent LMB fire
        private bool                _isBound;

        // ─────────────────────────────────────────────────────────────────────
        //  Binding
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Assign or replace the aim controller (called by CombatHUDPanel after spawning).</summary>
        public void SetAimController(QuickSlotAimController controller)
        {
            _aimController = controller;
        }

        /// <summary>
        /// Bind the CombatInputHandler so OnPointerDown can notify it to block concurrent fire events.
        /// Call once after the local player spawns.
        /// </summary>
        public void BindCombatHandler(CombatInputHandler handler)
        {
            _combatInputHandler = handler;
        }

        public void Bind(int slotIndex, IQuickSlotSystem quickSlotSystem)
        {
            Debug.Log($"[QuickSlotHUDButton] Bind: slotIndex={slotIndex}, quickSlotSystem={(quickSlotSystem != null ? quickSlotSystem.ToString() : "null")} ({quickSlotSystem?.GetHashCode() ?? 0})");
            if (_isBound) Unbind();

            _slotIndex        = slotIndex;
            _quickSlotSystem  = quickSlotSystem;

            // Auto-find ButtonPulseRing if not assigned in Inspector
            if (_pulseRing == null)
                _pulseRing = GetComponent<ButtonPulseRing>();

            // Always show placeholder even with no system so the button count matches config.
            if (_quickSlotSystem == null)
            {
                _isBound = true;
                RefreshEmpty();
                return;
            }

            _quickSlotSystem.OnQuickSlotAssigned += HandleSlotAssigned;
            _quickSlotSystem.OnQuickSlotRemoved  += HandleSlotRemoved;
            _quickSlotSystem.OnQuickSlotUsed     += HandleSlotUsed;

            _isBound = true;
            RefreshAll();
        }

        public void Unbind()
        {
            if (!_isBound) return;

            if (_quickSlotSystem != null)
            {
                _quickSlotSystem.OnQuickSlotAssigned -= HandleSlotAssigned;
                _quickSlotSystem.OnQuickSlotRemoved  -= HandleSlotRemoved;
                _quickSlotSystem.OnQuickSlotUsed     -= HandleSlotUsed;
            }

            _quickSlotSystem = null;
            _isBound         = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity
        // ─────────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            // Joystick must start hidden so it does not capture pointer events instead of this button.
            if (_joystick != null) _joystick.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            // Block any concurrent LMB fire event from CombatInputHandler.
            _combatInputHandler?.NotifyUIConsumedPress();

            // MOBA feedback — pulse ring around button.
            _pulseRing?.Play();

            _pressEventData  = eventData;
            _joystickStarted = false;

            if (_aimController != null)
            {
                // Delegate to aim controller — it decides whether to enter aim mode or direct-use.
                _aimController.TryBeginAim(_slotIndex);
                SetSelected(true);   // visually highlight this slot while aiming

                // FIX BUG 3: Chỉ start hold timer (show joystick) nếu item là throwable
                // và aim mode thực sự được enter. Non-throwable (consumable) dùng direct-use
                // → không cần joystick → không start timer → joystick không xuất hiện.
                if (_aimController.IsInAimMode)
                {
                    if (_holdTimer != null) StopCoroutine(_holdTimer);
                    _holdTimer = StartCoroutine(HoldTimerCo());
                }
            }
            else if (_quickSlotSystem != null && _quickSlotSystem.CanUseQuickSlot(_slotIndex))
            {
                // Fallback: direct use (no aim controller present).
                _quickSlotSystem.UseQuickSlot(_slotIndex);
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            StopHoldTimer();

            // ── BUG FIX: cache magnitude TRƯỚC khi joystick.OnPointerUp() reset Direction về zero.
            // Unity EventSystem dispatch order: IPointerUpHandler fires TRƯỚC IEndDragHandler,
            // nên nếu để OnEndDrag đọc magnitude thì luôn đọc được 0 → always CancelAim.
            // Resolve aim ngay tại đây với giá trị magnitude còn hợp lệ.
            float magnitude = (_joystickStarted && _joystick != null)
                ? _joystick.Direction.magnitude
                : 0f;

            if (_joystickStarted && _joystick != null)
            {
                _joystick.OnPointerUp(eventData);
                _joystick.gameObject.SetActive(false);
            }
            _joystickStarted = false;
            SetSelected(false);
            _rangeIndicator?.Hide();

            // Resolve aim state: magnitude >= threshold → ConfirmAim (ném), ngược lại → CancelAim.
            // OnMobileDragEnd tự guard bằng _inAimMode + IsMobile check → safe với non-throwable
            // và với PC mode (IsMobile = false → hàm return ngay).
            // Quick tap (joystick chưa start, magnitude = 0) → CancelAim huỷ server BeginThrow.
            _aimController?.OnMobileDragEnd(magnitude);
        }

        // IBeginDragHandler — starts joystick immediately on first drag move.
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_joystickStarted) StartJoystick(eventData);
        }

        // IDragHandler — feeds eventData to VariableJoystick (normalises to [−1,1]),
        // then passes the normalised Direction to QuickSlotAimController.
        public void OnDrag(PointerEventData eventData)
        {
            if (!_joystickStarted) StartJoystick(eventData);
            if (_joystick == null) return;

            _joystick.OnDrag(eventData);
            _aimController?.OnMobileDrag(_joystick.Direction);   // normalised [−1,1] — correct for Joystick01ToWorldDir
        }

        // IEndDragHandler — aim resolution đã được xử lý trong OnPointerUp (fires TRƯỚC EndDrag
        // theo thứ tự dispatch của Unity EventSystem). OnEndDrag chỉ làm cleanup phòng thủ
        // phòng trường hợp drag kết thúc mà không có PointerUp (edge case: pointer ra ngoài màn hình).
        public void OnEndDrag(PointerEventData eventData)
        {
            // Đọc magnitude trước khi reset (phòng thủ, thường đã được xử lý trong OnPointerUp).
            float magnitude = (_joystickStarted && _joystick != null)
                ? _joystick.Direction.magnitude
                : 0f;

            if (_joystickStarted && _joystick != null)
            {
                _joystick.OnPointerUp(eventData);
                _joystick.gameObject.SetActive(false);
            }
            _joystickStarted = false;

            // Chỉ gọi OnMobileDragEnd nếu OnPointerUp chưa được gọi (aim còn active).
            // Tránh double-resolve vì OnPointerUp đã gọi trước với magnitude đúng.
            _aimController?.OnMobileDragEndIfStillActive(magnitude);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private — Joystick helpers (mirrors FireButton pattern)
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator HoldTimerCo()
        {
            yield return new WaitForSecondsRealtime(_holdDelay);
            if (!_joystickStarted) StartJoystick(_pressEventData);
            _holdTimer = null;
        }

        private void StartJoystick(PointerEventData eventData)
        {
            _joystickStarted = true;
            if (_joystick != null)
            {
                _joystick.gameObject.SetActive(true);   // visible + captures pointer
                _joystick.OnPointerDown(eventData);     // Floating mode: positions background at touch
            }
        }

        private void StopHoldTimer()
        {
            if (_holdTimer != null) { StopCoroutine(_holdTimer); _holdTimer = null; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleSlotAssigned(int index, ItemInstance item)
        {
            if (index != _slotIndex) return;
            RefreshItem(item);
        }

        private void HandleSlotRemoved(int index)
        {
            if (index != _slotIndex) return;
            RefreshEmpty();
        }

        private void HandleSlotUsed(int index, ItemInstance item)
        {
            if (index != _slotIndex) return;
            // Refresh after use (quantity may have decreased or slot emptied)
            var current = _quickSlotSystem?.GetQuickSlotItem(_slotIndex);
            if (current != null)
                RefreshItem(current);
            else
                RefreshEmpty();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Display Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_quickSlotSystem == null) { RefreshEmpty(); return; }

            var item = _quickSlotSystem.GetQuickSlotItem(_slotIndex);
            if (item != null)
                RefreshItem(item);
            else
                RefreshEmpty();
        }

        private void RefreshItem(ItemInstance item)
        {
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            SetIcon(def?.Icon);
            SetInteractable(true);

            RefreshStackBadge(item.Quantity);
        }

        private void RefreshEmpty()
        {
            SetIcon(null);
            SetInteractable(false);
            RefreshStackBadge(0);
        }

        private void RefreshStackBadge(int count)
        {
            if (_stackCountText == null) return;

            if (count > 1)
            {
                _stackCountText.gameObject.SetActive(true);
                _stackCountText.text = count.ToString();
            }
            else
            {
                _stackCountText.gameObject.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Show or hide the selected highlight (e.g. active throwable slot).</summary>
        public void SetSelected(bool selected)
        {
            if (_selectedHighlight != null)
                _selectedHighlight.enabled = selected;
        }

        /// <summary>
        /// Bind the world-space range indicator (e.g. throw-arc radius).
        /// Call after the local player spawns, once the indicator GO is ready.
        /// Pass <c>null</c> to detach.
        /// </summary>
        public void BindRangeIndicator(RangeIndicator indicator, float? rangeOverride = null)
        {
            _rangeIndicator = indicator;
            if (rangeOverride.HasValue)
                _rangeIndicator?.SetRange(rangeOverride.Value);
        }
    }
}
