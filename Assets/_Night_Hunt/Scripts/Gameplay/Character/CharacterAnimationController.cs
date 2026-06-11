using System.Collections;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Audio;
using NightHunt.Diagnostics;

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
    /// CURRENT PLAYER CONTROLLER:
    ///   Runtime player model uses Toon Soldiers/SoldierAnimatorController.
    ///   Required movement params: Speed, VelocityX, VelocityY, IsGrounded, IsCrouching, IsSprinting.
    ///   Required combat params: WeaponType, Shoot, Reload, Draw, WeaponChanged,
    ///   WeaponChangedUB, WeaponChangedDeath, TakeDamage, Die, Respawn, DeathIndex.
    ///   Required action params: Interact, InteractIndex, Attack, AttackIndex, ThrowGrenade.
    ///   Optional legacy params are still written safely when present: X, Y, OnGround,
    ///   VerticalVel, Armed, Aiming, Reloading, Jump, Throw, Holster, Deploy.
    ///
    /// ACTUAL PLAYER LAYERS:
    ///   Base Layer, UpperBody, Death. Weapon state is selected mainly by WeaponType
    ///   and WeaponChanged triggers. Legacy per-weapon layer names are optional only.
    ///
    /// WEAPON SWITCH FLOW:
    ///   Equip: ClearTriggers -> SetInt(WeaponType) -> EndOfFrame -> WeaponChanged(x3) -> Draw
    ///   Holster: ClearTriggers -> Holster/WeaponChangedUB flow -> SetInt(0) -> WeaponChanged(x3)
    ///
    /// ANIMATION EVENT CALLBACKS (set in Animator clip events):
    ///   OnAnimEventFireBullet / OnAnimEventDrawWeapon / OnAnimEventHolsterComplete
    ///   OnAnimEventReloadStart / OnAnimEventReloadInsert / OnAnimEventReloadEnd
    ///   OnAnimEventReloadOut / OnAnimEventReloadIn / OnAnimEventReloadComplete
    ///   OnAnimEventMeleeHit
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

        [Header("Death")]
        [Tooltip("Animator layer index for the Death overlay (default 2). Must match your Animator Controller.")]
        [SerializeField] private int _deathLayerIndex = 2;

        [Header("Melee")]
        [Tooltip("Fallback hit timing when melee clips do not have OnAnimEventMeleeHit yet.")]
        [SerializeField] private bool _enableMeleeHitFallback = true;

        [Tooltip("Seconds after Shoot/attack trigger to run melee damage if no animation event is present.")]
        [SerializeField, Min(0f)] private float _meleeHitFallbackDelay = 0.28f;

        [Header("Weapon Timing")]
        [Tooltip("Fallback draw/holster seconds when the weapon has no DrawSpeed stat.")]
        [SerializeField, Min(0.01f)] private float _defaultWeaponDrawSeconds = 0.4f;

        [Tooltip("Fraction of DrawSpeed used for armed-to-armed swap holster anticipation.")]
        [SerializeField, Range(0.1f, 1f)] private float _swapHolsterDelayScale = 0.5f;

        [SerializeField, Min(0.01f)] private float _minWeaponTransitionSeconds = 0.05f;
        [SerializeField, Min(0.05f)] private float _maxWeaponTransitionSeconds = 1.5f;
        [SerializeField, Min(0f)] private float _reloadEndUpperBodyBlendSeconds = 0.05f;

        // ── Parameter hashes (computed once) ──────────────────────────────────
        private static readonly int SpeedHash            = Animator.StringToHash("Speed");
        private static readonly int XHash                = Animator.StringToHash("X");
        private static readonly int YHash                = Animator.StringToHash("Y");
        private static readonly int VelocityXHash        = Animator.StringToHash("VelocityX");
        private static readonly int VelocityYHash        = Animator.StringToHash("VelocityY");
        private static readonly int OnGroundHash         = Animator.StringToHash("OnGround");
        private static readonly int IsGroundedHash       = Animator.StringToHash("IsGrounded");
        private static readonly int VerticalHash         = Animator.StringToHash("VerticalVel");
        private static readonly int ArmedHash            = Animator.StringToHash("Armed");
        private static readonly int AimingHash           = Animator.StringToHash("Aiming");
        private static readonly int ReloadingHash        = Animator.StringToHash("Reloading");
        private static readonly int ShootHash            = Animator.StringToHash("Shoot");
        private static readonly int ShootBurstHash       = Animator.StringToHash("ShootBurst");
        private static readonly int ShootLoopHash        = Animator.StringToHash("ShootLoop");
        private static readonly int ShootBoltHash        = Animator.StringToHash("ShootBolt");
        private static readonly int ShootShotgunHash     = Animator.StringToHash("ShootShotgun");
        private static readonly int AttackHash           = Animator.StringToHash("Attack");
        private static readonly int MeleeAttackHash      = Animator.StringToHash("MeleeAttack");
        private static readonly int JumpHash             = Animator.StringToHash("Jump");
        private static readonly int RollHash             = Animator.StringToHash("Roll");
        private static readonly int ThrowHash            = Animator.StringToHash("Throw");
        // ── New params ──────────────────────────────────────────────────────────
        private static readonly int CrouchHash           = Animator.StringToHash("IsCrouching");
        private static readonly int ProneHash            = Animator.StringToHash("IsProne");
        private static readonly int GuardHash            = Animator.StringToHash("IsGuard");
        private static readonly int SprintHash           = Animator.StringToHash("IsSprinting");
        private static readonly int OnLadderHash         = Animator.StringToHash("IsOnLadder");
        private static readonly int WeaponTypeHash       = Animator.StringToHash("WeaponType");
        private static readonly int DrawHash             = Animator.StringToHash("Draw");
        private static readonly int HolsterHash          = Animator.StringToHash("Holster");
        private static readonly int WpnChangedHash       = Animator.StringToHash("WeaponChanged");
        private static readonly int WpnChangedUBHash     = Animator.StringToHash("WeaponChangedUB");
        private static readonly int WpnChangedDeathHash  = Animator.StringToHash("WeaponChangedDeath");
        private static readonly int ReloadTrigHash       = Animator.StringToHash("Reload");
        private static readonly int TakeDamageHash       = Animator.StringToHash("TakeDamage");
        private static readonly int DieHash              = Animator.StringToHash("Die");
        private static readonly int RespawnHash          = Animator.StringToHash("Respawn");
        private static readonly int DeathIndexHash       = Animator.StringToHash("DeathIndex");
        // ── Additional action triggers (match SoldierAnimatorTester contract) ─
        private static readonly int InteractHash         = Animator.StringToHash("Interact");
        private static readonly int InteractIndexHash    = Animator.StringToHash("InteractIndex");
        private static readonly int AttackIndexHash      = Animator.StringToHash("AttackIndex");
        private static readonly int ThrowGrenadeHash     = Animator.StringToHash("ThrowGrenade");
        private static readonly int DeployHash           = Animator.StringToHash("Deploy");

        // ── Runtime refs (bound at runtime) ───────────────────────────────────
        private PrActorUtils                      _actorUtils;
        private WeaponSystem                      _weaponSystem;
        private IItemUseSystem                    _itemUseSystem;
        private BaseCharacterPredictedMovement    _movement;
        private PlayerModelLoader                 _modelLoader;
        private CharacterLifecycleController      _lifecycle;
        private PlayerHealthSystem                _healthSystem;
        private MeleeDamageController             _meleeDamageController;
        private WeaponModelController             _weaponModelController;

        public event System.Action OnDeathAnimationComplete;

        public bool IsSwitching => _isSwitching;
        public bool IsReloading => _isReloading;

        public bool IsUpperBodyReady
        {
            get
            {
                var anim = _actorUtils?.charAnimator;
                if (anim == null || !anim.enabled) return false;
                return IsUpperBodyReadyForWeaponTriggers(anim, out _);
            }
        }

        // ── Animator layer indices (resolved after model binds) ────────────────
        private int _pistolLayer   = -1;
        private int _pistolActionsLayer = -1;
        private int _rifleLayer    = -1;
        private int _meleeLayer    = -1;
        private int _launcherLayer = -1;   // Optional "LauncherActions" layer — fallback to rifle
        private int _upperBodyLayer = -1;
        private int _partialActionsLayer = -1;

        // ── Weapon animation state ─────────────────────────────────────────────
        private WeaponSlotType? _activeSlot;
        private WeaponClass     _activeWeaponClass = WeaponClass.Rifle;
        private int             _activeWeaponType  = 0;   // integer driving WeaponType param
        private bool            _isFiring;
        private bool            _isReloading;
        private float           _targetPistolWeight;
        private float           _targetRifleWeight;
        private float           _targetMeleeWeight;
        private float           _targetLauncherWeight;
        private bool            _isSwitching;             // blocks new switch during coroutine
        private Coroutine       _weaponSwitchCoroutine;
        private Coroutine       _meleeFallbackCoroutine;
        private Coroutine       _delayedReloadVisualCoroutine;

        // ── Movement state ─────────────────────────────────────────────────────
        private Vector3 _prevPosition;
        private float   _smoothedSpeed;
        private bool    _started;
        private int[]   _prevAnimatorStateHashes;

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

            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterAnimationController] CharacterLifecycleController not found — Die/Respawn animations disabled")
                .Resolve();

            if (_modelLoader != null)
                _modelLoader.OnModelReady += BindModel;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnActiveWeaponChanged += OnWeaponChanged;
                _weaponSystem.OnWeaponEquipped      += OnWeaponEquipped;
                _weaponSystem.OnWeaponUnequipped    += OnWeaponUnequipped;
                _weaponSystem.OnShotFired           += OnShotFired;
                _weaponSystem.OnReloadStateChanged  += OnReloadStateChanged;
            }

            if (_movement != null)
            {
                _movement.OnJumpTriggered += OnJump;
                _movement.OnRollTriggered += OnRoll;
            }

            if (_lifecycle != null)
            {
                _lifecycle.OnDied      += HandleDied;
                _lifecycle.OnRespawned += HandleRespawned;
            }

            _healthSystem = ComponentResolver.Find<PlayerHealthSystem>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrDefault(null)
                .Resolve();

            if (_healthSystem != null)
                _healthSystem.OnHitReceived += HandleHitReceived;

            _meleeDamageController = ComponentResolver.Find<MeleeDamageController>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrDefault(null)
                .Resolve();

            _weaponModelController = ComponentResolver.Find<WeaponModelController>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrDefault(null)
                .Resolve();

            _itemUseSystem = ComponentResolver.Find<IItemUseSystem>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterAnimationController] IItemUseSystem not found — throw animation disabled")
                .Resolve();

            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted += HandleItemUseStarted;
                _itemUseSystem.OnThrowPrepareStarted += HandleThrowPrepareStarted;
                _itemUseSystem.OnThrowExecuted += HandleThrowExecuted;
                _itemUseSystem.OnDeployStarted += HandleDeployStarted;
            }
        }

        private void OnDestroy()
        {
            if (_modelLoader  != null) _modelLoader.OnModelReady -= BindModel;

            if (_weaponSystem != null)
            {
                _weaponSystem.OnActiveWeaponChanged -= OnWeaponChanged;
                _weaponSystem.OnWeaponEquipped      -= OnWeaponEquipped;
                _weaponSystem.OnWeaponUnequipped    -= OnWeaponUnequipped;
                _weaponSystem.OnShotFired           -= OnShotFired;
                _weaponSystem.OnReloadStateChanged  -= OnReloadStateChanged;
            }

            if (_movement != null)
            {
                _movement.OnJumpTriggered -= OnJump;
                _movement.OnRollTriggered -= OnRoll;
            }

            if (_lifecycle != null)
            {
                _lifecycle.OnDied      -= HandleDied;
                _lifecycle.OnRespawned -= HandleRespawned;
            }

            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted -= HandleItemUseStarted;
                _itemUseSystem.OnThrowPrepareStarted -= HandleThrowPrepareStarted;
                _itemUseSystem.OnThrowExecuted -= HandleThrowExecuted;
                _itemUseSystem.OnDeployStarted -= HandleDeployStarted;
            }

            if (_healthSystem != null)
                _healthSystem.OnHitReceived -= HandleHitReceived;

            if (_delayedReloadVisualCoroutine != null)
            {
                StopCoroutine(_delayedReloadVisualCoroutine);
                _delayedReloadVisualCoroutine = null;
            }
        }

        private void HandleHitReceived(DamageInfo _) => TriggerTakeDamage();

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

            var anim = ResolveModelAnimator(modelRoot, _actorUtils);
            if (anim == null)
            {
                Debug.LogError(
                    $"[CharacterAnimationController] Model '{modelRoot.name}' has PrActorUtils but no Animator. " +
                    "Player animation binding failed.");
                _actorUtils = null;
                return;
            }

            // Validate that this Animator's controller has the required parameters.
            // If not, PrActorUtils.charAnimator points to the wrong Animator Controller -
            // a clear error is logged and _actorUtils is cleared so Update() won't spam.
            if (!AnimatorHasHash(anim, SpeedHash))
            {
                Debug.LogError(
                    $"[CharacterAnimationController] Animator '{anim.runtimeAnimatorController?.name}' " +
                    $"on model '{modelRoot.name}' is missing required parameters (e.g. 'Speed'). " +
                    "Check PrActorUtils.charAnimator - it must point to the player Animator Controller.");
                _actorUtils = null;
                return;
            }

            _pistolLayer   = GetLayerIndex(anim, "PistolLyr");
            _pistolActionsLayer = GetLayerIndex(anim, "PistolActions");
            _rifleLayer    = GetLayerIndex(anim, "RifleActions");
            _meleeLayer    = GetLayerIndex(anim, "MeleeActions");
            _launcherLayer = GetLayerIndex(anim, "LauncherActions"); // optional; -1 if absent
            _upperBodyLayer = GetLayerIndex(anim, "UpperBody");
            _partialActionsLayer = GetLayerIndex(anim, "PartialActions");
            int resolvedDeathLayer = GetLayerIndex(anim, "Death");
            if (resolvedDeathLayer != -1)
                _deathLayerIndex = resolvedDeathLayer;
            _prevAnimatorStateHashes = new int[anim.layerCount];
            ApplyWeaponLayerWeights(anim, immediately: true);

            // Push current WeaponType immediately so the animator starts in the correct state.
            SafeSetInt(anim, WeaponTypeHash, _activeWeaponType);
            BindAnimationEventRelay(anim);

            Debug.Log(
                $"[ANIM_FIX] Bound animator '{anim.runtimeAnimatorController?.name}' " +
                $"legacyVelParams={AnimatorHasHash(anim, VelocityXHash) && AnimatorHasHash(anim, VelocityYHash)} " +
                $"groundedParams={AnimatorHasHash(anim, OnGroundHash) || AnimatorHasHash(anim, IsGroundedHash)} " +
                "weaponTypeMap=0 Unarmed, 1 Handgun, 2 Infantry, 3 Heavy, 4 Knife, 5 Machinegun, 6 RocketLauncher");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Animation,
                "AnimatorBound",
                $"controller={anim.runtimeAnimatorController?.name ?? "null"} layers={anim.layerCount} upperBody={_upperBodyLayer} pistol={_pistolLayer} pistolActions={_pistolActionsLayer} rifle={_rifleLayer} melee={_meleeLayer} launcher={_launcherLayer} partial={_partialActionsLayer} model={modelRoot.name} {DescribeAnimatorLayers(anim)}",
                this);
        }

        private static Animator ResolveModelAnimator(GameObject modelRoot, PrActorUtils actorUtils)
        {
            if (actorUtils.charAnimator != null)
                return actorUtils.charAnimator;

            var rootAnimator = modelRoot.GetComponent<Animator>();
            if (rootAnimator != null)
                return AssignResolvedAnimator(actorUtils, rootAnimator, modelRoot);

            var childAnimators = modelRoot.GetComponentsInChildren<Animator>(true);
            foreach (var candidate in childAnimators)
            {
                if (AnimatorHasHash(candidate, SpeedHash))
                    return AssignResolvedAnimator(actorUtils, candidate, modelRoot);
            }

            return childAnimators.Length > 0
                ? AssignResolvedAnimator(actorUtils, childAnimators[0], modelRoot)
                : null;
        }

        private static Animator AssignResolvedAnimator(PrActorUtils actorUtils, Animator animator, GameObject modelRoot)
        {
            actorUtils.charAnimator = animator;
            animator.applyRootMotion = actorUtils.useRootMotion;
            Debug.LogWarning(
                $"[CharacterAnimationController] PrActorUtils.charAnimator was not assigned on '{modelRoot.name}'. " +
                $"Auto-bound Animator '{animator.runtimeAnimatorController?.name}'.");
            return animator;
        }

        private void BindAnimationEventRelay(Animator anim)
        {
            if (anim == null) return;

            var relay = anim.GetComponent<CharacterAnimEventRelay>();
            if (relay == null)
                relay = anim.gameObject.AddComponent<CharacterAnimEventRelay>();

            relay.controller = this;
        }

        private static int GetLayerIndex(Animator anim, string name)
        {
            for (int i = 0; i < anim.layerCount; i++)
                if (anim.GetLayerName(i) == name) return i;
            return -1;
        }

        private static bool AnimatorHasHash(Animator anim, int hash, AnimatorControllerParameterType? type = null)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash && (!type.HasValue || p.type == type.Value)) return true;
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

            bool physicsGrounded = Physics.CheckSphere(
                transform.position + Vector3.up * _groundCheckOffset,
                _groundCheckRadius, _groundLayers, QueryTriggerInteraction.Ignore);

            bool movementGroundedReliable = _movement != null && (_movement.IsOwner || _movement.IsServerStarted);
            bool grounded = movementGroundedReliable ? (_movement.IsGroundedPublic || physicsGrounded) : physicsGrounded;

            if (grounded && Mathf.Abs(vVel) < 0.05f)
                vVel = 0f;

            // Movement params.
            SafeSetFloat(anim, SpeedHash, _smoothedSpeed);
            SafeSetFloat(anim, XHash, localVel.x);
            SafeSetFloat(anim, YHash, localVel.z);
            SafeSetFloat(anim, VelocityXHash, localVel.x);
            SafeSetFloat(anim, VelocityYHash, localVel.z);
            SafeSetBool (anim, OnGroundHash, grounded);
            SafeSetBool (anim, IsGroundedHash, grounded);
            SafeSetFloat(anim, VerticalHash, vVel);

            // Stance params — read from movement controller each frame.
            SafeSetBool(anim, CrouchHash, _movement?.IsCrouching() ?? false);
            SafeSetBool(anim, ProneHash, false);
            SafeSetBool(anim, GuardHash, false);
            SafeSetBool(anim, SprintHash, _movement?.IsSprinting() ?? false);
            SafeSetBool(anim, OnLadderHash, false);

            // Weapon / combat params.
            SafeSetBool(anim, ArmedHash, _activeSlot.HasValue);
            bool aimingHeld = _isFiring || (_weaponSystem != null && _weaponSystem.IsFireInputHeld);
            SafeSetBool(anim, AimingHash, aimingHeld);
            SafeSetBool(anim, ReloadingHash, _isReloading);
            SafeSetBool(anim, ShootLoopHash, _activeWeaponClass == WeaponClass.MachineGun && aimingHeld);
            SafeSetBool(anim, ShootBoltHash, _activeWeaponClass == WeaponClass.Sniper);
            SafeSetBool(anim, ShootShotgunHash, _activeWeaponClass == WeaponClass.Shotgun);

            // Smooth weapon layer weights.
            ApplyWeaponLayerWeights(anim, immediately: false);

            _prevPosition = transform.position;
        }

        // ── Weapon event handlers ──────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!WeaponAnimDebugEnabled())
                return;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled)
                return;

            if (_prevAnimatorStateHashes == null || _prevAnimatorStateHashes.Length != anim.layerCount)
                _prevAnimatorStateHashes = new int[anim.layerCount];

            for (int i = 0; i < anim.layerCount; i++)
            {
                var info = anim.GetCurrentAnimatorStateInfo(i);
                if (info.fullPathHash == _prevAnimatorStateHashes[i])
                    continue;

                var clips = anim.GetCurrentAnimatorClipInfo(i);
                string clipName = clips.Length > 0 && clips[0].clip != null
                    ? clips[0].clip.name
                    : "(no clip)";

                int animatorWeaponType = AnimatorHasHash(anim, WeaponTypeHash) ? anim.GetInteger(WeaponTypeHash) : -1;
                Debug.Log($"[ANIM_FLOW] State L{i} {anim.GetLayerName(i)} fullPath=#{info.fullPathHash:X8} short=#{info.shortNameHash:X8} clip='{clipName}' weaponType={animatorWeaponType} activeType={_activeWeaponType} activeSlot={_activeSlot?.ToString() ?? "None"} armed={_activeSlot.HasValue}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Animation,
                    "AnimatorStateChanged",
                    $"layer={i} layerName={anim.GetLayerName(i)} weight={anim.GetLayerWeight(i):F2} fullPath=#{info.fullPathHash:X8} short=#{info.shortNameHash:X8} clip='{clipName}' normalized={info.normalizedTime:F2} weaponType={animatorWeaponType} activeType={_activeWeaponType} activeSlot={_activeSlot?.ToString() ?? "None"} reloading={_isReloading} firing={_isFiring}",
                    this);
                _prevAnimatorStateHashes[i] = info.fullPathHash;
            }
        }

        /// <summary>
        /// Called when the active weapon slot changes on all clients.
        /// Uses a coroutine to sequence SetInteger → EndOfFrame → triggers,
        /// preventing stale-trigger race conditions (matches SoldierAnimatorTester pattern).
        /// </summary>
        private void OnWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            ItemInstance previousWeapon = oldSlot.HasValue && _weaponSystem != null
                ? _weaponSystem.GetWeapon(oldSlot.Value)
                : null;

            _activeSlot = newSlot;

            int newType = 0;
            _targetPistolWeight = _targetRifleWeight = _targetMeleeWeight = _targetLauncherWeight = 0f;

            if (newSlot.HasValue)
            {
                var inst = _weaponSystem?.GetWeapon(newSlot.Value);
                var def  = inst != null ? ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition : null;
                _activeWeaponClass = def?.WeaponClass ?? WeaponClass.Rifle;
                newType = ResolveAnimatorWeaponType(def);
                Debug.Log($"[ANIM_FLOW] Weapon active changed: slot={newSlot.Value} def={inst?.DefinitionID ?? "null"} class={_activeWeaponClass} weaponType={newType} override={def?.AnimatorWeaponTypeOverride ?? 0}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Animation,
                    "WeaponAnimChanged",
                    $"old={oldSlot?.ToString() ?? "none"} new={newSlot.Value} def={inst?.DefinitionID ?? "null"} class={_activeWeaponClass} weaponType={newType} override={def?.AnimatorWeaponTypeOverride ?? 0}",
                    this);

                // Keep layer weights in sync for controllers that use them.
                switch (_activeWeaponClass)
                {
                    case WeaponClass.Melee:   _targetMeleeWeight  = 1f; break;
                    case WeaponClass.Pistol:
                    case WeaponClass.SMG:     _targetPistolWeight = 1f; break;
                    case WeaponClass.Launcher when _launcherLayer >= 0:
                        _targetLauncherWeight = 1f; break;
                    default:                  _targetRifleWeight  = 1f; break;
                }
            }

            // Cancel any in-progress switch before starting a new one.
            if (_isSwitching && _weaponSwitchCoroutine != null)
            {
                StopCoroutine(_weaponSwitchCoroutine);
                _isSwitching = false;
            }
            else if (_weaponSwitchCoroutine != null)
            {
                StopCoroutine(_weaponSwitchCoroutine);
            }

            // When swapping between two armed states: play a short holster first, then equip.
            // When drawing from unarmed: skip holster, go straight to equip.
            // When holstering: full holster coroutine.
            bool wasArmed = _activeWeaponType != 0;
            _weaponSwitchCoroutine = (newSlot.HasValue && wasArmed)
                ? StartCoroutine(WeaponSwapCoroutine(newType, previousWeapon))
                : newSlot.HasValue
                    ? StartCoroutine(WeaponEquipCoroutine(newType))
                    : StartCoroutine(WeaponHolsterCoroutine(previousWeapon));
        }

        private void OnWeaponEquipped(WeaponSlotType slot, ItemInstance _)
        {
            if (_activeSlot.HasValue && _activeSlot.Value == slot)
                OnWeaponChanged(slot, slot);
        }

        private void OnWeaponUnequipped(WeaponSlotType slot, ItemInstance _)
        {
            if (_activeSlot.HasValue && _activeSlot.Value == slot)
                OnWeaponChanged(slot, null);
        }

        /// <summary>
        /// Equip coroutine: SetInt -> WeaponChanged(x3) -> Draw -> EndOfFrame.
        /// Keeping Draw in the same frame as the weapon type swap avoids the
        /// visible "idle first, draw later" flash on weapon select.
        /// </summary>
        private IEnumerator WeaponEquipCoroutine(int newType)
        {
            _isSwitching      = true;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            // Gate by IsUpperBodyReady state-machine flags
            string readyReason;
            while (!IsUpperBodyReadyForWeaponTriggers(anim, _activeWeaponType, out readyReason))
            {
                yield return null;
                anim = _actorUtils?.charAnimator;
                if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }
            }

            _activeWeaponType = newType;

            ClearWeaponTriggers(anim);
            SafeSetInt(anim, WeaponTypeHash, newType);

            bool setChanged = SafeSetTrigger(anim, WpnChangedHash);
            bool setChangedDeath = SafeSetTrigger(anim, WpnChangedDeathHash);
            LogAnimEvent("WeaponEquipChangeTriggers", anim, $"weaponType={newType} triggers=WeaponChanged,WeaponChangedDeath setChanged={setChanged} setChangedDeath={setChangedDeath}");

            PrimeWeaponActionParams(anim);
            bool setDraw = SafeSetTrigger(anim, DrawHash);
            LogAnimEvent("WeaponEquipDrawTrigger", anim, $"weaponType={newType} trigger=Draw setDraw={setDraw} immediate=True");
            ScheduleAnimPostFrameLog("WeaponEquipTriggersPost", $"weaponType={newType} trigger=Draw setDraw={setDraw}");

            yield return new WaitForEndOfFrame();

            anim = _actorUtils?.charAnimator; // re-fetch after yield (model may have swapped)
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            yield return WaitForUpperBodyWeaponReady(newType, "WeaponEquipUpperBodyReady", 24);

            _isSwitching = false;
            if (_isReloading)
                ScheduleReloadVisualAfterSwitch();
        }

        /// <summary>
        /// Swap coroutine: used when switching between two armed weapon slots.
        /// Plays a short holster, then applies the new weapon type and Draw in the same frame.
        /// Faster than WeaponHolsterCoroutine by scaling the weapon DrawSpeed stat.
        /// </summary>
        private IEnumerator WeaponSwapCoroutine(int newType, ItemInstance previousWeapon)
        {
            _isSwitching = true;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            // Gate by IsUpperBodyReady state-machine flags
            string readyReason;
            while (!IsUpperBodyReadyForWeaponTriggers(anim, _activeWeaponType, out readyReason))
            {
                yield return null;
                anim = _actorUtils?.charAnimator;
                if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }
            }

            // Short holster for the current weapon.
            ClearWeaponTriggers(anim);
            bool setChangedUbStart = SafeSetTrigger(anim, WpnChangedUBHash);
            bool setHolster = SafeSetTrigger(anim, HolsterHash);
            LogAnimEvent("WeaponSwapHolsterTrigger", anim, $"fromType={_activeWeaponType} toType={newType} trigger=Holster setHolster={setHolster} setChangedUB={setChangedUbStart}");

            float holsterDelay = ResolveWeaponTransitionSeconds(previousWeapon, _swapHolsterDelayScale);
            LogAnimEvent("WeaponSwapHolsterDelay", anim, $"delay={holsterDelay:F2} sourceDraw={DescribeWeaponDrawTiming(previousWeapon)} scale={_swapHolsterDelayScale:F2}");
            yield return new WaitForSeconds(holsterDelay);

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            _activeWeaponType = newType;
            SafeSetInt(anim, WeaponTypeHash, newType);

            bool setChanged = SafeSetTrigger(anim, WpnChangedHash);
            bool setChangedDeath = SafeSetTrigger(anim, WpnChangedDeathHash);
            LogAnimEvent("WeaponSwapChangeTriggers", anim, $"weaponType={newType} triggers=WeaponChanged,WeaponChangedDeath setChanged={setChanged} setChangedDeath={setChangedDeath}");

            PrimeWeaponActionParams(anim);
            bool setDraw = SafeSetTrigger(anim, DrawHash);
            LogAnimEvent("WeaponSwapDrawTrigger", anim, $"weaponType={newType} trigger=Draw setDraw={setDraw} immediate=True");
            ScheduleAnimPostFrameLog("WeaponSwapDrawTriggersPost", $"weaponType={newType} trigger=Draw setDraw={setDraw}");

            yield return new WaitForEndOfFrame();

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            yield return WaitForUpperBodyWeaponReady(newType, "WeaponSwapUpperBodyReady", 24);

            _isSwitching = false;
            if (_isReloading)
                ScheduleReloadVisualAfterSwitch();
        }

        /// <summary>
        /// Holster coroutine: Holster trigger -> DrawSpeed seconds -> SetInt(0) -> EndOfFrame -> WeaponChanged(x3).
        /// </summary>
        private IEnumerator WeaponHolsterCoroutine(ItemInstance previousWeapon)
        {
            _isSwitching = true;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            // Gate by IsUpperBodyReady state-machine flags
            string readyReason;
            while (!IsUpperBodyReadyForWeaponTriggers(anim, _activeWeaponType, out readyReason))
            {
                yield return null;
                anim = _actorUtils?.charAnimator;
                if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }
            }

            ClearWeaponTriggers(anim);

            if (_activeWeaponType != 0)
            {
                bool setChangedUbStart = SafeSetTrigger(anim, WpnChangedUBHash);
                bool setHolster = SafeSetTrigger(anim, HolsterHash);
                LogAnimEvent("WeaponHolsterTrigger", anim, $"activeType={_activeWeaponType} trigger=Holster setHolster={setHolster} setChangedUB={setChangedUbStart}");
            }

            float holsterDelay = ResolveWeaponTransitionSeconds(previousWeapon, 1f);
            LogAnimEvent("WeaponHolsterDelay", anim, $"delay={holsterDelay:F2} sourceDraw={DescribeWeaponDrawTiming(previousWeapon)}");
            yield return new WaitForSeconds(holsterDelay);

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            _activeWeaponType = 0;
            SafeSetInt(anim, WeaponTypeHash, 0);

            yield return new WaitForEndOfFrame();

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            bool setChanged = SafeSetTrigger(anim, WpnChangedHash);
            bool setChangedDeath = SafeSetTrigger(anim, WpnChangedDeathHash);
            LogAnimEvent("WeaponHolsterDoneTriggers", anim, $"weaponType=0 triggers=WeaponChanged,WeaponChangedDeath setChanged={setChanged} setChangedDeath={setChangedDeath}");
            // Unarmed — no Draw trigger.

            _isSwitching = false;
        }

        private float ResolveWeaponTransitionSeconds(ItemInstance weapon, float scale)
        {
            float seconds = weapon != null
                ? weapon.GetComputedStat(ItemStatType.DrawSpeed, _defaultWeaponDrawSeconds)
                : _defaultWeaponDrawSeconds;

            if (seconds <= 0f)
                seconds = _defaultWeaponDrawSeconds;

            seconds *= Mathf.Max(0.01f, scale);
            return Mathf.Clamp(seconds, _minWeaponTransitionSeconds, _maxWeaponTransitionSeconds);
        }

        private string DescribeWeaponDrawTiming(ItemInstance weapon)
        {
            if (weapon == null)
                return $"none fallback={_defaultWeaponDrawSeconds:F2}";

            float seconds = weapon.GetComputedStat(ItemStatType.DrawSpeed, _defaultWeaponDrawSeconds);
            return $"{weapon.DefinitionID}:{seconds:F2}";
        }

        /// <summary>Reset all weapon triggers to prevent stale-trigger issues on new switch.</summary>
        private void ClearWeaponTriggers(Animator anim)
        {
            SafeResetTrigger(anim, WpnChangedHash);
            SafeResetTrigger(anim, WpnChangedUBHash);
            SafeResetTrigger(anim, WpnChangedDeathHash);
            SafeResetTrigger(anim, DrawHash);
            SafeResetTrigger(anim, HolsterHash);
            SafeResetTrigger(anim, ShootHash);
            SafeResetTrigger(anim, ShootBurstHash);
            SafeResetTrigger(anim, ReloadTrigHash);
            SafeResetTrigger(anim, AttackHash);
            SafeResetTrigger(anim, MeleeAttackHash);
            SafeResetTrigger(anim, InteractHash);
            SafeResetTrigger(anim, ThrowGrenadeHash);
            SafeSetBool(anim, ShootLoopHash, false);
        }

        /// <summary>
        /// Raised on EVERY client (owner via local event, remotes via ObserversRpc in NetworkSync).
        /// </summary>
        private void OnShotFired(WeaponSlotType slot, Vector3 dir)
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                if (_activeWeaponClass == WeaponClass.Melee)
                {
                    int idx = UnityEngine.Random.Range(0, 2);
                    SafeResetTrigger(anim, AttackHash);
                    SafeResetTrigger(anim, MeleeAttackHash);
                    bool setAttackIndex = SafeSetInt(anim, AttackIndexHash, idx);
                    bool setAttack = SafeSetTrigger(anim, AttackHash);
                    bool hasLegacyMeleeAttack = AnimatorHasHash(anim, MeleeAttackHash);
                    bool setLegacyMeleeAttack = false;
                    if (hasLegacyMeleeAttack)
                        setLegacyMeleeAttack = SafeSetTrigger(anim, MeleeAttackHash);
                    if (WeaponAnimDebugEnabled())
                        Debug.Log($"[ANIM_FLOW] OnShotFired melee slot={slot} attackIndex={idx} trigger=Attack setAttack={setAttack} setAttackIndex={setAttackIndex} legacyMeleeAttack={setLegacyMeleeAttack} shootTrigger=false");
                    LogAnimEvent("ShotFiredMelee", anim, $"slot={slot} attackIndex={idx} trigger=Attack setAttack={setAttack} setAttackIndex={setAttackIndex} legacyMeleeAttackParam={hasLegacyMeleeAttack} setLegacyMeleeAttack={setLegacyMeleeAttack}");
                    ScheduleAnimPostFrameLog("ShotFiredMeleePost", $"slot={slot} attackIndex={idx} trigger=Attack setAttack={setAttack}");
                }
                else
                {
                    PrepareRangedShotVisual(anim);
                    SafeSetBool(anim, ShootLoopHash, _activeWeaponClass == WeaponClass.MachineGun);
                    SafeSetBool(anim, ShootBoltHash, _activeWeaponClass == WeaponClass.Sniper);
                    SafeSetBool(anim, ShootShotgunHash, _activeWeaponClass == WeaponClass.Shotgun);
                    SafeResetTrigger(anim, ShootHash);
                    bool setShoot = SafeSetTrigger(anim, ShootHash);
                    if (WeaponAnimDebugEnabled())
                        Debug.Log($"[ANIM_FLOW] OnShotFired ranged slot={slot} class={_activeWeaponClass} shootTrigger={setShoot}");
                    LogAnimEvent("ShotFiredRanged", anim, $"slot={slot} class={_activeWeaponClass} trigger=Shoot setShoot={setShoot} shootLoop={_activeWeaponClass == WeaponClass.MachineGun} shootBolt={_activeWeaponClass == WeaponClass.Sniper} shootShotgun={_activeWeaponClass == WeaponClass.Shotgun}");
                    ScheduleAnimPostFrameLog("ShotFiredRangedPost", $"slot={slot} class={_activeWeaponClass} trigger=Shoot setShoot={setShoot}");
                }
            }

            if (_enableMeleeHitFallback && _activeWeaponClass == WeaponClass.Melee)
            {
                if (_meleeFallbackCoroutine != null)
                    StopCoroutine(_meleeFallbackCoroutine);

                _meleeFallbackCoroutine = StartCoroutine(MeleeHitFallbackCoroutine());
            }

            _isFiring = true;
            CancelInvoke(nameof(ResetFiringFlag));
            Invoke(nameof(ResetFiringFlag), _aimingResetDelay);
        }

        private void ResetFiringFlag()
        {
            _isFiring = false;
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
                SafeSetBool(anim, ShootLoopHash, false);
        }

        /// <summary>
        /// Raised on EVERY client (owner via coroutine, remotes via ObserversRpc in NetworkSync).
        /// Also fires the Reload trigger when reload starts so the Animator plays the reload clip.
        /// </summary>
        private void OnReloadStateChanged(bool reloading)
        {
            _isReloading = reloading;

            if (reloading)
            {
                var anim = _actorUtils?.charAnimator;
                if (anim != null && anim.enabled)
                {
                    PrimeWeaponActionParams(anim);
                    if (!IsAnimatorReadyForWeaponAction(anim, out string reason))
                    {
                        LogAnimEvent("ReloadTriggerDeferred", anim, $"reloading={reloading} reason={reason}");
                        ScheduleReloadVisualAfterSwitch();
                    }
                    else
                    {
                        TriggerReloadVisual(anim, "ReloadTrigger", "reason=immediate");
                    }
                }
            }
            else
            {
                if (_delayedReloadVisualCoroutine != null)
                {
                    StopCoroutine(_delayedReloadVisualCoroutine);
                    _delayedReloadVisualCoroutine = null;
                }

                var anim = _actorUtils?.charAnimator;
                if (anim != null && anim.enabled)
                    FinishReloadVisual(anim, "ReloadStateEnded");
            }
        }

        private void ScheduleReloadVisualAfterSwitch()
        {
            if (_delayedReloadVisualCoroutine != null)
                StopCoroutine(_delayedReloadVisualCoroutine);

            _delayedReloadVisualCoroutine = StartCoroutine(ReloadVisualAfterSwitchCoroutine());
        }

        private IEnumerator WaitForUpperBodyWeaponReady(int weaponType, string eventName, int maxFrames)
        {
            int frames = 0;
            string reason = "not-checked";

            while (frames < maxFrames)
            {
                var anim = _actorUtils?.charAnimator;
                if (anim == null || !anim.enabled)
                {
                    reason = "animator-null-or-disabled";
                    break;
                }

                PrimeWeaponActionParams(anim);
                if (IsUpperBodyReadyForWeaponTriggers(anim, weaponType, out reason))
                    break;

                frames++;
                yield return null;
            }

            var readyAnim = _actorUtils?.charAnimator;
            if (readyAnim != null && readyAnim.enabled)
            {
                PrimeWeaponActionParams(readyAnim);
                bool ready = IsUpperBodyReadyForWeaponTriggers(readyAnim, weaponType, out reason);
                LogAnimEvent(eventName, readyAnim, $"weaponType={weaponType} waitFrames={frames} ready={ready} reason={reason}");
            }
        }

        private IEnumerator ReloadVisualAfterSwitchCoroutine()
        {
            int guard = 0;
            string lastReason = "not-checked";
            while (_isReloading && guard < 90)
            {
                var anim = _actorUtils?.charAnimator;
                if (anim != null && anim.enabled)
                {
                    PrimeWeaponActionParams(anim);
                    if (IsAnimatorReadyForWeaponAction(anim, out lastReason))
                        break;
                }
                else
                {
                    lastReason = "animator-null-or-disabled";
                }

                guard++;
                yield return null;
            }

            var readyAnim = _actorUtils?.charAnimator;
            if (readyAnim != null && readyAnim.enabled)
                PrimeWeaponActionParams(readyAnim);

            bool ready = readyAnim != null
                && readyAnim.enabled
                && IsAnimatorReadyForWeaponAction(readyAnim, out lastReason);

            if (_isReloading && !ready && readyAnim != null && readyAnim.enabled && _activeSlot.HasValue && _activeWeaponType > 0)
            {
                bool setChangedUb = SafeSetTrigger(readyAnim, WpnChangedUBHash);
                LogAnimEvent("ReloadWeaponStateRecovery", readyAnim, $"waitFrames={guard} ready={ready} reason={lastReason} setChangedUB={setChangedUb}");

                int recoveryFrames = 0;
                while (_isReloading && recoveryFrames < 20)
                {
                    yield return null;
                    recoveryFrames++;

                    readyAnim = _actorUtils?.charAnimator;
                    if (readyAnim == null || !readyAnim.enabled)
                    {
                        lastReason = "animator-null-or-disabled";
                        continue;
                    }

                    PrimeWeaponActionParams(readyAnim);
                    if (IsAnimatorReadyForWeaponAction(readyAnim, out lastReason))
                    {
                        ready = true;
                        guard += recoveryFrames;
                        break;
                    }
                }
            }

            _delayedReloadVisualCoroutine = null;
            if (!_isReloading)
                yield break;

            readyAnim = _actorUtils?.charAnimator;
            if (readyAnim == null || !readyAnim.enabled)
                yield break;

            PrimeWeaponActionParams(readyAnim);
            ready = IsAnimatorReadyForWeaponAction(readyAnim, out lastReason);
            TriggerReloadVisual(readyAnim, "ReloadTriggerDeferredFire", $"waitFrames={guard} ready={ready} reason={lastReason}");
        }

        private void TriggerReloadVisual(Animator anim, string eventName, string details)
        {
            PrimeWeaponActionParams(anim);
            SafeResetTrigger(anim, ReloadTrigHash);
            SafeResetTrigger(anim, ShootHash);
            SafeResetTrigger(anim, ShootBurstHash);
            SafeResetTrigger(anim, AttackHash);
            SafeResetTrigger(anim, MeleeAttackHash);
            SafeResetTrigger(anim, InteractHash);
            SafeResetTrigger(anim, ThrowHash);
            SafeResetTrigger(anim, ThrowGrenadeHash);
            SafeResetTrigger(anim, DeployHash);
            bool setReload = SafeSetTrigger(anim, ReloadTrigHash);
            LogAnimEvent(eventName, anim, $"reloading=True trigger=Reload setReload={setReload} {details}");
            ScheduleAnimPostFrameLog($"{eventName}Post", $"reloading=True trigger=Reload setReload={setReload} {details}");
        }

        private void PrepareRangedShotVisual(Animator anim)
        {
            if (anim == null || !anim.enabled || _isReloading)
                return;

            SafeSetBool(anim, ReloadingHash, false);
            SafeResetTrigger(anim, ReloadTrigHash);

            if (!IsAnimatorReadyForWeaponAction(anim, out string reason)
                && reason.StartsWith("upperbody-wrong-state", System.StringComparison.Ordinal))
            {
                TryReturnUpperBodyToWeaponIdle(anim, $"ShotFiredPrep {reason}");
            }
        }

        private void FinishReloadVisual(Animator anim, string reason)
        {
            if (anim == null || !anim.enabled)
                return;

            PrimeWeaponActionParams(anim);
            SafeSetBool(anim, ReloadingHash, false);
            SafeResetTrigger(anim, ReloadTrigHash);
            SafeSetBool(anim, ShootLoopHash, false);
            TryReturnUpperBodyToWeaponIdle(anim, reason);
            LogAnimEvent("ReloadStateEnded", anim, "reloading=False");
        }

        private void TryReturnUpperBodyToWeaponIdle(Animator anim, string reason)
        {
            if (anim == null || !anim.enabled)
                return;

            if (_upperBodyLayer < 0 || _upperBodyLayer >= anim.layerCount)
                return;

            if (_activeWeaponType <= 0 || anim.IsInTransition(_upperBodyLayer))
                return;

            string machineName = GetUpperBodyMachineName(_activeWeaponType);
            if (string.IsNullOrEmpty(machineName))
                return;

            string idleState = $"UpperBody.{machineName}.UB_Empty";
            int idleHash = Animator.StringToHash(idleState);
            if (!anim.HasState(_upperBodyLayer, idleHash))
            {
                LogAnimEvent("ReloadUpperBodyReturnSkipped", anim, $"reason={reason} missingState={idleState}");
                return;
            }

            var info = anim.GetCurrentAnimatorStateInfo(_upperBodyLayer);
            if (info.IsName(idleState))
                return;

            float blend = Mathf.Max(0f, _reloadEndUpperBodyBlendSeconds);
            if (blend <= 0f)
                anim.Play(idleHash, _upperBodyLayer, 0f);
            else
                anim.CrossFadeInFixedTime(idleHash, blend, _upperBodyLayer, 0f);

            LogAnimEvent("ReloadUpperBodyReturn", anim, $"reason={reason} state={idleState} blend={blend:F2}");
        }

        private void PrimeWeaponActionParams(Animator anim)
        {
            if (anim == null || !anim.enabled)
                return;

            SafeSetInt(anim, WeaponTypeHash, _activeWeaponType);
            SafeSetBool(anim, ArmedHash, _activeSlot.HasValue);
            SafeSetBool(anim, ReloadingHash, _isReloading);
            SafeSetBool(anim, ShootLoopHash, _activeWeaponClass == WeaponClass.MachineGun && _isFiring);
            SafeSetBool(anim, ShootBoltHash, _activeWeaponClass == WeaponClass.Sniper);
            SafeSetBool(anim, ShootShotgunHash, _activeWeaponClass == WeaponClass.Shotgun);
        }

        private bool IsAnimatorReadyForWeaponAction(Animator anim, out string reason)
        {
            if (anim == null || !anim.enabled)
            {
                reason = "animator-null-or-disabled";
                return false;
            }

            if (!_activeSlot.HasValue)
            {
                reason = "active-slot-none";
                return false;
            }

            if (_activeWeaponType <= 0)
            {
                reason = $"active-type-unarmed:{_activeWeaponType}";
                return false;
            }

            if (_isSwitching)
            {
                reason = "weapon-switching";
                return false;
            }

            if (TryGetTransitionLayer(anim, out string transitionLayer))
            {
                reason = $"transitioning:{transitionLayer}";
                return false;
            }

            if (AnimatorHasHash(anim, WeaponTypeHash, AnimatorControllerParameterType.Int))
            {
                int animatorWeaponType = anim.GetInteger(WeaponTypeHash);
                if (animatorWeaponType != _activeWeaponType)
                {
                    reason = $"weapon-type-mismatch animator={animatorWeaponType} active={_activeWeaponType}";
                    return false;
                }
            }

            if (!IsUpperBodyReadyForWeaponTriggers(anim, out reason))
                return false;

            reason = "ready";
            return true;
        }

        private bool IsUpperBodyReadyForWeaponTriggers(Animator anim, out string reason)
            => IsUpperBodyReadyForWeaponTriggers(anim, _activeWeaponType, out reason);

        private bool IsUpperBodyReadyForWeaponTriggers(Animator anim, int weaponType, out string reason)
        {
            if (anim == null || !anim.enabled)
            {
                reason = "animator-null-or-disabled";
                return false;
            }

            if (_upperBodyLayer < 0 || _upperBodyLayer >= anim.layerCount)
            {
                reason = "upperbody-layer-missing";
                return true;
            }

            if (anim.IsInTransition(_upperBodyLayer))
            {
                reason = "upperbody-transitioning";
                return false;
            }

            string machineName = GetUpperBodyMachineName(weaponType);
            if (string.IsNullOrEmpty(machineName))
            {
                reason = $"upperbody-no-machine weaponType={weaponType}";
                return false;
            }

            var info = anim.GetCurrentAnimatorStateInfo(_upperBodyLayer);
            string expectedIdlePath = $"UpperBody.{machineName}.UB_Empty";
            if (info.IsName(expectedIdlePath))
            {
                reason = $"upperbody-ready path={expectedIdlePath}";
                return true;
            }

            string clipName = GetCurrentClipName(anim, _upperBodyLayer);
            reason = $"upperbody-wrong-state expected={expectedIdlePath} clip={(string.IsNullOrEmpty(clipName) ? "(no clip)" : clipName)} state=#{info.fullPathHash:X8}";
            return false;
        }

        private static string GetUpperBodyMachineName(int weaponType) => weaponType switch
        {
            0 => "Unarmed_UpperBody",
            1 => "Handgun_UpperBody",
            2 => "Infantry_UpperBody",
            3 => "Heavy_UpperBody",
            4 => "Knife_UpperBody",
            5 => "Machinegun_UpperBody",
            6 => "RocketLauncher_UpperBody",
            _ => string.Empty,
        };

        private static bool TryGetTransitionLayer(Animator anim, out string layerName)
        {
            if (anim != null)
            {
                for (int i = 0; i < anim.layerCount; i++)
                {
                    if (anim.IsInTransition(i))
                    {
                        layerName = anim.GetLayerName(i);
                        return true;
                    }
                }
            }

            layerName = "none";
            return false;
        }

        private static string GetCurrentClipName(Animator anim, int layer)
        {
            if (anim == null || layer < 0 || layer >= anim.layerCount)
                return string.Empty;

            var clips = anim.GetCurrentAnimatorClipInfo(layer);
            return clips.Length > 0 && clips[0].clip != null
                ? clips[0].clip.name
                : string.Empty;
        }

        private static bool IsAnyLayerInTransition(Animator anim)
        {
            if (anim == null)
                return false;

            for (int i = 0; i < anim.layerCount; i++)
            {
                if (anim.IsInTransition(i))
                    return true;
            }

            return false;
        }

        // ── Movement event handlers ────────────────────────────────────────────

        private void OnJump()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                SafeSetTrigger(anim, JumpHash);
                LogAnimEvent("JumpTrigger", anim, "trigger=Jump");
            }
        }

        private void OnRoll()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                SafeSetTrigger(anim, RollHash);
                LogAnimEvent("RollTrigger", anim, "trigger=Roll");
            }
        }

        private void HandleThrowPrepareStarted()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                SafeResetTrigger(anim, ThrowHash);
                SafeResetTrigger(anim, ThrowGrenadeHash);
                bool setThrow = SafeSetTrigger(anim, ThrowHash);
                bool setFallbackThrowGrenade = false;
                if (!setThrow)
                    setFallbackThrowGrenade = SafeSetTrigger(anim, ThrowGrenadeHash);

                string details = $"trigger=Throw setThrow={setThrow} fallbackThrowGrenade={setFallbackThrowGrenade}";
                LogAnimEvent("ThrowPrepareTrigger", anim, details);
                ScheduleAnimPostFrameLog("ThrowPrepareTriggerPost", details);
            }
        }

        private void HandleThrowExecuted()
        {
            TriggerThrowGrenade();
        }

        private void HandleDeployStarted()
        {
            TriggerDeploy();
        }

        private void HandleItemUseStarted(ItemInstance item)
        {
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled)
                return;

            ItemType? type = def != null ? def.Type : null;
            LogAnimEvent("ItemUseStarted", anim, $"item={item?.InstanceID ?? "null"} def={def?.ItemID ?? "null"} type={type?.ToString() ?? "null"}");

            if (type == ItemType.Consumable || type == ItemType.Misc)
                TriggerInteract(1);
        }

        // ── Lifecycle event handlers ───────────────────────────────────────────

        private void HandleDied()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            SafeSetInt(anim, DeathIndexHash, UnityEngine.Random.Range(0, 5));

            if (_deathLayerIndex >= 0 && _deathLayerIndex < anim.layerCount)
                anim.SetLayerWeight(_deathLayerIndex, 1f);

            SafeSetTrigger(anim, DieHash);
            LogAnimEvent("DieTrigger", anim, $"deathLayer={_deathLayerIndex}");
        }

        private void HandleRespawned()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            if (_deathLayerIndex >= 0 && _deathLayerIndex < anim.layerCount)
                anim.SetLayerWeight(_deathLayerIndex, 0f);

            SafeSetTrigger(anim, RespawnHash);
            // Reset stance on respawn so we don't stay crouched from before death.
            SafeSetBool(anim, CrouchHash, false);
            LogAnimEvent("RespawnTrigger", anim, $"deathLayer={_deathLayerIndex}");
        }

        /// <summary>
        /// Call from the damage/combat system when the character takes a hit.
        /// </summary>
        public void TriggerTakeDamage()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                SafeSetTrigger(anim, TakeDamageHash);
                LogAnimEvent("TakeDamageTrigger", anim, "trigger=TakeDamage");
            }
        }

        /// <summary>
        /// Play the Interact animation. <paramref name="interactIndex"/> selects the clip variant
        /// (0 = pickup/use A, 1 = pickup/use B — matches SoldierAnimatorTester InteractIndex).
        /// Called by interaction systems and item-pickup handlers.
        /// </summary>
        public void TriggerInteract(int interactIndex = 0)
        {
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;
            PrimeWeaponActionParams(anim);

            if (_activeWeaponType > 0 && !IsUpperBodyReadyForWeaponTriggers(anim, out string readyReason))
            {
                bool setChangedUb = SafeSetTrigger(anim, WpnChangedUBHash);
                LogAnimEvent("InteractTriggerDeferred", anim, $"interactIndex={interactIndex} reason={readyReason} setChangedUB={setChangedUb}");
                StartCoroutine(TriggerInteractWhenUpperBodyReady(interactIndex));
                return;
            }

            TriggerInteractNow(anim, interactIndex, "InteractTrigger");
        }

        private IEnumerator TriggerInteractWhenUpperBodyReady(int interactIndex)
        {
            yield return WaitForUpperBodyWeaponReady(_activeWeaponType, "InteractUpperBodyReady", 24);

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) yield break;

            PrimeWeaponActionParams(anim);
            TriggerInteractNow(anim, interactIndex, "InteractTriggerDeferredFire");
        }

        private void TriggerInteractNow(Animator anim, int interactIndex, string eventName)
        {
            SafeResetTrigger(anim, InteractHash);
            bool setInteractIndex = SafeSetInt(anim, InteractIndexHash, interactIndex);
            bool setInteract = SafeSetTrigger(anim, InteractHash);
            LogAnimEvent(eventName, anim, $"interactIndex={interactIndex} trigger=Interact setInteract={setInteract} setInteractIndex={setInteractIndex}");
            ScheduleAnimPostFrameLog($"{eventName}Post", $"interactIndex={interactIndex} trigger=Interact setInteract={setInteract}");
        }

        /// <summary>
        /// Play the Deploy animation (placing a deployable item).
        /// Falls back to Interact[1] if the Animator Controller has no "Deploy" trigger.
        /// Called by ItemUseSystem / DeployablePlacementHandler on confirmed placement.
        /// </summary>
        public void TriggerDeploy()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            bool hasDeployParam = false;
            foreach (var p in anim.parameters)
                if (p.nameHash == DeployHash) { hasDeployParam = true; break; }

            if (hasDeployParam)
            {
                SafeResetTrigger(anim, DeployHash);
                bool setDeploy = SafeSetTrigger(anim, DeployHash);
                LogAnimEvent("DeployTrigger", anim, $"trigger=Deploy setDeploy={setDeploy}");
                ScheduleAnimPostFrameLog("DeployTriggerPost", $"trigger=Deploy setDeploy={setDeploy}");
            }
            else
            {
                LogAnimEvent("DeployFallbackInteract", anim, "missing Deploy trigger; fallback=Interact[1]");
                TriggerInteract(1);  // deploy/use is Interact_B
            }
        }

        /// <summary>
        /// Play a melee attack animation with an optional combo-index.
        /// Called by MeleeDamageController / WeaponSystem when the fire button is pressed
        /// with a melee weapon equipped.
        /// </summary>
        public void TriggerMeleeAttack(int attackIndex = -1)
        {
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            int idx = attackIndex >= 0 ? attackIndex : UnityEngine.Random.Range(0, 2);
            SafeResetTrigger(anim, AttackHash);
            SafeResetTrigger(anim, MeleeAttackHash);
            bool setAttackIndex = SafeSetInt(anim, AttackIndexHash, idx);
            bool setAttack = SafeSetTrigger(anim, AttackHash);
            bool hasLegacyMeleeAttack = AnimatorHasHash(anim, MeleeAttackHash);
            bool setLegacyMeleeAttack = false;
            if (hasLegacyMeleeAttack)
                setLegacyMeleeAttack = SafeSetTrigger(anim, MeleeAttackHash);
            LogAnimEvent("MeleeAttackTrigger", anim, $"attackIndex={idx} trigger=Attack setAttack={setAttack} setAttackIndex={setAttackIndex} legacyMeleeAttackParam={hasLegacyMeleeAttack} setLegacyMeleeAttack={setLegacyMeleeAttack}");
            ScheduleAnimPostFrameLog("MeleeAttackTriggerPost", $"attackIndex={idx} trigger=Attack setAttack={setAttack}");
        }

        /// <summary>
        /// Play the ThrowGrenade animation.
        /// Called by ItemUseSystem / ThrowableHandler at the moment the grenade is released.
        /// Note: "Throw" trigger (HandleThrowExecuted) fires at throw-start;
        /// "ThrowGrenade" fires at grenade-release (end of throw arc start).
        /// Add both to your animator if you need a two-phase animation.
        /// </summary>
        public void TriggerThrowGrenade()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;
            SafeResetTrigger(anim, ThrowGrenadeHash);
            bool setThrowGrenade = SafeSetTrigger(anim, ThrowGrenadeHash);
            LogAnimEvent("ThrowGrenadeTrigger", anim, $"trigger=ThrowGrenade setThrowGrenade={setThrowGrenade}");
            ScheduleAnimPostFrameLog("ThrowGrenadeTriggerPost", $"trigger=ThrowGrenade setThrowGrenade={setThrowGrenade}");
        }

        // ── Layer weight helper ────────────────────────────────────────────────


        private void ApplyWeaponLayerWeights(Animator anim, bool immediately)
        {
            float dt = immediately ? 1f : Time.deltaTime * _layerWeightBlendSpeed;
            SetLayerWeight(anim, _pistolLayer,   _targetPistolWeight,   dt);
            SetLayerWeight(anim, _pistolActionsLayer, _targetPistolWeight, dt);
            SetLayerWeight(anim, _rifleLayer,    _targetRifleWeight,    dt);
            SetLayerWeight(anim, _meleeLayer,    _targetMeleeWeight,    dt);
            SetLayerWeight(anim, _launcherLayer, _targetLauncherWeight, dt);
            SetLayerWeight(anim, _upperBodyLayer, 1f, dt);
            SetLayerWeight(anim, _partialActionsLayer, 1f, dt);
        }

        private static void SetLayerWeight(Animator anim, int idx, float target, float dt)
        {
            if (idx < 0) return;
            anim.SetLayerWeight(idx, Mathf.Lerp(anim.GetLayerWeight(idx), target, dt));
        }

        // ── WeaponClass → WeaponType int mapping ───────────────────────────────
        // SoldierAnimatorController states:
        // 0=Unarmed, 1=Handgun, 2=Infantry, 3=Heavy,
        // 4=Knife, 5=Machinegun, 6=RocketLauncher.
        private static int WeaponClassToInt(WeaponClass cls) => cls switch
        {
            WeaponClass.Pistol   => 1,
            WeaponClass.Rifle    => 2,
            WeaponClass.SMG      => 2,
            WeaponClass.Shotgun  => 2,
            WeaponClass.Sniper   => 2,
            WeaponClass.Melee    => 4,
            WeaponClass.MachineGun => 5,
            WeaponClass.Launcher => 6,
            _                    => 2,
        };

        private static int ResolveAnimatorWeaponType(WeaponDefinition def)
        {
            if (def != null && def.AnimatorWeaponTypeOverride > 0)
                return Mathf.Clamp(def.AnimatorWeaponTypeOverride, 1, 6);

            return WeaponClassToInt(def != null ? def.WeaponClass : WeaponClass.Rifle);
        }

        private static bool WeaponAnimDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return (cfg != null && cfg.EnableWeaponDebugLogs)
                || PhaseTestLog.IsEnabled(PhaseTestLogCategory.Animation);
        }

        private void LogAnimEvent(string eventName, Animator anim, string details)
        {
            if (!PhaseTestLog.IsEnabled(PhaseTestLogCategory.Animation, eventName, details))
                return;

            PhaseTestLog.Log(
                PhaseTestLogCategory.Animation,
                eventName,
                $"{details} activeSlot={_activeSlot?.ToString() ?? "None"} activeType={_activeWeaponType} class={_activeWeaponClass} switching={_isSwitching} {DescribeAnimatorParameters(anim)} {DescribeAnimatorLayers(anim)}",
                this);
        }

        private static string DescribeAnimatorParameters(Animator anim)
        {
            if (anim == null)
                return "params=null";

            string weaponType = AnimatorHasHash(anim, WeaponTypeHash, AnimatorControllerParameterType.Int)
                ? anim.GetInteger(WeaponTypeHash).ToString()
                : "missing";
            string attackIndex = AnimatorHasHash(anim, AttackIndexHash, AnimatorControllerParameterType.Int)
                ? anim.GetInteger(AttackIndexHash).ToString()
                : "missing";
            string interactIndex = AnimatorHasHash(anim, InteractIndexHash, AnimatorControllerParameterType.Int)
                ? anim.GetInteger(InteractIndexHash).ToString()
                : "missing";
            string shootLoop = AnimatorHasHash(anim, ShootLoopHash, AnimatorControllerParameterType.Bool)
                ? anim.GetBool(ShootLoopHash).ToString()
                : "missing";
            string shootBolt = AnimatorHasHash(anim, ShootBoltHash, AnimatorControllerParameterType.Bool)
                ? anim.GetBool(ShootBoltHash).ToString()
                : "missing";
            string shootShotgun = AnimatorHasHash(anim, ShootShotgunHash, AnimatorControllerParameterType.Bool)
                ? anim.GetBool(ShootShotgunHash).ToString()
                : "missing";

            return $"params=WeaponType:{weaponType} AttackIndex:{attackIndex} InteractIndex:{interactIndex} ShootLoop:{shootLoop} ShootBolt:{shootBolt} ShootShotgun:{shootShotgun} hasTriggers=Attack:{HasTrigger(anim, AttackHash)} MeleeAttack:{HasTrigger(anim, MeleeAttackHash)} Reload:{HasTrigger(anim, ReloadTrigHash)} Draw:{HasTrigger(anim, DrawHash)} Interact:{HasTrigger(anim, InteractHash)} Shoot:{HasTrigger(anim, ShootHash)} Throw:{HasTrigger(anim, ThrowHash)} ThrowGrenade:{HasTrigger(anim, ThrowGrenadeHash)} Deploy:{HasTrigger(anim, DeployHash)} WpnChanged:{HasTrigger(anim, WpnChangedHash)} WpnChangedUB:{HasTrigger(anim, WpnChangedUBHash)}";
        }

        private static string DescribeAnimatorLayers(Animator anim)
        {
            if (anim == null)
                return "layers=null";

            var sb = new System.Text.StringBuilder("layers=[");
            for (int i = 0; i < anim.layerCount; i++)
            {
                if (i > 0)
                    sb.Append(" | ");

                var info = anim.GetCurrentAnimatorStateInfo(i);
                var clips = anim.GetCurrentAnimatorClipInfo(i);
                string clipName = clips.Length > 0 && clips[0].clip != null
                    ? clips[0].clip.name
                    : "(no clip)";
                sb.Append(i)
                    .Append(':')
                    .Append(anim.GetLayerName(i))
                    .Append(" w=")
                    .Append(anim.GetLayerWeight(i).ToString("F2"))
                    .Append(" state=#")
                    .Append(info.fullPathHash.ToString("X8"))
                    .Append(" clip='")
                    .Append(clipName)
                    .Append('\'')
                    .Append(" norm=")
                    .Append(info.normalizedTime.ToString("F2"));

                if (anim.IsInTransition(i))
                {
                    var next = anim.GetNextAnimatorStateInfo(i);
                    var nextClips = anim.GetNextAnimatorClipInfo(i);
                    string nextClipName = nextClips.Length > 0 && nextClips[0].clip != null
                        ? nextClips[0].clip.name
                        : "(no clip)";
                    sb.Append(" transition=True next=#")
                        .Append(next.fullPathHash.ToString("X8"))
                        .Append(" nextClip='")
                        .Append(nextClipName)
                        .Append('\'')
                        .Append(" nextNorm=")
                        .Append(next.normalizedTime.ToString("F2"));
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static bool HasTrigger(Animator anim, int hash)
        {
            return AnimatorHasHash(anim, hash, AnimatorControllerParameterType.Trigger);
        }

        // ── Animation Event Callbacks ─────────────────────────────────────────
        // Called by Animator clip events. Function names must match exactly.
        // Set these on the appropriate animation clip frames in the Animator window.

        /// <summary>Logs the Animator state one frame after a trigger is set.</summary>
        private void ScheduleAnimPostFrameLog(string eventName, string details)
        {
            if (PhaseTestLog.IsEnabled(PhaseTestLogCategory.Animation, eventName, details))
                StartCoroutine(LogAnimPostFrame(eventName, details));
        }

        private IEnumerator LogAnimPostFrame(string eventName, string details)
        {
            yield return null;
            LogAnimEvent(eventName, _actorUtils?.charAnimator, details);
        }

        /// <summary>Bullet exits barrel; add VFX timing here if WeaponSystem does not cover it.</summary>
        public void OnAnimEventFireBullet() { /* WeaponSystem.OnShotFired covers audio. Add VFX here if needed. */ }

        /// <summary>Called at the frame when the weapon becomes visible (draw animation done).</summary>
        public void OnAnimEventDrawWeapon()
        {
            _weaponModelController?.ShowPendingWeaponModelFromAnimation();
            if (WeaponAnimDebugEnabled())
                Debug.Log($"[ANIM_FLOW] OnAnimEventDrawWeapon slot={_activeSlot?.ToString() ?? "None"} type={_activeWeaponType}");
            LogAnimEvent("AnimEventDrawWeapon", _actorUtils?.charAnimator, "event=OnAnimEventDrawWeapon");
        }

        /// <summary>Called at the frame when the weapon should be hidden (holster done).</summary>
        public void OnAnimEventHolsterComplete()
        {
            _weaponModelController?.CompleteHolsterFromAnimation();
            if (WeaponAnimDebugEnabled())
                Debug.Log($"[ANIM_FLOW] OnAnimEventHolsterComplete slot={_activeSlot?.ToString() ?? "None"} type={_activeWeaponType}");
            LogAnimEvent("AnimEventHolsterComplete", _actorUtils?.charAnimator, "event=OnAnimEventHolsterComplete");
        }

        /// <summary>Magazine ejects (reload-out phase). Play reload-out audio here.</summary>
        public void OnAnimEventReloadOut()
        {
            LogAnimEvent("AnimEventReloadOut", _actorUtils?.charAnimator, "event=OnAnimEventReloadOut");
            // TODO: AudioManager.Instance.Play3D(Library.GetReloadOut(_activeWeaponClass), transform.position, ...);
        }

        /// <summary>New magazine inserted (reload-in phase). Play reload-in audio here.</summary>
        public void OnAnimEventReloadIn()
        {
            LogAnimEvent("AnimEventReloadIn", _actorUtils?.charAnimator, "event=OnAnimEventReloadIn");
            // TODO: AudioManager.Instance.Play3D(Library.GetReloadIn(_activeWeaponClass), transform.position, ...);
        }

        /// <summary>Reload animation fully done (bolt charged / chamber closed).</summary>
        public void OnAnimEventReloadComplete()
        {
            LogAnimEvent("AnimEventReloadComplete", _actorUtils?.charAnimator, "event=OnAnimEventReloadComplete");
            /* Optionally signal WeaponSystem that anim is done. */
        }

        /// <summary>Melee damage window — notify melee system to run hit detection.</summary>
        public void OnAnimEventMeleeHit()
        {
            if (_meleeFallbackCoroutine != null)
            {
                StopCoroutine(_meleeFallbackCoroutine);
                _meleeFallbackCoroutine = null;
            }

            _meleeDamageController?.RequestMeleeHit();
            LogAnimEvent("AnimEventMeleeHit", _actorUtils?.charAnimator, "event=OnAnimEventMeleeHit");
        }

        /// <summary>Death animation fully complete — switch to ragdoll/death state.</summary>
        public void OnAnimEventDeathComplete()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled && _deathLayerIndex >= 0 && _deathLayerIndex < anim.layerCount)
            {
                var stateInfo = anim.GetCurrentAnimatorStateInfo(_deathLayerIndex);
                if (stateInfo.shortNameHash != 0 && !stateInfo.IsName("Death_Empty"))
                {
                    float normalizedTime = stateInfo.normalizedTime;
                    if (normalizedTime < 0.9f)
                    {
                        StartCoroutine(WaitForDeathAnimFinishCoroutine(stateInfo.fullPathHash));
                        return;
                    }
                }
            }

            InvokeDeathComplete();
        }

        private IEnumerator WaitForDeathAnimFinishCoroutine(int stateHash)
        {
            var anim = _actorUtils?.charAnimator;
            while (anim != null && anim.enabled)
            {
                var stateInfo = anim.GetCurrentAnimatorStateInfo(_deathLayerIndex);
                if (stateInfo.fullPathHash != stateHash)
                {
                    // State changed (e.g. interrupted or finished)
                    break;
                }
                if (stateInfo.normalizedTime >= 0.9f)
                {
                    break;
                }
                yield return null;
                anim = _actorUtils?.charAnimator;
            }
            InvokeDeathComplete();
        }

        private void InvokeDeathComplete()
        {
            LogAnimEvent("AnimEventDeathComplete", _actorUtils?.charAnimator, "event=OnAnimEventDeathComplete");
            OnDeathAnimationComplete?.Invoke();
        }

        private IEnumerator MeleeHitFallbackCoroutine()
        {
            if (_meleeHitFallbackDelay > 0f)
                yield return new WaitForSeconds(_meleeHitFallbackDelay);

            _meleeFallbackCoroutine = null;
            _meleeDamageController?.RequestMeleeHit();
        }

        // ── Safe Animator helpers ────────────────────────────────────────────────
        // Skip silently when an animator controller doesn't have a given param yet.
        // Prevents log spam when using legacy models that lack new params.

        private static bool SafeSetBool(Animator anim, int hash, bool value)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool) { anim.SetBool(hash, value); return true; }

            return false;
        }

        private static bool SafeSetInt(Animator anim, int hash, int value)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Int) { anim.SetInteger(hash, value); return true; }

            return false;
        }

        private static bool SafeSetFloat(Animator anim, int hash, float value)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Float) { anim.SetFloat(hash, value); return true; }

            return false;
        }

        private static bool SafeSetTrigger(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Trigger) { anim.SetTrigger(hash); return true; }

            return false;
        }

        private static bool SafeResetTrigger(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Trigger) { anim.ResetTrigger(hash); return true; }

            return false;
        }
    }

    internal sealed class CharacterAnimEventRelay : MonoBehaviour
    {
        internal CharacterAnimationController controller;

        public void OnAnimEventFireBullet() => controller?.OnAnimEventFireBullet();
        public void OnAnimEventDrawWeapon() => controller?.OnAnimEventDrawWeapon();
        public void OnAnimEventHolsterComplete() => controller?.OnAnimEventHolsterComplete();
        public void OnAnimEventReloadStart() => controller?.OnAnimEventReloadOut();
        public void OnAnimEventReloadInsert() => controller?.OnAnimEventReloadIn();
        public void OnAnimEventReloadEnd() => controller?.OnAnimEventReloadComplete();
        public void OnAnimEventReloadOut() => controller?.OnAnimEventReloadOut();
        public void OnAnimEventReloadIn() => controller?.OnAnimEventReloadIn();
        public void OnAnimEventReloadComplete() => controller?.OnAnimEventReloadComplete();
        public void OnAnimEventMeleeHit() => controller?.OnAnimEventMeleeHit();
        public void OnAnimEventDeathComplete() => controller?.OnAnimEventDeathComplete();
    }
}
