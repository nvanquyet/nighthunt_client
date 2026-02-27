using UnityEngine;
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
    public class QuickSlotHUDButton : ActionButton
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Quick Slot UI")]
        [SerializeField] private TextMeshProUGUI _stackCountText;
        [SerializeField] private Image           _selectedHighlight;  // optional ring/glow

        [Header("Slot Config")]
        [SerializeField] private int             _slotIndex;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IQuickSlotSystem _quickSlotSystem;
        private bool             _isBound;

        // ─────────────────────────────────────────────────────────────────────
        //  Binding
        // ─────────────────────────────────────────────────────────────────────

        public void Bind(int slotIndex, IQuickSlotSystem quickSlotSystem)
        {
            if (_isBound) Unbind();

            _slotIndex        = slotIndex;
            _quickSlotSystem  = quickSlotSystem;

            if (_quickSlotSystem == null) return;

            _quickSlotSystem.OnQuickSlotAssigned += HandleSlotAssigned;
            _quickSlotSystem.OnQuickSlotRemoved  += HandleSlotRemoved;
            _quickSlotSystem.OnQuickSlotUsed     += HandleSlotUsed;

            _isBound = true;
            RefreshAll();
        }

        public void Unbind()
        {
            if (_quickSlotSystem == null || !_isBound) return;

            _quickSlotSystem.OnQuickSlotAssigned -= HandleSlotAssigned;
            _quickSlotSystem.OnQuickSlotRemoved  -= HandleSlotRemoved;
            _quickSlotSystem.OnQuickSlotUsed     -= HandleSlotUsed;

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

        public override void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (_quickSlotSystem != null && _quickSlotSystem.CanUseQuickSlot(_slotIndex))
                _quickSlotSystem.UseQuickSlot(_slotIndex);
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
    }
}
