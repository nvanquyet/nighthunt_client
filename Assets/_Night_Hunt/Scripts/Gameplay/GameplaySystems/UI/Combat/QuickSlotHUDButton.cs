using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

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
    public class QuickSlotHUDButton : ActionButton, IDragHandler, IEndDragHandler
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

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IQuickSlotSystem _quickSlotSystem;
        private bool             _isBound;

        // ─────────────────────────────────────────────────────────────────────
        //  Binding
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Assign or replace the aim controller (called by CombatHUDPanel after spawning).</summary>
        public void SetAimController(QuickSlotAimController controller)
        {
            _aimController = controller;
        }

        public void Bind(int slotIndex, IQuickSlotSystem quickSlotSystem)
        {
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

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            // MOBA feedback — pulse ring around button + world-space range indicator
            _pulseRing?     .Play();
            _rangeIndicator?.Show();

            if (_aimController != null)
            {
                // Delegate to aim controller — it will decide whether to aim or direct-use.
                _aimController.TryBeginAim(_slotIndex, transform as RectTransform, eventData.position);
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
            // Hide the range ring that was shown on PointerDown.
            // (Throwable aim mode hides via ResetAimState; non-throwable needs this.)
            _rangeIndicator?.Hide();
        }

        // IDragHandler — forwarded to aim controller for mobile joystick movement.
        public void OnDrag(PointerEventData eventData)
        {
            _aimController?.OnMobileDrag(eventData);
        }

        // IEndDragHandler — forwarded to aim controller to confirm / cancel on lift.
        public void OnEndDrag(PointerEventData eventData)
        {
            _aimController?.OnMobileDragEnd(eventData);
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
