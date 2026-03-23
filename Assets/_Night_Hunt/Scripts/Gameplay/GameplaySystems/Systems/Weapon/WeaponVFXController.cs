using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.GameplaySystems.Aim;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Weapon visual effects for the local player (aim trail, muzzle flash coordination).
    ///
    /// PREFAB SETUP:
    ///   Lives on the same child GO as WeaponSystem + WeaponModelController ("WeaponSystem" GO).
    ///   All refs are auto-resolved from the same GO or parent hierarchy — no Inspector wiring.
    ///
    /// RESPONSIBILITIES:
    ///   • Aim trail (LineRenderer): visible for _aimTrailLingerDuration seconds after each shot.
    ///   • Muzzle flash: delegated to WeaponBase.PlayMuzzleFlash() — not handled here.
    ///   • Muzzle point: auto-updated from WeaponBase.FirePoint on each weapon swap.
    ///
    /// NOTE: Trail is hidden on remote clients (WeaponVFXController is only relevant for
    /// the local player's camera-facing effects). Remote clients see the projectile trail
    /// spawned by WeaponBase.SpawnVisualBullet() which is already replicated.
    /// </summary>
    public class WeaponVFXController : MonoBehaviour
    {
        [Header("Aim Trail")]
        [SerializeField] private LineRenderer _aimTrailLine;
        [SerializeField] private AimSystem _aimSystemSource;
        [SerializeField] private float _aimTrailFallbackLength = 15f;
        [SerializeField] private float _aimTrailLingerDuration = 0.12f;
        [SerializeField] private Color _aimTrailStartColor  = new Color(1f, 0.4f, 0f, 0.85f);
        [SerializeField] private Color _aimTrailEndColor    = new Color(1f, 0.8f, 0f, 0f);
        [SerializeField] private float _aimTrailStartWidth  = 0.025f;
        [SerializeField] private float _aimTrailEndWidth    = 0.005f;

        // ── Runtime refs ───────────────────────────────────────────────────────
        private IWeaponSystem         _weaponSystem;
        private IAimSystem            _aimSystem;
        private WeaponModelController _modelController;
        private Transform             _muzzlePoint;
        private Coroutine             _hideTrailCoroutine;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf().InParent()
                .OrLogWarning("[WeaponVFXController] IWeaponSystem not found")
                .Resolve();

            _aimSystem = ComponentResolver.Find<IAimSystem>(this)
                .UseExisting(_aimSystemSource)
                .OnSelf().InParent()
                .OrDefault(null)   // optional — injected later via Initialize()
                .Resolve();

            _modelController = ComponentResolver.Find<WeaponModelController>(this)
                .OnSelf().InParent()
                .OrLogWarning("[WeaponVFXController] WeaponModelController not found")
                .Resolve();

            EnsureAimTrail();
        }

        /// <summary>Inject IAimSystem at runtime (called by NetworkPlayer after local spawn).</summary>
        public void Initialize(IAimSystem aimSystem) => _aimSystem = aimSystem;

        private void OnEnable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired           += HandleShotFired;
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            }
            if (_modelController != null)
                _modelController.OnWeaponModelChanged += HandleWeaponModelChanged;
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
            {
                _weaponSystem.OnShotFired           -= HandleShotFired;
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            }
            if (_modelController != null)
                _modelController.OnWeaponModelChanged -= HandleWeaponModelChanged;

            SetTrailVisible(false);
        }

        // ── Per-frame trail position ───────────────────────────────────────────

        private void LateUpdate()
        {
            if (_aimTrailLine == null || !_aimTrailLine.enabled) return;
            if (_muzzlePoint  == null) { SetTrailVisible(false); return; }

            Vector3 dir = _weaponSystem?.GetAimDirection() ?? Vector3.zero;
            if (dir.sqrMagnitude < 0.001f) dir = _muzzlePoint.forward;

            float len = (_aimSystem != null) ? _aimSystem.GetVisionRange() : 0f;
            if (len <= 0f) len = _aimTrailFallbackLength;

            _aimTrailLine.SetPosition(0, _muzzlePoint.position);
            _aimTrailLine.SetPosition(1, _muzzlePoint.position + dir * len);
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleWeaponModelChanged(WeaponBase wb)
        {
            _muzzlePoint = wb?.FirePoint;
            // Trail stays hidden on new weapon draw — activates on first shot only.
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? _, WeaponSlotType? newSlot)
        {
            if (!newSlot.HasValue) { StopHideCoroutine(); SetTrailVisible(false); }
        }

        private void HandleShotFired(WeaponSlotType slot, Vector3 dir)
        {
            if (_muzzlePoint == null) return;
            StopHideCoroutine();
            SetTrailVisible(true);
            _hideTrailCoroutine = StartCoroutine(HideAfterDelay(_aimTrailLingerDuration));
        }

        // ── Trail helpers ──────────────────────────────────────────────────────

        private void SetTrailVisible(bool v) { if (_aimTrailLine != null) _aimTrailLine.enabled = v; }

        private void StopHideCoroutine()
        {
            if (_hideTrailCoroutine != null) { StopCoroutine(_hideTrailCoroutine); _hideTrailCoroutine = null; }
        }

        private System.Collections.IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetTrailVisible(false);
            _hideTrailCoroutine = null;
        }

        private void EnsureAimTrail()
        {
            if (_aimTrailLine != null) return;

            var go = new GameObject("AimTrail");
            go.transform.SetParent(transform, worldPositionStays: false);

            _aimTrailLine                       = go.AddComponent<LineRenderer>();
            _aimTrailLine.positionCount         = 2;
            _aimTrailLine.startWidth            = _aimTrailStartWidth;
            _aimTrailLine.endWidth              = _aimTrailEndWidth;
            _aimTrailLine.useWorldSpace         = true;
            _aimTrailLine.shadowCastingMode     = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimTrailLine.receiveShadows        = false;
            _aimTrailLine.material             = new Material(Shader.Find("Sprites/Default"));
            _aimTrailLine.startColor            = _aimTrailStartColor;
            _aimTrailLine.endColor              = _aimTrailEndColor;
            _aimTrailLine.enabled               = false;
        }
    }
}