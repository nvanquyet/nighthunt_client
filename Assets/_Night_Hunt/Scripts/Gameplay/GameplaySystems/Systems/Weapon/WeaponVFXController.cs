using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Character.Combat.Weapons;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Handles weapon visual effects for the local player's character.
    ///
    /// Attach to the same GameObject as WeaponSystem and WeaponModelController.
    ///
    /// Architecture:
    ///   • Subscribes to WeaponModelController.OnWeaponModelChanged.
    ///     When a new weapon model is spawned, _muzzlePoint is updated from
    ///     WeaponBase.FirePoint — set directly on the weapon prefab Inspector.
    ///   • Aim trail: a short LineRenderer drawn from the muzzle point in the
    ///     current aim direction. Visible whenever a weapon is drawn; hidden on holster.
    ///   • PrWeapon self-manages its muzzle flash (ShootFXFLash / Muzzle child);
    ///     this controller handles any extra centralised effects.
    ///
    /// Inspector setup: only _weaponSystemSource needs to be assigned.
    /// _muzzlePoint and _aimTrailLine are updated automatically on every weapon swap.
    /// </summary>
    public class WeaponVFXController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (e.g. the WeaponSystem component).")]
        [SerializeField] private MonoBehaviour _weaponSystemSource;

        [Tooltip("Parent transform used to keep instantiated VFX organised. If null, spawns at scene root.")]
        [SerializeField] private Transform _vfxPoolParent;

        [Header("Aim Trail")]
        [Tooltip("Optional LineRenderer used for the aim direction trail. Auto-created if left empty.")]
        [SerializeField] private LineRenderer _aimTrailLine;
        [Tooltip("MonoBehaviour that implements IAimSystem — trail length is synced to VisionRange each frame.")]
        [SerializeField] private MonoBehaviour _aimSystemSource;
        [Tooltip("Fallback trail length (world units) used when IAimSystem is not assigned or returns 0.")]
        [SerializeField] private float _aimTrailFallbackLength = 15f;
        [Tooltip("How many seconds the trail stays visible after a shot (0 = hide immediately after one frame).")]
        [SerializeField] private float _aimTrailLingerDuration = 0.12f;
        [Tooltip("Start colour of the aim trail (tip of gun).")]
        [SerializeField] private Color _aimTrailStartColor = new Color(1f, 0.4f, 0f, 0.85f);
        [Tooltip("End colour of the aim trail (fades out toward target).")]
        [SerializeField] private Color _aimTrailEndColor   = new Color(1f, 0.8f, 0f, 0f);
        [Tooltip("Width at the muzzle end.")]
        [SerializeField] private float _aimTrailStartWidth = 0.025f;
        [Tooltip("Width at the far end.")]
        [SerializeField] private float _aimTrailEndWidth   = 0.005f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private IWeaponSystem         _weaponSystem;
        private IAimSystem            _aimSystem;
        private WeaponModelController _modelController;
        private Transform             _muzzlePoint; // updated from WeaponBase.FirePoint on each weapon swap
        private Coroutine             _hideTrailCoroutine;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemSource as IWeaponSystem;
            if (_weaponSystem == null)
                Debug.LogWarning("[WeaponVFXController] _weaponSystemSource does not implement IWeaponSystem.");

            _aimSystem = _aimSystemSource as IAimSystem;

            _modelController = GetComponent<WeaponModelController>();
            if (_modelController == null)
                Debug.LogWarning("[WeaponVFXController] WeaponModelController not found on same GameObject — muzzle point won't auto-update.");

            EnsureAimTrail();
        }

        /// <summary>
        /// Wire the IAimSystem at runtime (called from NetworkPlayer after the local player spawns).
        /// Removes the need to assign _aimSystemSource in the Inspector.
        /// </summary>
        public void Initialize(IAimSystem aimSystem)
        {
            _aimSystem = aimSystem;
        }

        private void Start()
        {
            // Trail starts hidden — only activates after the first shot.
        }

        private void OnEnable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired          += HandleShotFired;
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            }
            if (_modelController != null)
                _modelController.OnWeaponModelChanged += HandleWeaponModelChanged;
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired          -= HandleShotFired;
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            }
            if (_modelController != null)
                _modelController.OnWeaponModelChanged -= HandleWeaponModelChanged;

            SetAimTrailVisible(false);
        }

        private void LateUpdate()
        {
            if (_aimTrailLine == null || !_aimTrailLine.enabled) return;
            if (_muzzlePoint == null) { SetAimTrailVisible(false); return; }

            // Fetch aim direction each frame — exposed by IWeaponSystem.GetAimDirection().
            Vector3 aimDir = _weaponSystem?.GetAimDirection() ?? Vector3.zero;
            if (aimDir.sqrMagnitude < 0.001f)
                aimDir = _muzzlePoint.forward; // fallback: weapon's own forward

            // Trail length = VisionRange so it matches the aiming circle on the ground.
            float trailLength = _aimSystem != null ? _aimSystem.GetVisionRange() : 0f;
            if (trailLength <= 0f) trailLength = _aimTrailFallbackLength;

            _aimTrailLine.SetPosition(0, _muzzlePoint.position);
            _aimTrailLine.SetPosition(1, _muzzlePoint.position + aimDir * trailLength);
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        /// <summary>
        /// Auto-updates _muzzlePoint from the spawned model's WeaponBase.FirePoint.
        /// </summary>
        private void HandleWeaponModelChanged(WeaponBase weaponBase)
        {
            _muzzlePoint = weaponBase?.FirePoint;
            // Trail stays hidden when a new weapon is drawn — it only appears on the first shot.
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            // Holstering: hide the trail immediately.
            if (!newSlot.HasValue)
            {
                StopHideTrailCoroutine();
                SetAimTrailVisible(false);
            }
            // Equipping: do NOT show trail — waits for next shot.
        }

        private void HandleShotFired(WeaponSlotType slot, Vector3 aimDirection)
        {
            // Flash the trail on every shot; hide it after _aimTrailLingerDuration seconds.
            if (_muzzlePoint == null) return;
            StopHideTrailCoroutine();
            SetAimTrailVisible(true);
            _hideTrailCoroutine = StartCoroutine(HideTrailAfterDelay(_aimTrailLingerDuration));
        }

        private System.Collections.IEnumerator HideTrailAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetAimTrailVisible(false);
            _hideTrailCoroutine = null;
        }

        private void StopHideTrailCoroutine()
        {
            if (_hideTrailCoroutine != null)
            {
                StopCoroutine(_hideTrailCoroutine);
                _hideTrailCoroutine = null;
            }
        }

        // ── Aim Trail Helpers ─────────────────────────────────────────────────

        private void SetAimTrailVisible(bool visible)
        {
            if (_aimTrailLine != null)
                _aimTrailLine.enabled = visible;
        }

        /// <summary>
        /// Ensures a LineRenderer for the aim trail exists. If <see cref="_aimTrailLine"/> was
        /// not assigned in the Inspector, a child GameObject is auto-created with default material.
        /// </summary>
        private void EnsureAimTrail()
        {
            if (_aimTrailLine != null) return;

            var go = new GameObject("AimTrail");
            go.transform.SetParent(transform, worldPositionStays: false);

            _aimTrailLine                 = go.AddComponent<LineRenderer>();
            _aimTrailLine.positionCount   = 2;
            _aimTrailLine.startWidth      = _aimTrailStartWidth;
            _aimTrailLine.endWidth        = _aimTrailEndWidth;
            _aimTrailLine.useWorldSpace   = true;
            _aimTrailLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimTrailLine.receiveShadows  = false;

            // Use the built-in Sprites/Default shader so no custom shader asset is required.
            var mat = new Material(Shader.Find("Sprites/Default"));
            _aimTrailLine.material        = mat;
            _aimTrailLine.startColor      = _aimTrailStartColor;
            _aimTrailLine.endColor        = _aimTrailEndColor;

            _aimTrailLine.enabled = false; // hidden until weapon is drawn
        }

        // ── Pooling helpers ───────────────────────────────────────────────────
        // Reserved for future centralised VFX (muzzle flash, bullet trails, hit decals).
    }
}
