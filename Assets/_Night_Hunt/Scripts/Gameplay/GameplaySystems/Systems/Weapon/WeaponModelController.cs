using System;
using System.Collections;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Diagnostics;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Spawns / destroys weapon model GameObjects whenever the active weapon slot changes.
    /// </summary>
    public class WeaponModelController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Leave blank — auto-resolved from same GO.")]
        [SerializeField] private PlayerModelLoader _modelLoader;
        [SerializeField] private WeaponSystem _weaponSystemSource;
        [SerializeField] private Vector3 _localRotationWeapon = new Vector3(90f, 0f, 0f);

        [Header("Elevation (Pitch)")]
        [Tooltip("Local-space axis of the weapon model's parent (WeaponR bone) around which the gun pitches up or down.")]
        [SerializeField] private Vector3 _pitchAxis = Vector3.right;

        [Tooltip("Maximum pitch angle (degrees) the gun can tilt up (+) or down (−).")]
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
        private WeaponBase        _currentWeaponBase;
        private WeaponBase        _pendingWeaponBase;
        private Transform         _pendingLeftHandIKTarget;
        private Coroutine         _spawnRetryCoroutine;
        private Coroutine         _visualFallbackCoroutine;
        private bool              _holsterPending;

        // ── Local offsets cached from WeaponBase ───────────────────────────────
        private Vector3 _baseLocalPosition = Vector3.zero;
        private Vector3 _baseLocalRotation = new Vector3(90f, 0f, 0f);

        // ── Elevation state ────────────────────────────────────────────────────
        private float _currentElevationAngle = 0f;

        // ── Public API ─────────────────────────────────────────────────────────
        public Transform LeftHandIKTarget { get; private set; }

        public event Action<WeaponBase>  OnWeaponModelChanged;
        public event Action<Transform>   OnLeftHandIKTargetChanged;

        public void SetElevationAngle(float elevationDeg)
        {
            float clamped = Mathf.Clamp(elevationDeg, -_maxElevationAngle, _maxElevationAngle);
            if (Mathf.Abs(clamped - _currentElevationAngle) < 0.01f) return;

            _currentElevationAngle = clamped;
            ApplyWeaponRotation();
            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "WeaponElevation",
                $"weapon={_currentModel?.name ?? "null"} elevation={_currentElevationAngle:F1} axis={_pitchAxis:F2}",
                this);
        }

        private void Awake()
        {
            _weaponSystem ??= ComponentResolver.Find<IWeaponSystem>(this)
                .UseExisting(_weaponSystemSource as IWeaponSystem)
                .OnSelf().InParent()
                .OrLogError("[WeaponModelController] IWeaponSystem not found")
                .Resolve();

            _modelLoader ??= ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
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
            _holsterPending = false;
        }

        public void Initialize(IWeaponSystem weaponSystem)
        {
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

            _weaponSystem = weaponSystem;

            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
        }

        private void OnModelReady(GameObject modelRoot)
        {
            _actorUtils = ComponentResolver.Find<PrActorUtils>(modelRoot)
                .OnSelf().InChildren()
                .OrLogWarning("[WeaponModelController] PrActorUtils not found — weapon spawns under controller root")
                .Resolve();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? prev, WeaponSlotType? next)
        {
            StopSpawnRetry();
            StopVisualFallback();
            DestroyPendingModel();
            _holsterPending = false;

            if (next == null || _weaponSystem == null)
            {
                PhaseTestLog.Log(
                    PhaseTestLogCategory.IK,
                    "WeaponModelHolsterRequested",
                    $"prev={prev?.ToString() ?? "none"} next=none current={_currentModel?.name ?? "null"} syncWithAnimator={_syncVisibilityWithAnimator}",
                    this);
                if (_syncVisibilityWithAnimator && _currentModel != null)
                    ScheduleHolsterFallback();
                else
                    DestroyCurrentModel();
                return;
            }

            var inst = _weaponSystem.GetWeapon(next.Value);
            if (inst == null)
            {
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.IK,
                    "WeaponModelSpawnRetry",
                    $"slot={next.Value} reason=weapon-cache-null maxFrames={_maxSpawnRetryFrames}",
                    this);
                _spawnRetryCoroutine = StartCoroutine(RetrySpawnWeapon(next.Value));
                return;
            }

            SpawnWeaponModel(next.Value, inst, _syncVisibilityWithAnimator);
        }

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

        private void SpawnWeaponModel(WeaponSlotType slot, ItemInstance inst, bool asPending)
        {
            _holsterPending = false;

            var def = ItemDatabase.GetDefinition(inst.DefinitionID);
            if (def == null) return;

            var visualPrefab = ItemVisualResolver.ResolveVisualPrefab(def);
            if (visualPrefab == null) return;

            Transform parent = (_actorUtils?.WeaponR != null) ? _actorUtils.WeaponR : transform;

            GameObject spawnedModel = Instantiate(visualPrefab, parent);

            var wb = ComponentResolver.Find<WeaponBase>(spawnedModel)
                .OnSelf().InChildren()
                .Resolve();

            Vector3 localPos = wb != null ? wb.BaseLocalPosition : Vector3.zero;
            Vector3 localRot = wb != null ? wb.BaseLocalRotation : _localRotationWeapon;

            spawnedModel.transform.localPosition = localPos;

            if (asPending)
            {
                _pendingModel = spawnedModel;
                _pendingWeaponBase = wb;
                _pendingLeftHandIKTarget = wb?.LeftHandIKTarget;
                _pendingModel.SetActive(false);
                PhaseTestLog.Log(
                    PhaseTestLogCategory.IK,
                    "WeaponModelPending",
                    $"slot={slot} def={def.ItemID} prefab={visualPrefab.name} parent={parent.name} parentPath={BuildPath(parent)} weaponBase={wb?.GetType().Name ?? "null"} firePoint={DescribeTransform(wb?.FirePoint)} leftIK={DescribeTransform(wb?.LeftHandIKTarget)} localPos={localPos:F3} localRot={localRot:F1}",
                    this);
                ScheduleDrawFallback();
                return;
            }

            float elevation = _currentElevationAngle;
            DestroyCurrentModel(notify: false);
            _currentElevationAngle = elevation;
            _currentModel = spawnedModel;
            _currentWeaponBase = wb;

            _baseLocalPosition = localPos;
            _baseLocalRotation = localRot;

            _weaponSystem.SetFireOrigin(wb?.FirePoint);
            _weaponSystem.SetCurrentWeaponBase(wb);

            LeftHandIKTarget = wb?.LeftHandIKTarget;

            OnWeaponModelChanged?.Invoke(wb);
            OnLeftHandIKTargetChanged?.Invoke(LeftHandIKTarget);

            ApplyWeaponRotation(spawnedModel);

            Debug.Log($"[WeaponModelController] Spawned '{def.DisplayName}' | parent={parent.name} | IK={LeftHandIKTarget != null}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "WeaponModelSpawned",
                $"slot={slot} def={def.ItemID} display='{def.DisplayName}' prefab={visualPrefab.name} parent={parent.name} parentPath={BuildPath(parent)} weaponBase={wb?.GetType().Name ?? "null"} firePoint={DescribeTransform(wb?.FirePoint)} leftIK={DescribeTransform(LeftHandIKTarget)} localPos={localPos:F3} localRot={localRot:F1}",
                this);
        }

        public void ShowPendingWeaponModelFromAnimation()
        {
            if (_pendingModel == null)
                return;

            StopVisualFallback();
            _holsterPending = false;
            ShowPendingWeaponModel("anim-event");
        }

        public void CompleteHolsterFromAnimation()
        {
            if (!_holsterPending)
                return;

            StopVisualFallback();
            _holsterPending = false;
            CompleteHolster("anim-event");
        }

        private void ScheduleDrawFallback()
        {
            if (!_syncVisibilityWithAnimator) return;
            _holsterPending = false;
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
            _holsterPending = true;
            _visualFallbackCoroutine = StartCoroutine(HolsterVisualFallbackCoroutine());
        }

        private IEnumerator HolsterVisualFallbackCoroutine()
        {
            if (_holsterVisualFallbackDelay > 0f)
                yield return new WaitForSeconds(_holsterVisualFallbackDelay);

            _visualFallbackCoroutine = null;
            _holsterPending = false;
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
            _currentWeaponBase = _pendingWeaponBase;
            LeftHandIKTarget = _pendingLeftHandIKTarget;

            _baseLocalPosition = _currentWeaponBase != null ? _currentWeaponBase.BaseLocalPosition : Vector3.zero;
            _baseLocalRotation = _currentWeaponBase != null ? _currentWeaponBase.BaseLocalRotation : _localRotationWeapon;

            _pendingModel = null;
            _pendingWeaponBase = null;
            _pendingLeftHandIKTarget = null;

            _currentModel.SetActive(true);
            ApplyWeaponRotation();

            _weaponSystem?.SetFireOrigin(_currentWeaponBase?.FirePoint);
            _weaponSystem?.SetCurrentWeaponBase(_currentWeaponBase);

            OnWeaponModelChanged?.Invoke(_currentWeaponBase);
            OnLeftHandIKTargetChanged?.Invoke(LeftHandIKTarget);

            Debug.Log($"[WeaponModelController] Showing weapon model from {reason} | IK={LeftHandIKTarget != null}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "WeaponModelShown",
                $"reason={reason} model={_currentModel?.name ?? "null"} weaponBase={_currentWeaponBase?.GetType().Name ?? "null"} firePoint={DescribeTransform(_currentWeaponBase?.FirePoint)} leftIK={DescribeTransform(LeftHandIKTarget)} localPos={_baseLocalPosition:F3} localRot={_baseLocalRotation:F1}",
                this);
        }

        private void CompleteHolster(string reason)
        {
            _holsterPending = false;
            DestroyCurrentModel();
            Debug.Log($"[WeaponModelController] Holster complete from {reason}.");
            PhaseTestLog.Log(PhaseTestLogCategory.IK, "WeaponModelHolstered", $"reason={reason}", this);
        }

        private void DestroyCurrentModel(bool notify = true)
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }

            _currentElevationAngle = 0f;
            _currentWeaponBase = null;
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

        private void ApplyWeaponRotation()
        {
            ApplyWeaponRotation(_currentModel);
        }

        private void ApplyWeaponRotation(GameObject model)
        {
            if (model == null) return;

            Quaternion baseRot  = Quaternion.Euler(_baseLocalRotation);
            Quaternion pitchRot = Quaternion.AngleAxis(_currentElevationAngle, _pitchAxis);

            model.transform.localRotation = baseRot * pitchRot;
            model.transform.localPosition = _baseLocalPosition;
        }

        private static string DescribeTransform(Transform target)
        {
            if (target == null)
                return "null";

            return $"{target.name} path={BuildPath(target)} localPos={target.localPosition:F3} localRot={target.localEulerAngles:F1} world={target.position:F2}";
        }

        private static string BuildPath(Transform target)
        {
            if (target == null)
                return "null";

            string path = target.name;
            Transform cursor = target.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
        }
    }
}
