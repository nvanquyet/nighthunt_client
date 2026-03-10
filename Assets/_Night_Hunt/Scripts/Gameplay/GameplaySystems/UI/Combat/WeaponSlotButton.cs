using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Types;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// HUD button representing a single weapon slot (Primary, Secondary, or Melee).
    ///
    /// Displays:
    ///   • Weapon icon (from ItemDefinition.Icon or ItemDefinition.Thumbnail).
    ///   • Ammo counter  "currentMag / capacity".
    ///   • Selected-border highlight when this slot is the active weapon.
    ///   • Cooldown ring over the icon during reload.
    ///
    /// Usage:
    ///   Call Bind(slotType, weaponSystem) once after the local player's
    ///   WeaponSystem subscribe to events; call Unbind() on destroy.
    /// </summary>
    public class WeaponSlotButton : ActionButton
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Slot UI")]
        [SerializeField] private Image            _selectedBorder;
        [SerializeField] private TextMeshProUGUI  _ammoText;
        [SerializeField] private Image            _emptySlotOverlay;    // shown when slot is empty

        [Header("Slot Config")]
        [SerializeField] private WeaponSlotType   _slotType;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IWeaponSystem      _weaponSystem;
        private CombatInputHandler _combatInputHandler;  // notified on press to block concurrent LMB fire
        private bool               _isBound;

        // ─────────────────────────────────────────────────────────────────────
        //  Binding
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this button to a live IWeaponSystem.
        /// Can be called more than once; old bindings are released first.
        /// </summary>
        public void Bind(WeaponSlotType slotType, IWeaponSystem weaponSystem)
        {
            if (_isBound) Unbind();

            _slotType     = slotType;
            _weaponSystem = weaponSystem;

            // Always show placeholder even with no system so the button count matches config.
            if (_weaponSystem == null)
            {
                _isBound = true;
                RefreshEmpty();
                return;
            }

            _weaponSystem.OnWeaponEquipped      += HandleWeaponEquipped;
            _weaponSystem.OnWeaponUnequipped    += HandleWeaponUnequipped;
            _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            _weaponSystem.OnAmmoChanged         += HandleAmmoChanged;
            _weaponSystem.OnReloadStateChanged  += HandleReloadStateChanged;

            _isBound = true;
            RefreshAll();
        }

        /// <summary>
        /// Bind the CombatInputHandler so OnPointerDown can notify it to block concurrent fire events.
        /// Call once after the local player spawns.
        /// </summary>
        public void BindCombatHandler(CombatInputHandler handler)
        {
            _combatInputHandler = handler;
        }

        public void Unbind()
        {
            if (!_isBound) return;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnWeaponEquipped      -= HandleWeaponEquipped;
                _weaponSystem.OnWeaponUnequipped    -= HandleWeaponUnequipped;
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
                _weaponSystem.OnAmmoChanged         -= HandleAmmoChanged;
                _weaponSystem.OnReloadStateChanged  -= HandleReloadStateChanged;
            }

            _weaponSystem = null;
            _isBound      = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        public override void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            // Notify CombatInputHandler to block the concurrent Input System LMB-performed event.
            _combatInputHandler?.NotifyUIConsumedPress();
            // Request weapon select via the bound system
            _weaponSystem?.SelectWeapon(_slotType);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance weapon)
        {
            if (slot != _slotType) return;
            RefreshIcon(weapon);
            SetInteractable(true);
        }

        private void HandleWeaponUnequipped(WeaponSlotType slot, ItemInstance weapon)
        {
            if (slot != _slotType) return;
            RefreshEmpty();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            bool isSelected = newSlot.HasValue && newSlot.Value == _slotType;
            SetSelectedBorder(isSelected);

            // Cancel reload ring if this slot was deselected during reload
            if (!isSelected) CancelCooldown();
        }

        private void HandleAmmoChanged(int currentMag, int totalLeft, int capacity)
        {
            // Only refresh if this is the currently active slot
            if (_weaponSystem == null) return;
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (!activeSlot.HasValue || activeSlot.Value != _slotType) return;

            RefreshAmmoText(currentMag, totalLeft);
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            if (_weaponSystem == null) return;
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (!activeSlot.HasValue || activeSlot.Value != _slotType) return;

            if (isReloading)
            {
                // Read ReloadSpeed from the weapon's computed stats
                var inst  = _weaponSystem.GetActiveWeapon();
                float dur = GetReloadDuration(inst);
                StartCooldown(dur);
                SetInteractable(false);
            }
            else
            {
                CancelCooldown();
                SetInteractable(true);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Display Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_weaponSystem == null) { RefreshEmpty(); return; }

            var weapon = _weaponSystem.GetWeapon(_slotType);
            if (weapon == null) { RefreshEmpty(); return; }

            RefreshIcon(weapon);
            SetInteractable(true);

            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            bool isSelected = activeSlot.HasValue && activeSlot.Value == _slotType;
            SetSelectedBorder(isSelected);

            int mag      = _weaponSystem.GetCurrentMagazine(_slotType);
            int total    = Mathf.RoundToInt(_weaponSystem.GetTotalAmmo(_slotType));
            RefreshAmmoText(mag, total);
        }

        private void RefreshEmpty()
        {
            SetIcon(null);
            SetInteractable(false);
            SetSelectedBorder(false);

            if (_ammoText       != null) _ammoText.text       = string.Empty;
            if (_emptySlotOverlay != null) _emptySlotOverlay.enabled = true;
        }

        private void RefreshIcon(ItemInstance weapon)
        {
            if (weapon == null) return;

            var def = ItemDatabase.GetDefinition(weapon.DefinitionID);
            SetIcon(def?.Icon);

            if (_emptySlotOverlay != null)
                _emptySlotOverlay.enabled = false;
        }

        /// <param name="currentMag">Bullets currently in magazine.</param>
        /// <param name="totalLeft">Total remaining ammo (reserve + current mag).</param>
        private void RefreshAmmoText(int currentMag, int totalLeft)
        {
            if (_ammoText == null) return;

            // Melee weapons — no magazine capacity on this slot, hide text
            var weapon = _weaponSystem?.GetWeapon(_slotType);
            int cap    = weapon != null ? GetMagazineCapacity(weapon) : 0;
            if (cap <= 0)
            {
                _ammoText.text = string.Empty;
                return;
            }

            _ammoText.text = $"{currentMag}/{totalLeft}";
        }

        private void SetSelectedBorder(bool selected)
        {
            if (_selectedBorder != null)
                _selectedBorder.enabled = selected;
        }

        private static float GetReloadDuration(ItemInstance inst)
        {
            if (inst == null) return 2f;
            return inst.GetComputedStat(ItemStatType.ReloadSpeed, 2f);
        }

        private static int GetMagazineCapacity(ItemInstance inst)
        {
            if (inst == null) return 0;
            return Mathf.RoundToInt(inst.GetComputedStat(ItemStatType.MagazineSize, 0f));
        }
    }
}
