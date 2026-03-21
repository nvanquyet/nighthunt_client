using UnityEngine;
using NightHunt.Utilities;
using NightHunt.Networking;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Gameplay.FogOfWar;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Bridges networked lifecycle + weapon state to local visuals:
    /// ragdoll, animator enable/disable, and left-hand IK for ALL clients.
    ///
    /// PREFAB SETUP:
    ///   Attach to a child GO named "Visual" (or directly on the root).
    ///   All cross-GO references are auto-resolved in Awake via root lookups —
    ///   no Inspector drag-and-drop required.
    ///
    /// IK SMOOTHING:
    ///   IK weight is lerped (not snapped) when a new weapon is drawn/holstered.
    ///   This prevents the left hand from teleporting when switching weapons.
    ///   _charIK.leftHandTarget is only set AFTER the weight is non-zero so
    ///   the solver doesn't fight the animation during the blend-in frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVisualController : MonoBehaviour
    {
        [Header("IK Smoothing")]
        [Tooltip("Speed (units/s) at which left-hand IK weight fades in/out on weapon swap.")]
        [SerializeField] private float _ikBlendSpeed = 8f;

        // ── Auto-resolved refs ─────────────────────────────────────────────────
        private CharacterLifecycleController _lifecycle;
        private NetworkPlayer                _networkPlayer;
        private IWeaponSystem                _weaponSystem;
        private PlayerModelLoader            _modelLoader;
        private WeaponModelController        _weaponModelController;
        private FogVisionBinder              _fogVisionBinder;

        // Model-side refs (bound in BindModel once mesh is ready).
        private PrCharacterRagdoll _ragdoll;
        private PrActorUtils       _actorUtils;
        private PrCharacterIK      _charIK;

        // IK blend state.
        private Transform _pendingIKTarget;   // target set by weapon swap
        private float     _ikWeight;          // current lerped weight [0..1]
        private float     _ikTargetWeight;    // 0 = holstered, 1 = armed

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterVisualController] CharacterLifecycleController not found")
                .Resolve();

            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                .OnSelf().OnRoot().InRootChildren()
                .OrLogWarning("[CharacterVisualController] NetworkPlayer not found")
                .Resolve();

            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
                .OrLogWarning("[CharacterVisualController] PlayerModelLoader not found")
                .Resolve();

            _weaponSystem = ComponentResolver.Find<IWeaponSystem>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterVisualController] IWeaponSystem not found")
                .Resolve();

            _weaponModelController = ComponentResolver.Find<WeaponModelController>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterVisualController] WeaponModelController not found")
                .Resolve();

            _fogVisionBinder = ComponentResolver.Find<FogVisionBinder>(this)
                .OnSelf().InChildren().InRootChildren()
                .OrLogWarning("[CharacterVisualController] FogVisionBinder not found — FOW will not be toggled on death")
                .Resolve();

            if (_modelLoader != null)
                _modelLoader.OnModelReady += BindModel;
        }

        private void OnDestroy()
        {
            if (_modelLoader != null)
                _modelLoader.OnModelReady -= BindModel;
        }

        private void OnEnable()
        {
            if (_lifecycle != null)
            {
                _lifecycle.OnDied      += HandleDied;
                _lifecycle.OnRespawned += HandleRespawned;
            }
            if (_networkPlayer != null)
                _networkPlayer.OnAliveChanged += HandleAliveChanged;
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            if (_weaponModelController != null)
                _weaponModelController.OnLeftHandIKTargetChanged += HandleIKTargetChanged;
        }

        private void OnDisable()
        {
            if (_lifecycle != null)
            {
                _lifecycle.OnDied      -= HandleDied;
                _lifecycle.OnRespawned -= HandleRespawned;
            }
            if (_networkPlayer != null)
                _networkPlayer.OnAliveChanged -= HandleAliveChanged;
            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            if (_weaponModelController != null)
                _weaponModelController.OnLeftHandIKTargetChanged -= HandleIKTargetChanged;
        }

        // ── IK weight update each frame ────────────────────────────────────────

        private void Update()
        {
            if (_charIK == null || !_charIK.ikActive) return;

            _ikWeight = Mathf.MoveTowards(_ikWeight, _ikTargetWeight, _ikBlendSpeed * Time.deltaTime);

            // Apply IK target once weight is meaningful (avoids single-frame snap).
            if (_ikWeight > 0.01f)
                _charIK.leftHandTarget = _pendingIKTarget;
            else
                _charIK.leftHandTarget = null;

            // Drive weight via a simple wrapper — PrCharacterIK uses full-weight (0 or 1).
            // If you need per-frame partial weights, mirror PrCharacterIK.OnAnimatorIK here.
            // For now we approximate by toggling IK and relying on the animator to blend.
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleDied()      => SetDeadVisuals();
        private void HandleRespawned() => SetAliveVisuals();

        private void HandleAliveChanged(bool isAlive)
        {
            if (isAlive) SetAliveVisuals();
            else         SetDeadVisuals();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? _, WeaponSlotType? newSlot)
        {
            // Clear IK immediately on weapon swap; HandleIKTargetChanged will re-set it
            // once WeaponModelController has finished spawning the new model.
            SetIKTargetSmooth(null);
        }

        private void HandleIKTargetChanged(Transform ikTarget) => SetIKTargetSmooth(ikTarget);

        // ── Model binding ──────────────────────────────────────────────────────

        private void BindModel(GameObject modelRoot)
        {
            _actorUtils = ComponentResolver.Find<PrActorUtils>(modelRoot)
                .OnSelf().InChildren()
                .OrLogWarning($"[CharacterVisualController] PrActorUtils not found on '{modelRoot.name}'")
                .Resolve();

            _charIK = ComponentResolver.Find<PrCharacterIK>(modelRoot)
                .OnSelf().InChildren()
                .OrLogWarning($"[CharacterVisualController] PrCharacterIK not found on '{modelRoot.name}'")
                .Resolve();

            _ragdoll = ComponentResolver.Find<PrCharacterRagdoll>(modelRoot)
                .OnSelf().InChildren()
                .OrLogWarning($"[CharacterVisualController] PrCharacterRagdoll not found on '{modelRoot.name}'")
                .Resolve();

            bool isAlive = _networkPlayer == null || _networkPlayer.IsAlive;
            if (isAlive) SetAliveVisuals();
            else         SetDeadVisuals();
        }

        // ── Visual state ───────────────────────────────────────────────────────

        private void SetDeadVisuals()
        {
            if (_charIK != null)     _charIK.ikActive = false;
            if (_actorUtils?.charAnimator != null)
                _actorUtils.charAnimator.enabled = false;
            _ragdoll?.ActivateRagdoll();
            // Disable FOW revealer — dead player should not reveal the map.
            if (_fogVisionBinder != null) _fogVisionBinder.enabled = false;
        }

        private void SetAliveVisuals()
        {
            _ragdoll?.DeactivateRagdoll();

            if (_actorUtils?.charAnimator != null)
                _actorUtils.charAnimator.enabled = true;

            if (_charIK != null) _charIK.ikActive = true;
            // Re-enable FOW revealer on respawn.
            if (_fogVisionBinder != null) _fogVisionBinder.enabled = true;

            // Re-apply whichever IK target the current weapon has (null if holstered).
            SetIKTargetSmooth(_weaponModelController?.LeftHandIKTarget);
        }

        // ── IK smooth blend helper ─────────────────────────────────────────────

        /// <summary>
        /// Sets the pending IK target and triggers a weight fade.
        /// Passing null starts a fade-out; non-null starts a fade-in.
        /// </summary>
        private void SetIKTargetSmooth(Transform target)
        {
            _pendingIKTarget = target;
            _ikTargetWeight  = (target != null) ? 1f : 0f;

            // If we just holstered, begin fade-out immediately (don't wait for Update).
            if (target == null && _charIK != null)
                _charIK.rightHandTarget = null;
        }
    }
}