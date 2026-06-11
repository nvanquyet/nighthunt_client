using System.Collections;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Gameplay.FogOfWar;
using NightHunt.Diagnostics;

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

        // [Header("Death Visuals")]
        // [Tooltip("Time to let the Animator play Die before switching to ragdoll. 0 = ragdoll immediately.")]
        // [SerializeField, Min(0f)] private float _deathAnimationToRagdollDelay = 1.2f;

        // ── Auto-resolved refs ─────────────────────────────────────────────────
        private CharacterLifecycleController _lifecycle;
        private NetworkPlayer                _networkPlayer;
        private IWeaponSystem                _weaponSystem;
        private PlayerModelLoader            _modelLoader;
        private WeaponModelController        _weaponModelController;
        private FogVisionBinder              _fogVisionBinder;
        private CharacterAnimationController _animationController;

        // Model-side refs (bound in BindModel once mesh is ready).
        private PrCharacterRagdoll _ragdoll;
        private PrActorUtils       _actorUtils;
        private PrCharacterIK      _charIK;
        private GameObject         _modelRoot;

        // IK blend state.
        private Transform _pendingIKTarget;   // target set by weapon swap
        private float     _ikWeight;          // current lerped weight [0..1]
        private float     _ikTargetWeight;    // 0 = holstered, 1 = armed
        private Coroutine _deathVisualCoroutine;

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

            _animationController = ComponentResolver.Find<CharacterAnimationController>(this)
                .OnSelf().InChildren().InParent().InRootChildren()
                .OrDefault(null)
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
            if (_animationController != null)
                _animationController.OnDeathAnimationComplete += HandleDeathAnimationComplete;
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
            if (_animationController != null)
                _animationController.OnDeathAnimationComplete -= HandleDeathAnimationComplete;
        }

        // ── IK weight update each frame ────────────────────────────────────────

        private void Update()
        {
            if (_charIK == null || !_charIK.ikActive) return;

            _ikWeight = Mathf.MoveTowards(_ikWeight, _ikTargetWeight, _ikBlendSpeed * Time.deltaTime);
            _charIK.leftHandWeight = _ikWeight;

            // Apply IK target once weight is meaningful (avoids single-frame snap).
            if (_ikWeight > 0.01f)
                _charIK.leftHandTarget = _pendingIKTarget;
            else
                _charIK.leftHandTarget = null;
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
            _modelRoot = modelRoot;

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

            InitializeRagdollSafely();

            bool isAlive = _networkPlayer == null || _networkPlayer.IsAlive;
            if (isAlive) SetAliveVisuals();
            else         SetDeadVisuals();

            if (isAlive && isActiveAndEnabled)
                StartCoroutine(ReapplyAliveVisualsAfterRagdollStart());

            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "VisualModelBound",
                $"player={_networkPlayer?.DisplayName ?? "null"} model={modelRoot.name} charIK={(_charIK != null ? _charIK.name : "null")} ragdoll={(_ragdoll != null ? _ragdoll.name : "null")} animator={(_actorUtils?.charAnimator != null ? _actorUtils.charAnimator.name : "null")} alive={isAlive}",
                this);
        }

        // ── Visual state ───────────────────────────────────────────────────────

        private void SetDeadVisuals()
        {
            if (_deathVisualCoroutine != null)
            {
                StopCoroutine(_deathVisualCoroutine);
                _deathVisualCoroutine = null;
            }

            if (_charIK != null)     _charIK.ikActive = false;
            // Disable FOW revealer — dead player should not reveal the map.
            if (_fogVisionBinder != null) _fogVisionBinder.enabled = false;

            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "VisualDead",
                $"player={_networkPlayer?.DisplayName ?? "null"} ikActive={(_charIK != null && _charIK.ikActive)}",
                this);

            if (_animationController == null || _actorUtils?.charAnimator == null)
            {
                ApplyDeadRagdollVisuals();
            }
            else
            {
                _deathVisualCoroutine = StartCoroutine(DeathSafetyFallbackCoroutine());
            }
        }

        private IEnumerator DeathSafetyFallbackCoroutine()
        {
            yield return new WaitForSeconds(3.0f);
            _deathVisualCoroutine = null;
            ApplyDeadRagdollVisuals();
        }

        private void HandleDeathAnimationComplete()
        {
            if (_networkPlayer != null && _networkPlayer.IsAlive)
                return;

            if (_deathVisualCoroutine != null)
            {
                StopCoroutine(_deathVisualCoroutine);
                _deathVisualCoroutine = null;
            }

            ApplyDeadRagdollVisuals();
        }

        private void SetAliveVisuals()
        {
            if (_deathVisualCoroutine != null)
            {
                StopCoroutine(_deathVisualCoroutine);
                _deathVisualCoroutine = null;
            }

            DeactivateRagdollSafely();
            ApplyAliveModelPhysicsAndHitboxes();

            if (_actorUtils?.charAnimator != null)
                _actorUtils.charAnimator.enabled = true;

            if (_charIK != null) _charIK.ikActive = true;
            // Re-enable FOW revealer on respawn.
            if (_fogVisionBinder != null) _fogVisionBinder.enabled = true;

            // Re-apply whichever IK target the current weapon has (null if holstered).
            SetIKTargetSmooth(_weaponModelController?.LeftHandIKTarget);

            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "VisualAlive",
                $"player={_networkPlayer?.DisplayName ?? "null"} ikActive={(_charIK != null && _charIK.ikActive)} target={_weaponModelController?.LeftHandIKTarget?.name ?? "null"}",
                this);
        }

        private void ApplyDeadRagdollVisuals()
        {
            if (_actorUtils?.charAnimator != null)
                _actorUtils.charAnimator.enabled = false;
            _ragdoll?.ActivateRagdoll();
        }

        private IEnumerator ReapplyAliveVisualsAfterRagdollStart()
        {
            yield return null;

            if (_networkPlayer != null && !_networkPlayer.IsAlive)
                yield break;

            SetAliveVisuals();
        }

        private void InitializeRagdollSafely()
        {
            if (_ragdoll == null)
                return;

            try
            {
                _ragdoll.InitializeRagdoll();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CharacterVisualController] InitializeRagdoll failed on '{_modelRoot?.name}': {ex.Message}", this);
            }
        }

        private void DeactivateRagdollSafely()
        {
            if (_ragdoll == null)
                return;

            try
            {
                _ragdoll.DeactivateRagdoll();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CharacterVisualController] DeactivateRagdoll failed on '{_modelRoot?.name}': {ex.Message}", this);
            }
        }

        private void ApplyAliveModelPhysicsAndHitboxes()
        {
            if (_modelRoot == null)
                return;

            int playerLayer = LayerMask.NameToLayer("Player");
            int hitboxLayer = LayerMask.NameToLayer("PlayerHitBox");

            foreach (Transform child in _modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == null)
                    continue;

                if (child.gameObject == _modelRoot)
                {
                    if (playerLayer != -1)
                        child.gameObject.layer = playerLayer;
                    continue;
                }

                Collider collider = child.GetComponent<Collider>();
                if (collider != null && hitboxLayer != -1)
                    child.gameObject.layer = hitboxLayer;
                else if (playerLayer != -1)
                    child.gameObject.layer = playerLayer;
            }

            foreach (Collider collider in _modelRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null)
                    continue;

                bool isModelRootCapsule = collider is CapsuleCollider && collider.gameObject == _modelRoot;
                collider.enabled = !isModelRootCapsule;

                if (!isModelRootCapsule && collider.GetComponent<PlayerHitboxMarker>() == null)
                {
                    var marker = collider.gameObject.AddComponent<PlayerHitboxMarker>();
                    marker.IsHeadshot = collider.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            foreach (Rigidbody rb in _modelRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == null)
                    continue;

                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
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

            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "IKTargetBlend",
                $"player={_networkPlayer?.DisplayName ?? "null"} target={(target != null ? target.name : "null")} world={(target != null ? target.position.ToString("F2") : "null")} local={(target != null ? target.localPosition.ToString("F2") : "null")} currentWeight={_ikWeight:F2} targetWeight={_ikTargetWeight:F2} charIK={(_charIK != null ? _charIK.name : "null")}",
                this);
        }
    }
}
