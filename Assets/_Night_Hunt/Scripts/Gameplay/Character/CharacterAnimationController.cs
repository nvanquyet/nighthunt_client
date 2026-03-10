using UnityEngine;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Drives the player Animator each frame from movement state and weapon/combat systems.
    /// Uses transform position-delta as velocity source so it works correctly
    /// for BOTH the owner (client-predicted) and remote clients (position
    /// replicated by FishNet's NetworkTransform).
    ///
    /// EXPECTED ANIMATOR PARAMETERS (create these in your Animator Controller):
    ///   Float  "Speed"       — horizontal speed in m/s
    ///   Float  "X"           — strafe velocity (right = positive, LEFT of model = negative)
    ///   Float  "Y"           — forward velocity (forward = positive, BACKWARD = negative)
    ///   Bool   "OnGround"    — true while grounded
    ///   Float  "VerticalVel" — vertical velocity (positive = ascending)
    ///   Bool   "Armed"       — true when weapon equipped
    ///   Bool   "Aiming"      — true during weapon fire (auto-aim with fire)
    ///   Bool   "Reloading"   — true during reload
    ///   Trigger "Shoot"      — fired on each shot
    ///   Trigger "Jump"       — fired when jump starts
    ///   Trigger "Roll"       — fired when roll starts
    ///
    /// EXPECTED ANIMATOR LAYERS (create these in your Animator Controller):
    ///   Layer "PistolLyr"    — secondary weapon animations (weight 0-1)
    ///   Layer "RifleActions" — primary weapon animations (weight 0-1)
    ///   Layer "MeleeActions" — melee weapon animations (weight 0-1)
    ///
    /// INSPECTOR SETUP:
    ///   Attach to the root player prefab alongside WeaponSystem.
    ///   Also add PlayerModelLoader to the root — it will inject _actorUtils at runtime.
    ///   DO NOT drag PrActorUtils in the Inspector; it lives on the dynamically-spawned
    ///   model child and is bound automatically via PlayerModelLoader.OnModelReady.
    ///
    /// ANIMATION DIRECTION LOGIC (X/Y parameters):
    ///   Uses ACTUAL VELOCITY transformed to character LOCAL SPACE (not input).
    ///   
    ///   WHY USE VELOCITY (not input):
    ///   - TANK mode: character rotates to match movement → local velocity ≈ forward
    ///   - STRAFE mode (camera-locked): character rotates to AIM but moves freely
    ///     → local velocity shows TRUE direction relative to model's facing
    ///   
    ///   EXAMPLES in STRAFE mode:
    ///   - Model faces North, camera faces East, press W (move East) 
    ///     → localVelocity.x > 0 → RIGHT strafe animation ✅
    ///   - Model faces North, camera faces South, press W (move South)
    ///     → localVelocity.z < 0 → BACKWARD animation ✅
    ///   - Model faces East, camera faces North, press W (move North)
    ///     → localVelocity.x < 0 → LEFT strafe animation ✅
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationController : MonoBehaviour
    {
        // NOT a SerializeField — bound at runtime via PlayerModelLoader.OnModelReady.
        private PrActorUtils _actorUtils;
        
        // Game system references (cached in Awake)
        private WeaponSystem _weaponSystem;
        private BaseCharacterPredictedMovement _movementController;

        [Header("Ground Detection")]
        [Tooltip("Sphere radius for ground check cast from the character base.")]
        [SerializeField] private float _groundCheckRadius = 0.25f;
        [SerializeField] private float _groundCheckOffset = 0.05f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("Smoothing")]
        [Tooltip("Speed parameter is lerped each frame to avoid animation snapping.")]
        [SerializeField] private float _speedSmoothing = 12f;
        
        [Header("Weapon Layer Transition")]
        [Tooltip("Speed at which weapon layer weights interpolate on weapon change.")]
        [SerializeField] private float _layerWeightTransitionSpeed = 8f;

        // Cached Animator parameter hashes (avoids string lookup each frame).
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

        // Cached Animator layer indices (resolved in BindModel once Animator is ready).
        private int _pistolLayerIndex    = -1;
        private int _rifleLayerIndex     = -1;
        private int _meleeLayerIndex     = -1;

        private Vector3 _prevPosition;
        private float   _smoothedSpeed;
        private bool    _started;
        
        // Weapon state for animation (read from WeaponSystem events)
        private WeaponSlotType? _activeWeaponSlot;
        private bool _isFiring;
        private bool _isReloading;
        
        // Target layer weights (lerped each frame for smooth transitions)
        private float _targetPistolWeight;
        private float _targetRifleWeight;
        private float _targetMeleeWeight;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            // Cache game systems
            _weaponSystem = GetComponent<WeaponSystem>();
            _movementController = GetComponent<BaseCharacterPredictedMovement>();
            
            // Subscribe to model loader so we get PrActorUtils once the mesh is ready.
            var modelLoader = GetComponent<PlayerModelLoader>();
            if (modelLoader != null)
            {
                modelLoader.OnModelReady += BindModel;
            }
            else
            {
                Debug.LogWarning("[CharacterAnimationController] PlayerModelLoader not found on root. " +
                                 "Animation will not drive until model is bound manually.");
            }
            
            // Subscribe to weapon system events (if present)
            if (_weaponSystem != null)
            {
                _weaponSystem.OnActiveWeaponChanged += OnWeaponChanged;
                _weaponSystem.OnShotFired           += OnShotFired;
                _weaponSystem.OnReloadStateChanged  += OnReloadStateChanged;
            }
            else
            {
                Debug.LogWarning("[CharacterAnimationController] WeaponSystem not found. " +
                                 "Weapon animations will not be driven.");
            }
            
            // Subscribe to movement events (if present)
            if (_movementController != null)
            {
                _movementController.OnJumpTriggered += OnJump;
                _movementController.OnRollTriggered += OnRoll;
            }
            else
            {
                Debug.LogWarning("[CharacterAnimationController] Movement controller not found. " +
                                 "Jump/Roll animations will not be driven.");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from model loader
            var modelLoader = GetComponent<PlayerModelLoader>();
            if (modelLoader != null)
                modelLoader.OnModelReady -= BindModel;
            
            // Unsubscribe from weapon system
            if (_weaponSystem != null)
            {
                _weaponSystem.OnActiveWeaponChanged -= OnWeaponChanged;
                _weaponSystem.OnShotFired           -= OnShotFired;
                _weaponSystem.OnReloadStateChanged  -= OnReloadStateChanged;
            }
            
            // Unsubscribe from movement controller
            if (_movementController != null)
            {
                _movementController.OnJumpTriggered -= OnJump;
                _movementController.OnRollTriggered -= OnRoll;
            }
        }

        private void Start()
        {
            _prevPosition = transform.position;
            _started      = true;
        }

        /// <summary>
        /// Called by PlayerModelLoader.OnModelReady once the character mesh is instantiated.
        /// Extracts PrActorUtils (which holds the Animator reference) from the model root
        /// and resolves animator layer indices for weapon animations.
        /// </summary>
        private void BindModel(GameObject modelRoot)
        {
            _actorUtils = modelRoot.GetComponentInChildren<PrActorUtils>(true);
            if (_actorUtils == null)
            {
                Debug.LogWarning($"[CharacterAnimationController] PrActorUtils not found on model '{modelRoot.name}'. " +
                                 "Animator parameters will not be driven.");
                return;
            }
            
            // Resolve animator layer indices (only once when model spawns)
            Animator anim = _actorUtils.charAnimator;
            if (anim != null)
            {
                _pistolLayerIndex = GetLayerIndex(anim, "PistolLyr");
                _rifleLayerIndex  = GetLayerIndex(anim, "RifleActions");
                _meleeLayerIndex  = GetLayerIndex(anim, "MeleeActions");
                
                // Initialize current weapon state
                UpdateWeaponLayerWeights(anim, immediately: true);
            }
        }
        
        /// <summary>
        /// Helper to safely get animator layer index. Returns -1 if layer not found.
        /// </summary>
        private int GetLayerIndex(Animator anim, string layerName)
        {
            for (int i = 0; i < anim.layerCount; i++)
            {
                if (anim.GetLayerName(i) == layerName)
                    return i;
            }
            return -1; // Layer not found (animator may not have weapon layers yet)
        }

        private void Update()
        {
            if (!_started) return;

            Animator anim = _actorUtils?.charAnimator;
            if (anim == null || !anim.enabled) return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                _prevPosition = transform.position;
                return;
            }

            // -- Velocity from position delta (works for owner + remote) -------
            Vector3 delta  = transform.position - _prevPosition;
            float   hSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / dt;
            float   vVel   = delta.y / dt;

            // Smooth horizontal speed to prevent rapid flicker on minor corrections.
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, hSpeed, _speedSmoothing * dt);
            
            // -- Strafe velocity in local space (for directional blend trees) ---
            // CRITICAL: Transform ACTUAL VELOCITY (not input) to local space.
            // This shows which direction the model is ACTUALLY MOVING relative to its forward.
            //
            // Examples in STRAFE mode (camera-locked):
            // - Model faces North, moving East  → localVelocity.x > 0 (strafe RIGHT animation)
            // - Model faces North, moving South → localVelocity.z < 0 (backward animation)
            // - Model faces East,  moving North → localVelocity.x < 0 (strafe LEFT animation)
            //
            // This works for BOTH modes:
            // - TANK mode: character rotates to match movement → local velocity ≈ (0, 0, forward)
            // - STRAFE mode: character rotates to aim but moves freely → local velocity shows real direction
            Vector3 worldVelocity = delta / dt;
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            // -- Grounded check ------------------------------------------------
            bool grounded = Physics.CheckSphere(
                transform.position + Vector3.up * _groundCheckOffset,
                _groundCheckRadius,
                _groundLayers,
                QueryTriggerInteraction.Ignore);

            // -- Push movement params to Animator ------------------------------
            anim.SetFloat(SpeedHash,    _smoothedSpeed);
            anim.SetFloat(XHash,        localVelocity.x); // Strafe right
            anim.SetFloat(YHash,        localVelocity.z); // Forward (note: Z is forward in Unity)
            anim.SetBool (OnGroundHash, grounded);
            anim.SetFloat(VerticalHash, vVel);
            
            // -- Push weapon/combat state to Animator --------------------------
            bool isArmed = _activeWeaponSlot.HasValue;
            anim.SetBool(ArmedHash,     isArmed);
            anim.SetBool(AimingHash,    _isFiring);      // Auto-aim when firing
            anim.SetBool(ReloadingHash, _isReloading);
            
            // -- Interpolate weapon layer weights ------------------------------
            UpdateWeaponLayerWeights(anim, immediately: false);

            _prevPosition = transform.position;
        }
        
        // ── Weapon System Event Handlers ──────────────────────────────────────
        
        /// <summary>
        /// Called when WeaponSystem changes active weapon slot.
        /// Updates target layer weights for smooth crossfade between weapon animation layers.
        /// </summary>
        private void OnWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            _activeWeaponSlot = newSlot;
            
            // Reset target weights to zero
            _targetPistolWeight = 0f;
            _targetRifleWeight  = 0f;
            _targetMeleeWeight  = 0f;
            
            // Set target weight for active weapon slot
            if (newSlot.HasValue)
            {
                switch (newSlot.Value)
                {
                    case WeaponSlotType.Secondary: // Pistol/sidearm
                        _targetPistolWeight = 1f;
                        break;
                    case WeaponSlotType.Primary:   // Rifle/primary
                        _targetRifleWeight = 1f;
                        break;
                    case WeaponSlotType.Melee:
                        _targetMeleeWeight = 1f;
                        break;
                }
            }
        }
        
        /// <summary>
        /// Called when WeaponSystem fires a shot.
        /// Triggers the "Shoot" animator parameter and sets _isFiring flag for auto-aim.
        /// </summary>
        private void OnShotFired(WeaponSlotType slot, Vector3 aimDirection)
        {
            Animator anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                anim.SetTrigger(ShootHash);
            }
            
            // Set firing flag for auto-aim (will be updated in Update() -> Aiming bool)
            _isFiring = true;
            
            // Reset firing flag after a short delay (avoid staying in aim pose)
            // In a real implementation, you might want to track continuous fire differently
            CancelInvoke(nameof(ResetFiringFlag));
            Invoke(nameof(ResetFiringFlag), 0.2f);
        }
        
        /// <summary>
        /// Resets the firing flag after a short delay. Called via Invoke from OnShotFired.
        /// </summary>
        private void ResetFiringFlag()
        {
            _isFiring = false;
        }
        
        /// <summary>
        /// Called when WeaponSystem starts/stops reloading.
        /// Updates the _isReloading flag which drives the "Reloading" animator bool.
        /// </summary>
        private void OnReloadStateChanged(bool isReloading)
        {
            _isReloading = isReloading;
        }
        
        // ── Movement System Event Handlers ────────────────────────────────────
        
        /// <summary>
        /// Called when movement system triggers a jump.
        /// Fires the "Jump" animator trigger.
        /// </summary>
        private void OnJump()
        {
            Animator anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                anim.SetTrigger(JumpHash);
            }
        }
        
        /// <summary>
        /// Called when movement system triggers a roll.
        /// Fires the "Roll" animator trigger.
        /// </summary>
        private void OnRoll()
        {
            Animator anim = _actorUtils?.charAnimator;
            if (anim != null && anim.enabled)
            {
                anim.SetTrigger(RollHash);
            }
        }
        
        // ── Helper Methods ────────────────────────────────────────────────────
        
        /// <summary>
        /// Updates animator layer weights for weapon animations.
        /// Interpolates smoothly between current and target weights unless <paramref name="immediately"/> is true.
        /// </summary>
        private void UpdateWeaponLayerWeights(Animator anim, bool immediately)
        {
            if (anim == null) return;
            
            float dt = immediately ? 1f : Time.deltaTime * _layerWeightTransitionSpeed;
            
            // Interpolate layer weights
            if (_pistolLayerIndex >= 0)
            {
                float current = anim.GetLayerWeight(_pistolLayerIndex);
                float target  = _targetPistolWeight;
                anim.SetLayerWeight(_pistolLayerIndex, Mathf.Lerp(current, target, dt));
            }
            
            if (_rifleLayerIndex >= 0)
            {
                float current = anim.GetLayerWeight(_rifleLayerIndex);
                float target  = _targetRifleWeight;
                anim.SetLayerWeight(_rifleLayerIndex, Mathf.Lerp(current, target, dt));
            }
            
            if (_meleeLayerIndex >= 0)
            {
                float current = anim.GetLayerWeight(_meleeLayerIndex);
                float target  = _targetMeleeWeight;
                anim.SetLayerWeight(_meleeLayerIndex, Mathf.Lerp(current, target, dt));
            }
        }
    }
}
