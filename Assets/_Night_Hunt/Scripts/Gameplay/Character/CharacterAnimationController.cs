using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Drives the player Animator each frame from movement + weapon state.
    /// Works for BOTH the local owner and ALL remote clients (position-delta velocity).
    ///
    /// PREFAB SETUP:
    ///   Attach to any GO in the player prefab hierarchy (root or a "Visual" child GO).
    ///   All refs are auto-resolved via root.GetComponentInChildren — no Inspector wiring.
    ///
    /// REMOTE CLIENT SUPPORT:
    ///   OnShotFired / OnReloadStateChanged are now raised on remote clients too
    ///   (via WeaponSystem.NetworkSync RPCs), so all animator parameters stay in sync.
    ///
    /// ANIMATOR PARAMETERS EXPECTED:
    ///   Float  "Speed"       — horizontal speed m/s
    ///   Float  "X"           — strafe (right = +)
    ///   Float  "Y"           — forward (forward = +)
    ///   Bool   "OnGround"
    ///   Float  "VerticalVel"
    ///   Bool   "Armed"
    ///   Bool   "Aiming"      — true briefly after each shot (auto-aim pose)
    ///   Bool   "Reloading"
    ///   Trigger "Shoot"
    ///   Trigger "Jump"
    ///   Trigger "Roll"
    ///
    /// ANIMATOR LAYERS EXPECTED:
    ///   "PistolLyr"    — weight 0→1 when Secondary active
    ///   "RifleActions" — weight 0→1 when Primary active
    ///   "MeleeActions" — weight 0→1 when Melee active
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Ground Detection")]
        [SerializeField] private float     _groundCheckRadius = 0.25f;
        [SerializeField] private float     _groundCheckOffset = 0.05f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("Smoothing")]
        [SerializeField] private float _speedSmoothing          = 12f;
        [SerializeField] private float _layerWeightBlendSpeed   = 8f;
        [SerializeField] private float _aimingResetDelay        = 0.2f;

        // ── Parameter hashes (computed once) ──────────────────────────────────
        private static readonly int SpeedHash     = Animator.StringToHash("Speed");
        private static readonly int XHash         = Animator.StringToHash("X");
        private static readonly int YHash         = Animator.StringToHash("Y");
        private static readonly int OnGroundHash  = Animator.StringToHash("OnGround");
        private static readonly int VerticalHash  = Animator.StringToHash("VerticalVel");
        private static readonly int ArmedHash     = Animator.StringToHash("Armed");
        private static readonly int AimingHash    = Animator.StringToHash("Aiming");
        private static readonly int ReloadingHash = Animator.StringToHash("Reloading");
        private static readonly int ShootHash     = Animator.StringToHash("Shoot");
        private static readonly int JumpHash      = Animator.StringToHash("Jump");
        private static readonly int RollHash      = Animator.StringToHash("Roll");
        private static readonly int ThrowHash     = Animator.StringToHash("Throw");

        // ── Runtime refs (bound at runtime) ───────────────────────────────────
        private PrActorUtils                    _actorUtils;
        private WeaponSystem                    _weaponSystem;
        private IItemUseSystem                  _itemUseSystem;
        private BaseCharacterPredictedMovement  _movement;
        private PlayerModelLoader               _modelLoader;

        // ── Animator layer indices (resolved after model binds) ────────────────
        private int _pistolLayer   = -1;
        private int _rifleLayer    = -1;
        private int _meleeLayer    = -1;
        private int _launcherLayer = -1;   // Optional "LauncherActions" layer — fallback to rifle

        // ── Weapon animation state ─────────────────────────────────────────────
        private WeaponSlotType? _activeSlot;
        private WeaponClass     _activeWeaponClass = WeaponClass.Rifle;
        private bool            _isFiring;
        private bool            _isReloading;
        private float           _targetPistolWeight;
        private float           _targetRifleWeight;
        private float           _targetMeleeWeight;
        private float           _targetLauncherWeight;

        // ── Movement state ─────────────────────────────────────────────────────
        private Vector3 _prevPosition;
        private float   _smoothedSpeed;
        private bool    _started;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = ComponentResolver.Find<WeaponSystem>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterAnimationController] WeaponSystem not found — weapon animations disabled")
                .Resolve();

            _movement = ComponentResolver.Find<BaseCharacterPredictedMovement>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterAnimationController] Movement controller not found — Jump/Roll disabled")
                .Resolve();

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
                .OrLogWarning("[CharacterAnimationController] PlayerModelLoader not found")
                .Resolve();

            if (_modelLoader != null)
                _modelLoader.OnModelReady += BindModel;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnActiveWeaponChanged += OnWeaponChanged;
                _weaponSystem.OnShotFired           += OnShotFired;
                _weaponSystem.OnReloadStateChanged  += OnReloadStateChanged;
            }

            if (_movement != null)
            {
                _movement.OnJumpTriggered += OnJump;
                _movement.OnRollTriggered += OnRoll;
            }

            _itemUseSystem = ComponentResolver.Find<IItemUseSystem>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterAnimationController] IItemUseSystem not found — throw animation disabled")
                .Resolve();

            if (_itemUseSystem != null)
                _itemUseSystem.OnThrowExecuted += HandleThrowExecuted;
        }

        private void OnDestroy()
        {
            if (_modelLoader  != null) _modelLoader.OnModelReady -= BindModel;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnActiveWeaponChanged -= OnWeaponChanged;
                _weaponSystem.OnShotFired           -= OnShotFired;
                _weaponSystem.OnReloadStateChanged  -= OnReloadStateChanged;
            }

            if (_movement != null)
            {
                _movement.OnJumpTriggered -= OnJump;
                _movement.OnRollTriggered -= OnRoll;
            }

            if (_itemUseSystem != null)
                _itemUseSystem.OnThrowExecuted -= HandleThrowExecuted;
        }

        private void Start()
        {
            _prevPosition = transform.position;
            _started      = true;
        }

        // ── Model binding ──────────────────────────────────────────────────────

        private void BindModel(GameObject modelRoot)
        {
            _actorUtils = ComponentResolver.Find<PrActorUtils>(modelRoot)
                .OnSelf().InChildren()
                .OrLogWarning($"[CharacterAnimationController] PrActorUtils not found on '{modelRoot.name}'")
                .Resolve();

            if (_actorUtils == null) return;

            var anim = _actorUtils.charAnimator;
            if (anim != null)
            {
                // Validate that this Animator's controller has the required parameters.
                // If not, PrActorUtils.charAnimator points to the wrong Animator Controller —
                // a clear error is logged and _actorUtils is cleared so Update() won't spam.
                if (!AnimatorHasHash(anim, SpeedHash))
                {
                    Debug.LogError(
                        $"[CharacterAnimationController] Animator '{anim.runtimeAnimatorController?.name}' " +
                        $"on model '{modelRoot.name}' is missing required parameters (e.g. 'Speed'). " +
                        $"Check PrActorUtils.charAnimator — it must point to the player Animator Controller.");
                    _actorUtils = null;
                    return;
                }

                _pistolLayer   = GetLayerIndex(anim, "PistolLyr");
                _rifleLayer    = GetLayerIndex(anim, "RifleActions");
                _meleeLayer    = GetLayerIndex(anim, "MeleeActions");
                _launcherLayer = GetLayerIndex(anim, "LauncherActions"); // optional; -1 if absent
                ApplyWeaponLayerWeights(anim, immediately: true);
            }
        }

        private static int GetLayerIndex(Animator anim, string name)
        {
            for (int i = 0; i < anim.layerCount; i++)
                if (anim.GetLayerName(i) == name) return i;
            return -1;
        }

        private static bool AnimatorHasHash(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash) return true;
            return false;
        }

        // ── Per-frame update ───────────────────────────────────────────────────

        private void Update()
        {
            if (!_started) return;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) { _prevPosition = transform.position; return; }

            // Velocity from position delta — works for owner (predicted) + remote (replicated).
            Vector3 delta  = transform.position - _prevPosition;
            float   hSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / dt;
            float   vVel   = delta.y / dt;

            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, hSpeed, _speedSmoothing * dt);

            // Transform actual velocity to LOCAL space for correct strafe blending.
            // Tank mode:   model faces movement dir  → localVel ≈ (0, 0, +z)
            // Strafe mode: model faces aim dir        → localVel shows real direction
            Vector3 localVel = transform.InverseTransformDirection(delta / dt);

            bool grounded = Physics.CheckSphere(
                transform.position + Vector3.up * _groundCheckOffset,
                _groundCheckRadius, _groundLayers, QueryTriggerInteraction.Ignore);

            // Movement params.
            anim.SetFloat(SpeedHash,    _smoothedSpeed);
            anim.SetFloat(XHash,        localVel.x);
            anim.SetFloat(YHash,        localVel.z);
            anim.SetBool (OnGroundHash, grounded);
            anim.SetFloat(VerticalHash, vVel);

            // Weapon / combat params.
            anim.SetBool(ArmedHash,     _activeSlot.HasValue);
            anim.SetBool(AimingHash,    _isFiring);
            anim.SetBool(ReloadingHash, _isReloading);

            // Smooth weapon layer weights.
            ApplyWeaponLayerWeights(anim, immediately: false);

            _prevPosition = transform.position;
        }

        // ── Weapon event handlers ──────────────────────────────────────────────

        private void OnWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            _activeSlot           = newSlot;
            _targetPistolWeight   = 0f;
            _targetRifleWeight    = 0f;
            _targetMeleeWeight    = 0f;
            _targetLauncherWeight = 0f;

            if (!newSlot.HasValue) return;

            // Resolve weapon class for accurate layer selection.
            var inst = _weaponSystem?.GetWeapon(newSlot.Value);
            var def  = inst != null ? ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition : null;
            _activeWeaponClass = def?.WeaponClass ?? WeaponClass.Rifle;

            // Use weapon class to pick the right layer regardless of which slot is active.
            switch (_activeWeaponClass)
            {
                case WeaponClass.Melee:
                    _targetMeleeWeight = 1f;
                    break;
                case WeaponClass.Pistol:
                case WeaponClass.SMG:
                    _targetPistolWeight = 1f;
                    break;
                case WeaponClass.Launcher:
                    if (_launcherLayer >= 0)
                        _targetLauncherWeight = 1f;
                    else
                        _targetRifleWeight = 1f;
                    break;
                case WeaponClass.Rifle:
                case WeaponClass.Shotgun:
                case WeaponClass.Sniper:
                default:
                    _targetRifleWeight = 1f;
                    break;
            }
        }

        /// <summary>
        /// Raised on EVERY client (owner via local event, remotes via ObserversRpc in NetworkSync).
        /// </summary>
        private void OnShotFired(WeaponSlotType slot, Vector3 dir)
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
                anim.SetTrigger(ShootHash);

            _isFiring = true;
            CancelInvoke(nameof(ResetFiringFlag));
            Invoke(nameof(ResetFiringFlag), _aimingResetDelay);
        }

        private void ResetFiringFlag() => _isFiring = false;

        /// <summary>
        /// Raised on EVERY client (owner via coroutine, remotes via ObserversRpc in NetworkSync).
        /// </summary>
        private void OnReloadStateChanged(bool reloading) => _isReloading = reloading;

        // ── Movement event handlers ────────────────────────────────────────────

        private void OnJump()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled) anim.SetTrigger(JumpHash);
        }

        private void OnRoll()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled) anim.SetTrigger(RollHash);
        }

        private void HandleThrowExecuted()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled) anim.SetTrigger(ThrowHash);
        }

        // ── Layer weight helper ────────────────────────────────────────────────

        private void ApplyWeaponLayerWeights(Animator anim, bool immediately)
        {
            float dt = immediately ? 1f : Time.deltaTime * _layerWeightBlendSpeed;
            SetLayerWeight(anim, _pistolLayer,   _targetPistolWeight,   dt);
            SetLayerWeight(anim, _rifleLayer,    _targetRifleWeight,    dt);
            SetLayerWeight(anim, _meleeLayer,    _targetMeleeWeight,    dt);
            SetLayerWeight(anim, _launcherLayer, _targetLauncherWeight, dt);
        }

        private static void SetLayerWeight(Animator anim, int idx, float target, float dt)
        {
            if (idx < 0) return;
            anim.SetLayerWeight(idx, Mathf.Lerp(anim.GetLayerWeight(idx), target, dt));
        }
    }
}