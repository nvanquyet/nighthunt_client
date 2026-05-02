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

        [Header("Elevation (Pitch)")]
        [Tooltip("Local-space axis of the weapon model's parent (WeaponR bone) around which the gun " +
                 "pitches up or down.\n" +
                 "Default Vector3.right works when WeaponR local X = character world right.\n" +
                 "Adjust per rig if the gun tilts in the wrong direction or wrong amount.")]
        [SerializeField] private Vector3 _pitchAxis = Vector3.right;

        [Tooltip("Maximum pitch angle (degrees) the gun can tilt up (+) or down (−).\n" +
                 "Prevents extreme poses that look wrong on the character rig.")]
        [Range(0f, 90f)]
        [SerializeField] private float _maxElevationAngle = 60f;

        [Header("Spawn Retry")]
        [Tooltip("Frames to wait when GetWeapon() returns null (inventory cache not yet populated).")]
        [SerializeField] private int _maxSpawnRetryFrames = 8;

        [Header("Animation Visibility Sync")]
        [SerializeField] private bool _syncVisibilityWithAnimator = true;
        [SerializeField, Min(0f)] private float _drawVisualFallbackDelay = 0.35f;
        [SerializeField, Min(0f)] private float _holsterVisualFallbackDelay = 0.4f;

        // ── Runtime refs ───────────────────────────────────────────────────────
        private IWeaponSystem     _weaponSystem;
        private PrActorUtils      _actorUtils;
        private GameObject        _currentModel;
        private GameObject        _pendingModel;
        private WeaponBase        _pendingWeaponBase;
        private Transform         _pendingLeftHandIKTarget;
        private Coroutine         _spawnRetryCoroutine;
        private Coroutine         _visualFallbackCoroutine;

        // ── Elevation state ────────────────────────────────────────────────────
        private float _currentElevationAngle = 0f;

        // ── Public API ─────────────────────────────────────────────────────────
        public Transform LeftHandIKTarget { get; private set; }

        public event Action<WeaponBase>  OnWeaponModelChanged;
        public event Action<Transform>   OnLeftHandIKTargetChanged;

        /// <summary>
        /// Applies a vertical pitch to the current weapon model so it visually aims
        /// up or down toward an acquired target at a different elevation.
        ///
        /// AXIS — In the parent bone's (WeaponR) local space:
        ///   <see cref="_pitchAxis"/> defines which axis to rotate around.
        ///   Default <c>Vector3.right</c> = character's lateral right axis.
        ///   Positive angle  → gun tilts UP   (target is above shooter).
        ///   Negative angle  → gun tilts DOWN  (target is below shooter).
        ///
        /// FORMULA applied to the weapon model's localRotation:
        ///   localRot = Euler(_localRotationWeapon) * AngleAxis(elevationDeg, _pitchAxis)
        ///
        ///   The base Euler offset keeps the model aligned to the rig bone.
        ///   The pitch is layered on top in the parent's local space, so character
        ///   YAW rotation (handled by the bone hierarchy) is unaffected.
        ///
        /// Call with 0f to level the gun (no acquired target / fallback raycast).
        /// Safe to call every frame — no allocation.
        /// </summary>
        public void SetElevationAngle(float elevationDeg)
        {
            float clamped = Mathf.Clamp(elevationDeg, -_maxElevationAngle, _maxElevationAngle);
            // Skip update if change is negligible (< 0.01°).
            if (Mathf.Abs(clamped - _currentElevationAngle) < 0.01f) return;

            _currentElevationAngle = clamped;
            ApplyWeaponRotation();
        }

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
            StopVisualFallback();
            DestroyPendingModel();
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
            StopVisualFallback();
            DestroyPendingModel();

            if (next == null || _weaponSystem == null)
            {
                if (_syncVisibilityWithAnimator && _currentModel != null)
                    ScheduleHolsterFallback();
                else
                    DestroyCurrentModel();
                return;
            }

            var inst = _weaponSystem.GetWeapon(next.Value);
            if (inst == null)
            {
                // Inventory cache not yet ready on this client — retry each frame.
                _spawnRetryCoroutine = StartCoroutine(RetrySpawnWeapon(next.Value));
                return;
            }

            SpawnWeaponModel(next.Value, inst, _syncVisibilityWithAnimator);
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
                    SpawnWeaponModel(slot, inst, _syncVisibilityWithAnimator);
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

        private void StopVisualFallback()
        {
            if (_visualFallbackCoroutine != null)
            {
                StopCoroutine(_visualFallbackCoroutine);
                _visualFallbackCoroutine = null;
            }
        }

        // ── Spawn / Destroy ────────────────────────────────────────────────────

        private void SpawnWeaponModel(WeaponSlotType slot, ItemInstance inst, bool asPending)
        {
            var def = ItemDatabase.GetDefinition(inst.DefinitionID);
            if (def == null)
            {
                Debug.LogWarning($"[WeaponModelController] No ItemDatabase entry for '{inst.DefinitionID}'.");
                return;
            }
            var visualPrefab = ItemVisualResolver.ResolveVisualPrefab(def);
            if (visualPrefab == null)
            {
                Debug.LogWarning($"[WeaponModelController] '{def.DisplayName}' has no VisualPrefab.");
                return;
            }

            Transform parent = (_actorUtils?.WeaponR != null) ? _actorUtils.WeaponR : transform;

            GameObject spawnedModel = Instantiate(visualPrefab, parent);
            spawnedModel.transform.localPosition = Vector3.zero;
            // Use ApplyWeaponRotation so elevation is included from the first frame
            // (typically 0° on spawn, but preserves any elevation set before the model was ready).
            ApplyWeaponRotation(spawnedModel);

            if (asPending)
                spawnedModel.SetActive(false);

            var wb = ComponentResolver.Find<WeaponBase>(spawnedModel)
                .OnSelf().InChildren()
                .Resolve(); // null is valid — weapon may not have WeaponBase

            // Wire WeaponSystem so it delegates fire/FX to the spawned prefab component.
            if (asPending)
            {
                _pendingModel = spawnedModel;
                _pendingWeaponBase = wb;
                _pendingLeftHandIKTarget = wb?.LeftHandIKTarget;
                ScheduleDrawFallback();
                Debug.Log($"[WeaponModelController] Pending '{def.DisplayName}' until Draw event | parent={parent.name} | WeaponBase={wb != null}");
                return;
            }

            float elevation = _currentElevationAngle;
            DestroyCurrentModel(notify: false);
            _currentElevationAngle = elevation;
            _currentModel = spawnedModel;

            _weaponSystem.SetFireOrigin(wb?.FirePoint);
            _weaponSystem.SetCurrentWeaponBase(wb);

            // IK target — read from WeaponBase inspector field (no child-name search needed).
            LeftHandIKTarget = wb?.LeftHandIKTarget;

            OnWeaponModelChanged?.Invoke(wb);
            OnLeftHandIKTargetChanged?.Invoke(LeftHandIKTarget);

            Debug.Log($"[WeaponModelController] Spawned '{def.DisplayName}' | parent={parent.name} " +
                      $"| WeaponBase={wb != null} | FirePoint={wb?.FirePoint != null} | IK={LeftHandIKTarget != null}");
        }

        public void ShowPendingWeaponModelFromAnimation()
        {
            StopVisualFallback();
            ShowPendingWeaponModel("anim-event");
        }

        public void CompleteHolsterFromAnimation()
        {
            StopVisualFallback();
            CompleteHolster("anim-event");
        }

        private void ScheduleDrawFallback()
        {
            if (!_syncVisibilityWithAnimator)
                return;

            _visualFallbackCoroutine = StartCoroutine(DrawVisualFallbackCoroutine());
        }

        private IEnumerator DrawVisualFallbackCoroutine()
        {
            if (_drawVisualFallbackDelay > 0f)
                yield return new WaitForSeconds(_drawVisualFallbackDelay);

            _visualFallbackCoroutine = null;
            ShowPendingWeaponModel("fallback");
        }

        private void ScheduleHolsterFallback()
        {
            _visualFallbackCoroutine = StartCoroutine(HolsterVisualFallbackCoroutine());
        }

        private IEnumerator HolsterVisualFallbackCoroutine()
        {
            if (_holsterVisualFallbackDelay > 0f)
                yield return new WaitForSeconds(_holsterVisualFallbackDelay);

            _visualFallbackCoroutine = null;
            CompleteHolster("fallback");
        }

        private void ShowPendingWeaponModel(string reason)
        {
            if (_pendingModel == null)
            {
                if (_currentModel != null && !_currentModel.activeSelf)
                    _currentModel.SetActive(true);
                return;
            }

            float elevation = _currentElevationAngle;
            DestroyCurrentModel(notify: false);
            _currentElevationAngle = elevation;

            _currentModel = _pendingModel;
            var wb = _pendingWeaponBase;
            LeftHandIKTarget = _pendingLeftHandIKTarget;

            _pendingModel = null;
            _pendingWeaponBase = null;
            _pendingLeftHandIKTarget = null;

            _currentModel.SetActive(true);
            ApplyWeaponRotation();

            _weaponSystem?.SetFireOrigin(wb?.FirePoint);
            _weaponSystem?.SetCurrentWeaponBase(wb);

            OnWeaponModelChanged?.Invoke(wb);
            OnLeftHandIKTargetChanged?.Invoke(LeftHandIKTarget);

            Debug.Log($"[WeaponModelController] Showing weapon model from {reason} | WeaponBase={wb != null} | IK={LeftHandIKTarget != null}");
        }

        private void CompleteHolster(string reason)
        {
            DestroyCurrentModel();
            Debug.Log($"[WeaponModelController] Holster complete from {reason}.");
        }

        private void DestroyCurrentModel(bool notify = true)
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }

            _currentElevationAngle = 0f;
            LeftHandIKTarget = null;
            _weaponSystem?.SetCurrentWeaponBase(null);
            _weaponSystem?.SetFireOrigin(null);

            if (notify)
            {
                OnWeaponModelChanged?.Invoke(null);
                OnLeftHandIKTargetChanged?.Invoke(null);
            }
        }

        private void DestroyPendingModel()
        {
            if (_pendingModel != null)
            {
                Destroy(_pendingModel);
                _pendingModel = null;
            }

            _pendingWeaponBase = null;
            _pendingLeftHandIKTarget = null;
        }

        // ── Weapon rotation ────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the weapon model's localRotation from the base alignment offset
        /// and the current elevation angle.
        ///
        /// localRotation = Euler(_localRotationWeapon) * AngleAxis(_currentElevationAngle, _pitchAxis)
        ///
        /// Reading order (right-to-left in quaternion multiplication):
        ///   1. AngleAxis pitch — rotates the model up/down around _pitchAxis
        ///      (in the PARENT bone's local space, before the base alignment is applied).
        ///   2. Euler base offset — aligns the model mesh to the bone convention (90°, 0°, 0°).
        ///
        /// Net result: the model sits at the correct bone-alignment AND is tilted by the
        /// elevation angle, while the character's YAW (driven by the bone hierarchy above)
        /// remains completely unaffected.
        /// </summary>
        private void ApplyWeaponRotation()
        {
            ApplyWeaponRotation(_currentModel);
        }

        private void ApplyWeaponRotation(GameObject model)
        {
            if (model == null) return;

            Quaternion baseRot  = Quaternion.Euler(_localRotationWeapon);
            Quaternion pitchRot = Quaternion.AngleAxis(_currentElevationAngle, _pitchAxis);

            // Base × Pitch: pitch is applied first (in parent local space), then base aligns to rig.
            model.transform.localRotation = baseRot * pitchRot;
        }
    }
}
