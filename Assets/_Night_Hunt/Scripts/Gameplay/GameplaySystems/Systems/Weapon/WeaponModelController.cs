using System;
using System.Collections;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Combat.Weapons;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Spawns / destroys weapon model GameObjects whenever the active weapon slot changes.
    ///
    /// PREFAB SETUP:
    ///   Lives on the same child GO as WeaponSystem (e.g. "WeaponSystem" child).
    ///   PlayerModelLoader is auto-found on the root via GetComponentInParent.
    ///
    /// RACE CONDITION FIX:
    ///   On remote clients the SyncVar fires before the inventory cache is populated.
    ///   HandleActiveWeaponChanged now retries for up to 5 frames before giving up.
    ///
    /// EVENTS:
    ///   OnWeaponModelChanged(WeaponBase)      — fires after each swap (null = holstered).
    ///   OnLeftHandIKTargetChanged(Transform)  — fires after each swap (null = holstered).
    /// </summary>
    public class WeaponModelController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave blank — auto-resolved from same GO.")]
        [SerializeField] private PlayerModelLoader _modelLoader;
        [SerializeField] private WeaponSystem _weaponSystemSource;
        [SerializeField] private Vector3 _localRotationWeapon = new Vector3(90f, 0f, 0f);

        [Header("Spawn Retry")]
        [Tooltip("Frames to wait when GetWeapon() returns null (inventory cache not yet populated).")]
        [SerializeField] private int _maxSpawnRetryFrames = 8;

        // ── Runtime refs ───────────────────────────────────────────────────────
        private IWeaponSystem     _weaponSystem;
        private PrActorUtils      _actorUtils;
        private GameObject        _currentModel;
        private Coroutine         _spawnRetryCoroutine;

        // ── Public API ─────────────────────────────────────────────────────────
        public Transform LeftHandIKTarget { get; private set; }

        public event Action<WeaponBase>  OnWeaponModelChanged;
        public event Action<Transform>   OnLeftHandIKTargetChanged;

        // ── Unity lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _weaponSystem ??= ComponentResolver.Find<IWeaponSystem>(this)
                .UseExisting(_weaponSystemSource as IWeaponSystem)
                .OnSelf().InParent()
                .OrLogError("[WeaponModelController] IWeaponSystem not found")
                .Resolve();

            _modelLoader ??= ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().InParent().OnRoot()
                .OrLogWarning("[WeaponModelController] PlayerModelLoader not found")
                .Resolve();

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

            StopSpawnRetry();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Re-inject IWeaponSystem at runtime (e.g. from NetworkPlayer after late spawn).</summary>
        public void Initialize(IWeaponSystem weaponSystem)
        {
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

            _weaponSystem = weaponSystem;

            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        // ── Model binding ──────────────────────────────────────────────────────

        private void OnModelReady(GameObject modelRoot)
        {
            _actorUtils = ComponentResolver.Find<PrActorUtils>(modelRoot)
                .OnSelf().InChildren()
                .OrLogWarning("[WeaponModelController] PrActorUtils not found — weapon spawns under controller root")
                .Resolve();
        }

        // ── Active weapon changed ──────────────────────────────────────────────

        private void HandleActiveWeaponChanged(WeaponSlotType? prev, WeaponSlotType? next)
        {
            StopSpawnRetry();
            DestroyCurrentModel();

            if (next == null || _weaponSystem == null) return;

            var inst = _weaponSystem.GetWeapon(next.Value);
            if (inst == null)
            {
                // Inventory cache not yet ready on this client — retry each frame.
                _spawnRetryCoroutine = StartCoroutine(RetrySpawnWeapon(next.Value));
                return;
            }

            SpawnWeaponModel(next.Value, inst);
        }

        /// <summary>
        /// Retries SpawnWeaponModel each frame until inventory cache is ready.
        /// Covers the race where the SyncVar arrives before SyncDictionary is populated
        /// on freshly-connected remote clients.
        /// </summary>
        private IEnumerator RetrySpawnWeapon(WeaponSlotType slot)
        {
            for (int i = 0; i < _maxSpawnRetryFrames; i++)
            {
                yield return null;

                var inst = _weaponSystem?.GetWeapon(slot);
                if (inst != null)
                {
                    SpawnWeaponModel(slot, inst);
                    _spawnRetryCoroutine = null;
                    yield break;
                }
            }

            Debug.LogWarning($"[WeaponModelController] RetrySpawn: GetWeapon({slot}) still null after " +
                             $"{_maxSpawnRetryFrames} frames — model not spawned.");
            _spawnRetryCoroutine = null;
        }

        private void StopSpawnRetry()
        {
            if (_spawnRetryCoroutine != null)
            {
                StopCoroutine(_spawnRetryCoroutine);
                _spawnRetryCoroutine = null;
            }
        }

        // ── Spawn / Destroy ────────────────────────────────────────────────────

        private void SpawnWeaponModel(WeaponSlotType slot, ItemInstance inst)
        {
            var def = ItemDatabase.GetDefinition(inst.DefinitionID);
            if (def == null)
            {
                Debug.LogWarning($"[WeaponModelController] No ItemDatabase entry for '{inst.DefinitionID}'.");
                return;
            }
            if (def.HeldPrefab == null)
            {
                Debug.LogWarning($"[WeaponModelController] '{def.DisplayName}' has no HeldPrefab.");
                return;
            }

            Transform parent = (_actorUtils?.WeaponR != null) ? _actorUtils.WeaponR : transform;

            _currentModel = Instantiate(def.HeldPrefab, parent);
            _currentModel.transform.localPosition = Vector3.zero;
            _currentModel.transform.localRotation = Quaternion.Euler(_localRotationWeapon);

            var wb = ComponentResolver.Find<WeaponBase>(_currentModel)
                .OnSelf().InChildren()
                .Resolve(); // null is valid — weapon may not have WeaponBase

            // Wire WeaponSystem so it delegates fire/FX to the spawned prefab component.
            _weaponSystem.SetFireOrigin(wb?.FirePoint);
            _weaponSystem.SetCurrentWeaponBase(wb);

            // IK target — read from WeaponBase inspector field (no child-name search needed).
            LeftHandIKTarget = wb?.LeftHandIKTarget;

            OnWeaponModelChanged?.Invoke(wb);
            OnLeftHandIKTargetChanged?.Invoke(LeftHandIKTarget);

            Debug.Log($"[WeaponModelController] Spawned '{def.DisplayName}' | parent={parent.name} " +
                      $"| WeaponBase={wb != null} | FirePoint={wb?.FirePoint != null} | IK={LeftHandIKTarget != null}");
        }

        private void DestroyCurrentModel()
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }

            LeftHandIKTarget = null;
            _weaponSystem?.SetCurrentWeaponBase(null);
            _weaponSystem?.SetFireOrigin(null);

            OnWeaponModelChanged?.Invoke(null);
            OnLeftHandIKTargetChanged?.Invoke(null);
        }
    }
}