using System;
using UnityEngine;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Root panel that wires all Combat HUD sub-components to live game systems.
    ///
    /// Responsibilities:
    ///   • Receives IWeaponSystem + IQuickSlotSystem via Initialize().
    ///   • Distributes them to the appropriate WeaponSlotButton / QuickSlotHUDButton children.
    ///   • Owns cross-cutting HUD elements: ammo label, reload indicator, depleted warning.
    ///   • NEVER polls — everything is event-driven.
    ///
    /// Inspector setup:
    ///   • Assign the weapon slot buttons (primary / secondary / melee index).
    ///   • Assign the quick slot buttons array in order (slot 0 → index 0).
    ///   • Assign the shared ammo text / reload text references.
    ///   • Call Initialize() from whoever owns the local player (e.g. a Player bootstrapper).
    /// </summary>
    public class CombatHUDPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Weapon Slots
        // ─────────────────────────────────────────────────────────────────────

        [Header("Weapon Slot Buttons")]
        [Tooltip("Button for WeaponSlotType.Primary.")]
        [SerializeField] private WeaponSlotButton _primaryButton;

        [Tooltip("Button for WeaponSlotType.Secondary.")]
        [SerializeField] private WeaponSlotButton _secondaryButton;

        [Tooltip("Button for WeaponSlotType.Melee.")]
        [SerializeField] private WeaponSlotButton _meleeButton;

        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Quick Slots
        // ─────────────────────────────────────────────────────────────────────

        [Header("Quick Slot Buttons")]
        [Tooltip("Order matches slot index 0–3.")]
        [SerializeField] private QuickSlotHUDButton[] _quickSlotButtons;

        // ─────────────────────────────────────────────────────────────────────
        //  Inspector — Shared HUD Labels
        // ─────────────────────────────────────────────────────────────────────

        [Header("Ammo Display")]
        [Tooltip("Large ammo counter — 'currentMag / totalLeft'.")]
        [SerializeField] private TextMeshProUGUI _ammoLabel;

        [Tooltip("Reserve ammo small label.")]
        [SerializeField] private TextMeshProUGUI _reserveLabel;

        [Header("Status Messages")]
        [Tooltip("Shown while the active weapon is reloading.")]
        [SerializeField] private GameObject _reloadingIndicator;

        [Tooltip("Shown when both magazine and reserve ammo are depleted.")]
        [SerializeField] private GameObject _depletedWarning;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        private IWeaponSystem    _weaponSystem;
        private IQuickSlotSystem _quickSlotSystem;
        private bool             _isInitialized;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call once when the local player's systems are ready.
        /// Can be called again to rebind (e.g. after reconnect / respawn).
        /// </summary>
        public void Initialize(IWeaponSystem weaponSystem, IQuickSlotSystem quickSlotSystem)
        {
            if (_isInitialized) Teardown();

            _weaponSystem    = weaponSystem;
            _quickSlotSystem = quickSlotSystem;

            // ── Weapon slot buttons ──────────────────────────────────────────
            _primaryButton?  .Bind(WeaponSlotType.Primary,   _weaponSystem);
            _secondaryButton?.Bind(WeaponSlotType.Secondary, _weaponSystem);
            _meleeButton?    .Bind(WeaponSlotType.Melee,     _weaponSystem);

            // ── Quick slot buttons ───────────────────────────────────────────
            if (_quickSlotButtons != null)
            {
                for (int i = 0; i < _quickSlotButtons.Length; i++)
                    _quickSlotButtons[i]?.Bind(i, _quickSlotSystem);
            }

            // ── Shared HUD events ────────────────────────────────────────────
            if (_weaponSystem != null)
            {
                _weaponSystem.OnAmmoChanged         += HandleAmmoChanged;
                _weaponSystem.OnReloadStateChanged  += HandleReloadStateChanged;
                _weaponSystem.OnWeaponDepleted      += HandleWeaponDepleted;
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            }

            // Initial visual state
            HideStatusMessages();

            _isInitialized = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            Teardown();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Teardown
        // ─────────────────────────────────────────────────────────────────────

        private void Teardown()
        {
            _primaryButton?  .Unbind();
            _secondaryButton?.Unbind();
            _meleeButton?    .Unbind();

            if (_quickSlotButtons != null)
                foreach (var btn in _quickSlotButtons)
                    btn?.Unbind();

            if (_weaponSystem != null)
            {
                _weaponSystem.OnAmmoChanged         -= HandleAmmoChanged;
                _weaponSystem.OnReloadStateChanged  -= HandleReloadStateChanged;
                _weaponSystem.OnWeaponDepleted      -= HandleWeaponDepleted;
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

                _weaponSystem    = null;
                _quickSlotSystem = null;
            }

            _isInitialized = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers — Weapon System
        // ─────────────────────────────────────────────────────────────────────

        private void HandleAmmoChanged(int currentMag, int totalLeft, int capacity)
        {
            if (_ammoLabel != null)
                _ammoLabel.text = $"{currentMag} / {capacity}";

            if (_reserveLabel != null)
                _reserveLabel.text = totalLeft.ToString();

            // Hide depleted warning once ammo is restored (e.g. after pickup)
            if (currentMag > 0 || totalLeft > 0)
                SetDepletedWarning(false);
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            if (_reloadingIndicator != null)
                _reloadingIndicator.SetActive(isReloading);
        }

        private void HandleWeaponDepleted(WeaponSlotType slot)
        {
            // Only show warning for currently active slot
            if (_weaponSystem == null) return;
            var activeSlot = _weaponSystem.GetActiveWeaponSlot();
            if (activeSlot.HasValue && activeSlot.Value == slot)
                SetDepletedWarning(true);
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            // Hide status messages when switching weapons
            HideStatusMessages();

            if (!newSlot.HasValue)
            {
                // Holstered — clear ammo display
                if (_ammoLabel    != null) _ammoLabel.text    = string.Empty;
                if (_reserveLabel != null) _reserveLabel.text = string.Empty;
                return;
            }

            // Refresh ammo for the newly selected slot
            if (_weaponSystem == null) return;
            int mag      = _weaponSystem.GetCurrentMagazine(newSlot.Value);
            float total  = _weaponSystem.GetTotalAmmo(newSlot.Value);

            var inst = _weaponSystem.GetActiveWeapon();
            int cap  = inst != null ? Mathf.RoundToInt(inst.GetComputedStat(ItemStatType.MagazineSize, 0f)) : 0;

            HandleAmmoChanged(mag, Mathf.RoundToInt(total), cap);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helper Methods
        // ─────────────────────────────────────────────────────────────────────

        private void HideStatusMessages()
        {
            if (_reloadingIndicator != null) _reloadingIndicator.SetActive(false);
            SetDepletedWarning(false);
        }

        private void SetDepletedWarning(bool show)
        {
            if (_depletedWarning != null)
                _depletedWarning.SetActive(show);
        }
    }
}
