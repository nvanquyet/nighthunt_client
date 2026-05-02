using System.Collections;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Audio;

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
    ///   Float    "Speed"              — horizontal speed m/s
    ///   Float    "X"                  — strafe (right = +)
    ///   Float    "Y"                  — forward (forward = +)
    ///   Bool     "OnGround"           — grounded state
    ///   Float    "VerticalVel"        — vertical velocity
    ///   Bool     "Armed"              — any weapon drawn
    ///   Bool     "Aiming"             — true briefly after each shot
    ///   Bool     "Reloading"          — reload in progress
    ///   Bool     "IsCrouching"        — crouch stance
    ///   Bool     "IsSprinting"        — sprint state
    ///   Int      "WeaponType"         — 0=Unarmed 1=Rifle 2=Pistol/SMG 3=Launcher 4=Melee 5=Shotgun 6=Sniper
    ///   Trigger  "Shoot"
    ///   Trigger  "Jump"
    ///   Trigger  "Roll"
    ///   Trigger  "Throw"
    ///   Trigger  "Draw"               — weapon equip animation
    ///   Trigger  "Holster"            — weapon holster animation
    ///   Trigger  "WeaponChanged"      — base layer weapon-type transition
    ///   Trigger  "WeaponChangedUB"    — upper body layer weapon-type transition
    ///   Trigger  "WeaponChangedDeath" — death layer weapon-type transition
    ///   Trigger  "Reload"             — reload trigger (fires when reload starts)
    ///   Trigger  "TakeDamage"         — hit reaction
    ///   Trigger  "Die"                — death
    ///   Trigger  "Respawn"            — respawn
    ///   Int      "DeathIndex"         — random death animation index (0-4)
    ///
    /// ANIMATOR LAYERS EXPECTED:
    ///   "PistolLyr"    — weight 0→1 when Secondary active     (optional)
    ///   "RifleActions" — weight 0→1 when Primary active       (optional)
    ///   "MeleeActions" — weight 0→1 when Melee active         (optional)
    ///   "Death"        — death overlay; weight 0→1 on die     (index configurable)
    ///
    /// WEAPON SWITCH FLOW:
    ///   Equip:   ClearTriggers → SetInt(WeaponType) → EndOfFrame → WeaponChanged(x3) → Draw
    ///   Holster: ClearTriggers → Holster trigger → 0.4s → SetInt(0) → EndOfFrame → WeaponChanged(x3)
    ///
    /// ANIMATION EVENT CALLBACKS (set in Animator clip events):
    ///   OnAnimEventFireBullet / OnAnimEventDrawWeapon / OnAnimEventHolsterComplete
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
        private static readonly int AttackHash           = Animator.StringToHash("Attack");
        private static readonly int MeleeAttackHash      = Animator.StringToHash("MeleeAttack");
        private static readonly int JumpHash             = Animator.StringToHash("Jump");
        private static readonly int RollHash             = Animator.StringToHash("Roll");
        private static readonly int ThrowHash            = Animator.StringToHash("Throw");
        // ── New params ──────────────────────────────────────────────────────────
        private static readonly int CrouchHash           = Animator.StringToHash("IsCrouching");
        private static readonly int SprintHash           = Animator.StringToHash("IsSprinting");
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

        // ── Animator layer indices (resolved after model binds) ────────────────
        private int _pistolLayer   = -1;
        private int _pistolActionsLayer = -1;
        private int _rifleLayer    = -1;
        private int _meleeLayer    = -1;
        private int _launcherLayer = -1;   // Optional "LauncherActions" layer — fallback to rifle
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
                _itemUseSystem.OnThrowExecuted += HandleThrowExecuted;
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
                _itemUseSystem.OnThrowExecuted -= HandleThrowExecuted;

            if (_healthSystem != null)
                _healthSystem.OnHitReceived -= HandleHitReceived;
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
                _pistolActionsLayer = GetLayerIndex(anim, "PistolActions");
                _rifleLayer    = GetLayerIndex(anim, "RifleActions");
                _meleeLayer    = GetLayerIndex(anim, "MeleeActions");
                _launcherLayer = GetLayerIndex(anim, "LauncherActions"); // optional; -1 if absent
                _partialActionsLayer = GetLayerIndex(anim, "PartialActions");
                _prevAnimatorStateHashes = new int[anim.layerCount];
                ApplyWeaponLayerWeights(anim, immediately: true);

                // Push current WeaponType immediately so the animator starts in the correct state.
                SafeSetInt(anim, WeaponTypeHash, _activeWeaponType);

                Debug.Log(
                    $"[ANIM_FIX] Bound animator '{anim.runtimeAnimatorController?.name}' " +
                    $"legacyVelParams={AnimatorHasHash(anim, VelocityXHash) && AnimatorHasHash(anim, VelocityYHash)} " +
                    $"groundedParams={AnimatorHasHash(anim, OnGroundHash) || AnimatorHasHash(anim, IsGroundedHash)} " +
                    "weaponTypeMap=0 Unarmed, 1 Rifle, 2 Pistol/SMG, 3 Launcher, 4 Melee, 5 Shotgun, 6 Sniper");
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
            SafeSetBool(anim, SprintHash, _movement?.IsSprinting() ?? false);

            // Weapon / combat params.
            SafeSetBool(anim, ArmedHash, _activeSlot.HasValue);
            bool aimingHeld = _isFiring || (_weaponSystem != null && _weaponSystem.IsFireInputHeld);
            SafeSetBool(anim, AimingHash, aimingHeld);
            SafeSetBool(anim, ReloadingHash, _isReloading);

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
                ? StartCoroutine(WeaponSwapCoroutine(newType))
                : newSlot.HasValue
                    ? StartCoroutine(WeaponEquipCoroutine(newType))
                    : StartCoroutine(WeaponHolsterCoroutine());
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
        /// Equip coroutine: SetInt → EndOfFrame → WeaponChanged(x3) → Draw.
        /// SetInteger must happen BEFORE the trigger so the Animator reads the
        /// correct WeaponType when evaluating its transitions.
        /// </summary>
        private IEnumerator WeaponEquipCoroutine(int newType)
        {
            _isSwitching      = true;
            _activeWeaponType = newType;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            ClearWeaponTriggers(anim);
            anim.SetInteger(WeaponTypeHash, newType);

            yield return new WaitForEndOfFrame();

            anim = _actorUtils?.charAnimator; // re-fetch after yield (model may have swapped)
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            SafeSetTrigger(anim, WpnChangedHash);
            SafeSetTrigger(anim, WpnChangedUBHash);
            SafeSetTrigger(anim, WpnChangedDeathHash);
            SafeSetTrigger(anim, DrawHash);

            _isSwitching = false;
        }

        /// <summary>
        /// Swap coroutine: used when switching between two armed weapon slots.
        /// Plays a short holster, then equips the new weapon with WeaponChanged + Draw.
        /// Faster than WeaponHolsterCoroutine (0.2 s vs 0.4 s) to keep swapping snappy.
        /// </summary>
        private IEnumerator WeaponSwapCoroutine(int newType)
        {
            _isSwitching = true;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            // Short holster for the current weapon.
            ClearWeaponTriggers(anim);
            SafeSetTrigger(anim, HolsterHash);

            yield return new WaitForSeconds(0.2f);

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            _activeWeaponType = newType;
            anim.SetInteger(WeaponTypeHash, newType);

            yield return new WaitForEndOfFrame();

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            SafeSetTrigger(anim, WpnChangedHash);
            SafeSetTrigger(anim, WpnChangedUBHash);
            SafeSetTrigger(anim, WpnChangedDeathHash);
            SafeSetTrigger(anim, DrawHash);

            _isSwitching = false;
        }

        /// <summary>
        /// Holster coroutine: Holster trigger → 0.4 s → SetInt(0) → EndOfFrame → WeaponChanged(x3).
        /// </summary>
        private IEnumerator WeaponHolsterCoroutine()
        {
            _isSwitching = true;

            var anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            ClearWeaponTriggers(anim);

            if (_activeWeaponType != 0)
                SafeSetTrigger(anim, HolsterHash);

            yield return new WaitForSeconds(0.4f);

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            _activeWeaponType = 0;
            anim.SetInteger(WeaponTypeHash, 0);

            yield return new WaitForEndOfFrame();

            anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) { _isSwitching = false; yield break; }

            SafeSetTrigger(anim, WpnChangedHash);
            SafeSetTrigger(anim, WpnChangedUBHash);
            SafeSetTrigger(anim, WpnChangedDeathHash);
            // Unarmed — no Draw trigger.

            _isSwitching = false;
        }

        /// <summary>Reset all weapon triggers to prevent stale-trigger issues on new switch.</summary>
        private void ClearWeaponTriggers(Animator anim)
        {
            SafeResetTrigger(anim, WpnChangedHash);
            SafeResetTrigger(anim, WpnChangedUBHash);
            SafeResetTrigger(anim, WpnChangedDeathHash);
            SafeResetTrigger(anim, DrawHash);
            SafeResetTrigger(anim, HolsterHash);
        }

        /// <summary>
        /// Raised on EVERY client (owner via local event, remotes via ObserversRpc in NetworkSync).
        /// </summary>
        private void OnShotFired(WeaponSlotType slot, Vector3 dir)
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                SafeSetTrigger(anim, ShootHash);
                if (_activeWeaponClass == WeaponClass.Melee)
                {
                    SafeSetTrigger(anim, AttackHash);
                    SafeSetTrigger(anim, MeleeAttackHash);
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

        private void ResetFiringFlag() => _isFiring = false;

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
                    SafeSetTrigger(anim, ReloadTrigHash);
            }
        }

        // ── Movement event handlers ────────────────────────────────────────────

        private void OnJump()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled) SafeSetTrigger(anim, JumpHash);
        }

        private void OnRoll()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled) SafeSetTrigger(anim, RollHash);
        }

        private void HandleThrowExecuted()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled) SafeSetTrigger(anim, ThrowHash);
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
        }

        /// <summary>
        /// Call from the damage/combat system when the character takes a hit.
        /// </summary>
        public void TriggerTakeDamage()
        {
            var anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
                SafeSetTrigger(anim, TakeDamageHash);
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
            SetLayerWeight(anim, _partialActionsLayer, _activeSlot.HasValue ? 1f : 0f, dt);
        }

        private static void SetLayerWeight(Animator anim, int idx, float target, float dt)
        {
            if (idx < 0) return;
            anim.SetLayerWeight(idx, Mathf.Lerp(anim.GetLayerWeight(idx), target, dt));
        }

        // ── WeaponClass → WeaponType int mapping ───────────────────────────────
        // SoldierAnimatorController states:
        // Animator contract:
        // 0=Unarmed 1=Rifle 2=Pistol/SMG 3=Launcher 4=Melee 5=Shotgun 6=Sniper.
        private static int WeaponClassToInt(WeaponClass cls) => cls switch
        {
            WeaponClass.Rifle    => 1,
            WeaponClass.Pistol   => 2,
            WeaponClass.SMG      => 2,
            WeaponClass.Launcher => 3,
            WeaponClass.Melee    => 4,
            WeaponClass.Shotgun  => 5,
            WeaponClass.Sniper   => 6,
            _                    => 1,
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
            return cfg != null && cfg.EnableWeaponDebugLogs;
        }

        // ── Animation Event Callbacks ─────────────────────────────────────────
        // Called by Animator clip events. Function names must match exactly.
        // Set these on the appropriate animation clip frames in the Animator window.

        /// <summary>Bullet exits barrel — add VFX timing here if WeaponSystem doesn't cover it.</summary>
        public void OnAnimEventFireBullet() { /* WeaponSystem.OnShotFired covers audio. Add VFX here if needed. */ }

        /// <summary>Called at the frame when the weapon becomes visible (draw animation done).</summary>
        public void OnAnimEventDrawWeapon()
        {
            _weaponModelController?.ShowPendingWeaponModelFromAnimation();
            if (WeaponAnimDebugEnabled())
                Debug.Log($"[ANIM_FLOW] OnAnimEventDrawWeapon slot={_activeSlot?.ToString() ?? "None"} type={_activeWeaponType}");
        }

        /// <summary>Called at the frame when the weapon should be hidden (holster done).</summary>
        public void OnAnimEventHolsterComplete()
        {
            _weaponModelController?.CompleteHolsterFromAnimation();
            if (WeaponAnimDebugEnabled())
                Debug.Log($"[ANIM_FLOW] OnAnimEventHolsterComplete slot={_activeSlot?.ToString() ?? "None"} type={_activeWeaponType}");
        }

        /// <summary>Magazine ejects (reload-out phase). Play reload-out audio here.</summary>
        public void OnAnimEventReloadOut()
        {
            // TODO: AudioManager.Instance.Play3D(Library.GetReloadOut(_activeWeaponClass), transform.position, ...);
        }

        /// <summary>New magazine inserted (reload-in phase). Play reload-in audio here.</summary>
        public void OnAnimEventReloadIn()
        {
            // TODO: AudioManager.Instance.Play3D(Library.GetReloadIn(_activeWeaponClass), transform.position, ...);
        }

        /// <summary>Reload animation fully done (bolt charged / chamber closed).</summary>
        public void OnAnimEventReloadComplete() { /* Optionally signal WeaponSystem that anim is done. */ }

        /// <summary>Melee damage window — notify melee system to run hit detection.</summary>
        public void OnAnimEventMeleeHit()
        {
            if (_meleeFallbackCoroutine != null)
            {
                StopCoroutine(_meleeFallbackCoroutine);
                _meleeFallbackCoroutine = null;
            }

            _meleeDamageController?.RequestMeleeHit();
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

        private static void SafeSetBool(Animator anim, int hash, bool value)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash) { anim.SetBool(hash, value); return; }
        }

        private static void SafeSetInt(Animator anim, int hash, int value)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash) { anim.SetInteger(hash, value); return; }
        }

        private static void SafeSetFloat(Animator anim, int hash, float value)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash) { anim.SetFloat(hash, value); return; }
        }

        private static void SafeSetTrigger(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash) { anim.SetTrigger(hash); return; }
        }

        private static void SafeResetTrigger(Animator anim, int hash)
        {
            foreach (var p in anim.parameters)
                if (p.nameHash == hash) { anim.ResetTrigger(hash); return; }
        }
    }
}
