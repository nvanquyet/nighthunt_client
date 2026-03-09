using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Spawns and destroys weapon model GameObjects whenever the active weapon slot changes.
    ///
    /// Mirrors the role that PrCharacterInventory plays in the PR reference:
    /// equip → instantiate EquippedPrefab under the weapon socket bone; unequip → destroy.
    ///
    /// Inspector setup:
    ///   _weaponSystemSource — MonoBehaviour implementing IWeaponSystem on the same player.
    ///   _weaponSocket       — Transform on the character's right-hand bone (weapon parent).
    ///   _vfxController      — optional WeaponVFXController; muzzle point is updated after each swap.
    ///
    /// Convention (each EquippedPrefab should follow):
    ///   Child named "Muzzle"      → muzzle flash / projectile origin point.
    ///   Child named "LeftHandIK"  → two-handed grip IK target for PrCharacterIK.leftHandTarget.
    /// </summary>
    public class WeaponModelController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (e.g. the WeaponSystem component on this player).")]
        [SerializeField] private MonoBehaviour _weaponSystemSource;

        [Tooltip("Transform on the character's right-hand bone where weapon models are parented.")]
        [SerializeField] private Transform _weaponSocket;

        [Tooltip("Optional WeaponVFXController — muzzle point is refreshed after every weapon swap.")]
        [SerializeField] private WeaponVFXController _vfxController;

        [Header("Child-transform name conventions")]
        [Tooltip("Name of the child Transform on each EquippedPrefab used as left-hand IK target.")]
        [SerializeField] private string _leftHandIKName = "LeftHandIK";

        [Tooltip("Name of the child Transform on each EquippedPrefab used as the muzzle point.")]
        [SerializeField] private string _muzzleName = "Muzzle";

        // ── Runtime ──────────────────────────────────────────────────────────
        private IWeaponSystem _weaponSystem;
        private GameObject    _currentModel;

        /// <summary>
        /// Left-hand IK target found on the current weapon model.
        /// Read by PrCharacterIK (or any IK component) and assigned to leftHandTarget.
        /// Null when no weapon is equipped or the model has no IK marker child.
        /// </summary>
        public Transform LeftHandIKTarget { get; private set; }

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemSource as IWeaponSystem;
            if (_weaponSystem == null && _weaponSystemSource != null)
                Debug.LogWarning("[WeaponModelController] _weaponSystemSource does not implement IWeaponSystem.");
        }

        private void OnEnable()
        {
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Inject dependencies at runtime after all systems are initialised (called from NetworkPlayer).
        /// </summary>
        public void Initialize(IWeaponSystem weaponSystem, Transform weaponSocket)
        {
            // Unsubscribe from old system if re-initialising.
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

            _weaponSystem = weaponSystem;
            _weaponSocket = weaponSocket;

            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void HandleActiveWeaponChanged(WeaponSlotType? previousSlot, WeaponSlotType? newSlot)
        {
            DestroyCurrentModel();

            if (newSlot == null || _weaponSystem == null) return;

            var inst = _weaponSystem.GetWeapon(newSlot.Value);
            if (inst == null) return;

            var def = ItemDatabase.GetDefinition(inst.DefinitionID);
            if (def == null || def.EquippedPrefab == null) return;

            Transform parent = _weaponSocket != null ? _weaponSocket : transform;
            _currentModel = Instantiate(def.EquippedPrefab, parent);
            _currentModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Refresh muzzle point on VFX controller.
            Transform muzzle = _currentModel.transform.Find(_muzzleName);
            if (_vfxController != null)
                _vfxController.SetMuzzlePoint(muzzle); // null is fine — VFX falls back to transform

            // Propagate muzzle point to WeaponSystem for hitscan raycast origin.
            _weaponSystem?.SetFireOrigin(muzzle);

            // Expose left-hand IK target so PrCharacterIK can pick it up.
            LeftHandIKTarget = _currentModel.transform.Find(_leftHandIKName);

            Debug.Log($"[WeaponModelController] Spawned model '{def.EquippedPrefab.name}' for '{def.DisplayName}'" +
                      $" | Muzzle={muzzle != null} | LeftHandIK={LeftHandIKTarget != null}");
        }

        private void DestroyCurrentModel()
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }
            LeftHandIKTarget = null;
        }
    }
}
