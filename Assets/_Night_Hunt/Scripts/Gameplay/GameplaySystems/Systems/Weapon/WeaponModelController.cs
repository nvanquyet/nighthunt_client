using System;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Spawns and destroys weapon model GameObjects whenever the active weapon slot changes.
    ///
    /// Follows the same SciFi package convention as PrCharacterInventory:
    ///   • Instantiates EquippedPrefab under PrActorUtils.WeaponR (right-hand bone).
    ///   • Reads PrWeapon.ShootFXPos for the fire/hitscan origin — no child-name search.
    ///   • Reads PrWeapon.useIK + "ArmIK" child for left-hand IK — SciFi convention.
    ///   • PrWeapon self-manages its own muzzle flash (ShootFXFLash/Muzzle) internally.
    ///
    /// Inspector setup: only _weaponSystemSource needs to be assigned.
    /// PrActorUtils is resolved automatically via PlayerModelLoader.OnModelReady.
    ///
    /// Events:
    ///   OnWeaponModelChanged(PrWeapon)  — fires after each swap; WeaponVFXController/
    ///                                     WeaponSystem subscribe for ShootFXPos updates.
    ///   OnLeftHandIKTargetChanged(Transform) — fires after each swap; CharacterVisualController
    ///                                          subscribes for IK wiring.
    /// </summary>
    public class WeaponModelController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (e.g. the WeaponSystem component on this player).")]
        [SerializeField] private MonoBehaviour _weaponSystemSource;
        [SerializeField] private Vector3 localRotationWeapon = new Vector3(90,0,0); 
        // ── Runtime ──────────────────────────────────────────────────────────
        private IWeaponSystem     _weaponSystem;
        private PrActorUtils      _actorUtils;     // resolved via PlayerModelLoader.OnModelReady
        private PlayerModelLoader _modelLoader;
        private GameObject        _currentModel;

        /// <summary>Left-hand IK "ArmIK" Transform on the current weapon model, or null.</summary>
        public Transform LeftHandIKTarget { get; private set; }

        /// <summary>
        /// Fired after every weapon swap with the spawned model's WeaponBase component,
        /// or null when holstered. WeaponVFXController subscribes to update the muzzle point.
        /// </summary>
        public event Action<NightHunt.Gameplay.Character.Combat.Weapons.WeaponBase> OnWeaponModelChanged;

        /// <summary>
        /// Fired after every weapon swap with the left-hand IK Transform (may be null).
        /// CharacterVisualController subscribes here so IK is set only after model is live.
        /// </summary>
        public event Action<Transform> OnLeftHandIKTargetChanged;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemSource as IWeaponSystem;
            if (_weaponSystem == null && _weaponSystemSource != null)
                Debug.LogWarning("[WeaponModelController] _weaponSystemSource does not implement IWeaponSystem.");

            // PrActorUtils lives on the dynamically-spawned model child — bind once it's ready.
            _modelLoader = GetComponent<PlayerModelLoader>();
            if (_modelLoader != null)
                _modelLoader.OnModelReady += OnModelReady;
        }

        private void OnDestroy()
        {
            if (_modelLoader != null)
                _modelLoader.OnModelReady -= OnModelReady;
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

        // ── Model Binding ─────────────────────────────────────────────────────

        private void OnModelReady(GameObject modelRoot)
        {
            _actorUtils = modelRoot.GetComponentInChildren<PrActorUtils>(true);
            if (_actorUtils == null)
                Debug.LogWarning("[WeaponModelController] PrActorUtils not found on model — weapon will spawn under controller root.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Re-inject IWeaponSystem at runtime (e.g. from NetworkPlayer after late spawn).
        /// PrActorUtils is always self-resolved via PlayerModelLoader.
        /// </summary>
        public void Initialize(IWeaponSystem weaponSystem)
        {
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

            _weaponSystem = weaponSystem;

            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void HandleActiveWeaponChanged(WeaponSlotType? previousSlot, WeaponSlotType? newSlot)
        {
            Debug.Log($"[WeaponModelController] OnActiveWeaponChanged: {previousSlot} → {newSlot} | " +
                      $"weaponSystem={_weaponSystem != null} | actorUtils={_actorUtils != null} | " +
                      $"WeaponR={_actorUtils?.WeaponR != null}");

            DestroyCurrentModel();

            if (newSlot == null || _weaponSystem == null)
            {
                Debug.Log("[WeaponModelController] Skipping spawn — no active slot or weaponSystem.");
                return;
            }

            var inst = _weaponSystem.GetWeapon(newSlot.Value);
            if (inst == null)
            {
                Debug.LogWarning($"[WeaponModelController] GetWeapon({newSlot.Value}) returned null — item not in cache yet.");
                return;
            }

            var def = ItemDatabase.GetDefinition(inst.DefinitionID);
            if (def == null)
            {
                Debug.LogWarning($"[WeaponModelController] No ItemDatabase definition for '{inst.DefinitionID}'.");
                return;
            }
            if (def.EquippedPrefab == null)
            {
                Debug.LogWarning($"[WeaponModelController] '{def.DisplayName}' has no EquippedPrefab set in ItemDatabase.");
                return;
            }

            Transform parent = (_actorUtils != null && _actorUtils.WeaponR != null)
                ? _actorUtils.WeaponR
                : transform;

            _currentModel = Instantiate(def.EquippedPrefab, parent);

            _currentModel.transform.localPosition = Vector3.zero;
            //Change local rotation to localRotationWeapon value
            _currentModel.transform.localRotation = Quaternion.Euler(localRotationWeapon);

            var weaponBase = _currentModel.GetComponent<NightHunt.Gameplay.Character.Combat.Weapons.WeaponBase>();

            // Set fire origin from WeaponBase.FirePoint (replaces PrWeapon.ShootFXPos).
            _weaponSystem.SetFireOrigin(weaponBase != null ? weaponBase.FirePoint : null);

            // Wire WeaponBase so WeaponSystem delegates ballistics to the prefab component.
            _weaponSystem.SetCurrentWeaponBase(weaponBase);

            OnWeaponModelChanged?.Invoke(weaponBase);

            // Left-hand IK target from WeaponBase inspector field (replaces Find("ArmIK")).
            LeftHandIKTarget = weaponBase?.LeftHandIKTarget;
            OnLeftHandIKTargetChanged?.Invoke(LeftHandIKTarget);

            Debug.Log($"[WeaponModelController] Spawned '{def.EquippedPrefab.name}' for '{def.DisplayName}'" +
                      $" | parent={parent.name}" +
                      $" | WeaponBase={weaponBase != null}" +
                      $" | FirePoint={weaponBase?.FirePoint != null}" +
                      $" | IKTarget={LeftHandIKTarget != null}");
        }

        private void DestroyCurrentModel()
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }
            LeftHandIKTarget = null;
            OnLeftHandIKTargetChanged?.Invoke(null);
            OnWeaponModelChanged?.Invoke(null);
            _weaponSystem?.SetCurrentWeaponBase(null);
        }
    }
}
