using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// HUD button representing a single weapon slot (Primary, Secondary, or Melee).
    ///
    /// Displays:
    ///   ΓÇó Weapon name (DisplayName from ItemDefinition).
    ///   ΓÇó Weapon icon (from ItemDefinition.Icon).
    ///   ΓÇó Magazine ammo count (Field 1 ΓÇö current rounds in mag).
    ///   ΓÇó Reserve ammo count (Field 2 ΓÇö total remaining reserve).
    ///   ΓÇó Magazine fill slider (currentMag / magCapacity).
    ///   ΓÇó Selected-border highlight when this slot is the active weapon.
    ///
    /// Click / Double-click behaviour:
    ///   ΓÇó Single click, slot IS active  ΓåÆ RequestReload().
    ///   ΓÇó Single click, slot NOT active ΓåÆ SelectWeapon (switch).
    ///   ΓÇó Double click (ΓëñDoubleClickThreshold) ΓåÆ HolsterWeapon (put away).
    ///
    /// Usage:
    ///   Call Bind(slotType, weaponSystem) once after the local player's
    ///   WeaponSystem subscribes to events; call Unbind() on destroy.
    /// </summary>
    /// <summary>
    /// <para>Inherits double-click detection, <see cref="CombatInputHandler"/> fire-blocking,
    /// and DOTween press animation from <see cref="SlotHUDButton"/>.</para>
    /// </summary>
    public class WeaponSlotButton : SlotHUDButton
    {
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Inspector
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        [Header("Weapon Slot UI")]
        [SerializeField] private Image _selectedBorder;
        [SerializeField] private TextMeshProUGUI _weaponNameText;     // weapon display name
        [SerializeField] private TextMeshProUGUI _magAmmoText;        // Field 1: rounds in mag
        [SerializeField] private TextMeshProUGUI _reserveAmmoText;    // Field 2: reserve ammo remaining
        [SerializeField] private Slider _ammoSlider;         // fill = currentMag / magCapacity

        [Header("Slot Config")]
        [SerializeField] private WeaponSlotType _slotType;

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Runtime
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private IWeaponSystem _weaponSystem;
        private bool _isBound;

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Binding
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        /// <summary>
        /// Bind this button to a live IWeaponSystem.
        /// Can be called more than once; old bindings are released first.
        /// </summary>
        public void Bind(WeaponSlotType slotType, IWeaponSystem weaponSystem)
        {
            if (_isBound) Unbind();

            _slotType = slotType;
            _weaponSystem = weaponSystem;

            // Always show placeholder even with no system so the button count matches config.
            if (_weaponSystem == null)
            {
                _isBound = true;
                RefreshEmpty();
                return;
            }

            _weaponSystem.OnWeaponEquipped += HandleWeaponEquipped;
            _weaponSystem.OnWeaponUnequipped += HandleWeaponUnequipped;
            _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            _weaponSystem.OnAmmoChanged += HandleAmmoChanged;
            _weaponSystem.OnReloadStateChanged += HandleReloadStateChanged;

            _isBound = true;
            RefreshAll();
        }

        public void Unbind()
        {
            if (!_isBound) return;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnWeaponEquipped -= HandleWeaponEquipped;
                _weaponSystem.OnWeaponUnequipped -= HandleWeaponUnequipped;
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
                _weaponSystem.OnAmmoChanged -= HandleAmmoChanged;
                _weaponSystem.OnReloadStateChanged -= HandleReloadStateChanged;
            }

            _weaponSystem = null;
            _isBound = false;
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Unity Lifecycle
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        protected override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        public override void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // base calls DOTween animation AND NotifyUIConsumedPress (via SlotHUDButton).
            base.OnPointerDown(eventData);

            if (_weaponSystem == null) return;

            bool isDoubleClick = ConsumeDoubleClick();

            if (isDoubleClick)
            {
                // Double-click ΓåÆ holster (put weapon away regardless of which slot)
                _weaponSystem.HolsterWeapon();
                return;
            }

            // Single click
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (activeSlot.HasValue && activeSlot.Value == _slotType)
            {
                // Already holding this weapon ΓåÆ reload
                _weaponSystem.RequestReload();
            }
            else
            {
                // Switch to this weapon slot
                _weaponSystem.SelectWeapon(_slotType);
            }
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Event Handlers
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private void HandleWeaponEquipped(WeaponSlotType slot, ItemInstance weapon)
        {
            if (slot != _slotType) return;
            RefreshAll();
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
        }

        private void HandleAmmoChanged(int currentMag, int totalLeft, int capacity)
        {
            if (_weaponSystem == null) return;
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (!activeSlot.HasValue || activeSlot.Value != _slotType) return;

            RefreshAmmoDisplay(currentMag, totalLeft, capacity);
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            // No cooldown ring ΓÇö intentionally left empty.
            // Reload state is reflected through ammo count updates only.
        }

        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
        //  Display Helpers
        // ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

        private void RefreshAll()
        {
            if (_weaponSystem == null) { RefreshEmpty(); return; }

            var weapon = _weaponSystem.GetWeapon(_slotType);
            if (weapon == null) { RefreshEmpty(); return; }

            RefreshIcon(weapon);

            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            bool isSelected = activeSlot.HasValue && activeSlot.Value == _slotType;
            SetSelectedBorder(isSelected);

            int mag = _weaponSystem.GetCurrentMagazine(_slotType);
            int total = Mathf.RoundToInt(_weaponSystem.GetTotalAmmo(_slotType));
            int cap = GetMagazineCapacity(weapon);
            RefreshAmmoDisplay(mag, total, cap);
        }

        /// <summary>
        /// Slot is empty: clear icon sprite and all text fields.
        /// No overlay ΓÇö simply nothing is shown.
        /// </summary>
        private void RefreshEmpty()
        {
            SetIcon(null);
            SetSelectedBorder(false);

            if (_weaponNameText != null) _weaponNameText.text = string.Empty;
            if (_magAmmoText != null) _magAmmoText.text = string.Empty;
            if (_reserveAmmoText != null) _reserveAmmoText.text = string.Empty;
            if (_ammoSlider != null) _ammoSlider.value = 0f;
        }

        private void RefreshIcon(ItemInstance weapon)
        {
            if (weapon == null) { RefreshEmpty(); return; }

            var def = ItemDatabase.GetDefinition(weapon.DefinitionID);
            SetIcon(def?.Icon);

            if (_weaponNameText != null)
                _weaponNameText.text = def?.DisplayName ?? string.Empty;
        }

        /// <summary>
        /// Update the two ammo display elements.
        ///   _magAmmoText    ΓåÆ "<b><color=yellow><size=150%>currentMag</size></color></b> / magCapacity"
        ///   _reserveAmmoText ΓåÆ reserveAmmo (total rounds left outside mag)
        /// </summary>
        /// <param name="currentMag">Rounds currently loaded in the magazine.</param>
        /// <param name="reserveAmmo">Reserve ammo remaining (NOT including what is in mag).</param>
        /// <param name="magCapacity">Maximum magazine capacity.</param>
        private void RefreshAmmoDisplay(int currentMag, int reserveAmmo, int magCapacity)
        {
            // Melee weapons have no magazine ΓÇö hide ammo elements
            if (magCapacity <= 0)
            {
                if (_magAmmoText != null) _magAmmoText.text = string.Empty;
                if (_reserveAmmoText != null) _reserveAmmoText.text = string.Empty;
                if (_ammoSlider != null) _ammoSlider.value = 0f;
                return;
            }

            // Field 1: "currentMag / magCapacity"
            // currentMag ΓÇö yellow, 150% size; "/" and magCapacity ΓÇö default styleP
            if (_magAmmoText != null)
                _magAmmoText.text = $"<color=#FFD700><size=150%>{currentMag}</size></color> / {magCapacity}";

            // Field 2: reserve ammo remaining
            if (_reserveAmmoText != null)
                _reserveAmmoText.text = reserveAmmo.ToString();

            if (_ammoSlider != null)
                _ammoSlider.value = magCapacity > 0 ? (float)currentMag / magCapacity : 0f;
        }

        private void SetSelectedBorder(bool selected)
        {
            if (_selectedBorder != null)
                _selectedBorder.enabled = selected;
        }

        private static int GetMagazineCapacity(ItemInstance inst)
        {
            if (inst == null) return 0;
            return Mathf.RoundToInt(inst.GetComputedStat(ItemStatType.MagazineSize, 0f));
        }
    }
}
