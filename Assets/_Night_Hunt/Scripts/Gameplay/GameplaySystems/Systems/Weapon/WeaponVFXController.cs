using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Handles weapon visual effects for the local player's character.
    ///
    /// Attach to the same GameObject as WeaponSystem and WeaponModelController.
    ///
    /// Architecture:
    ///   • Subscribes to WeaponModelController.OnWeaponModelChanged.
    ///     When a new PrWeapon model is spawned, _muzzlePoint is updated from
    ///     PrWeapon.ShootFXPos — the SciFi-convention muzzle origin on the prefab.
    ///   • PrWeapon self-manages its muzzle flash (ShootFXFLash / Muzzle child);
    ///     this controller handles any extra centralised effects.
    ///
    /// Inspector setup: only _weaponSystemSource needs to be assigned.
    /// _muzzlePoint is updated automatically on every weapon swap.
    /// </summary>
    public class WeaponVFXController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (e.g. the WeaponSystem component).")]
        [SerializeField] private MonoBehaviour _weaponSystemSource;

        [Tooltip("Parent transform used to keep instantiated VFX organised. If null, spawns at scene root.")]
        [SerializeField] private Transform _vfxPoolParent;

        // ── Runtime ──────────────────────────────────────────────────────────
        private IWeaponSystem         _weaponSystem;
        private WeaponModelController _modelController;
        private Transform             _muzzlePoint; // updated via PrWeapon.ShootFXPos on each swap

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemSource as IWeaponSystem;
            if (_weaponSystem == null)
                Debug.LogWarning("[WeaponVFXController] _weaponSystemSource does not implement IWeaponSystem.");

            _modelController = GetComponent<WeaponModelController>();
            if (_modelController == null)
                Debug.LogWarning("[WeaponVFXController] WeaponModelController not found on same GameObject — muzzle point won't auto-update.");
        }

        private void OnEnable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired     += HandleShotFired;
                _weaponSystem.OnHitscanResult += HandleHitscanResult;
            }
            if (_modelController != null)
                _modelController.OnWeaponModelChanged += HandleWeaponModelChanged;
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired     -= HandleShotFired;
                _weaponSystem.OnHitscanResult -= HandleHitscanResult;
            }
            if (_modelController != null)
                _modelController.OnWeaponModelChanged -= HandleWeaponModelChanged;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        /// <summary>
        /// Auto-updates _muzzlePoint from the spawned model's PrWeapon.ShootFXPos.
        /// Follows the same SciFi convention as PrWeapon — no child-name string search.
        /// </summary>
        private void HandleWeaponModelChanged(PrWeapon prWeapon)
        {
            _muzzlePoint = prWeapon != null ? prWeapon.ShootFXPos : null;
        }

        private void HandleShotFired(WeaponSlotType slot, Vector3 aimDirection) { }

        private void HandleHitscanResult(WeaponSlotType slot, Vector3 origin, Vector3 endpoint) { }

        // ── Pooling helpers ───────────────────────────────────────────────────
        // Reserved for future centralised VFX (muzzle flash, bullet trails, hit decals).
        // Pool implementation goes here when effects are added.
    }
}
