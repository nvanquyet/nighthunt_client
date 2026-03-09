using UnityEngine;
using NightHunt.Networking;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Bridges networked lifecycle and weapon state to local visuals:
    /// ragdoll activation, animator enable/disable, and IK targets for ALL clients.
    ///
    /// HOW IT WORKS:
    ///  - PlayerStatSystem.SyncList fires OnStatChanged on every client.
    ///  - CharacterLifecycleController.HandleDeath() therefore runs on every client,
    ///    firing the C# events OnDied / OnRespawned locally.
    ///  - This controller subscribes to those events to drive the visual state.
    ///  - NetworkPlayer._isAlive.OnChange provides a second path for late-joiners
    ///    who receive the SyncVar snapshot in the spawn packet before stats sync.
    ///
    /// INSPECTOR SETUP:
    ///  1. Attach to the ROOT player prefab alongside NetworkPlayer.
    ///  2. Also add PlayerModelLoader to the root — it will call BindModel() at runtime.
    ///  3. _lifecycle, _networkPlayer, _weaponSystemSource all auto-resolve via GetComponent.
    ///     DO NOT try to drag PrActorUtils / PrCharacterIK / PrCharacterRagdoll —
    ///     those live on the dynamically-spawned model child (e.g. Soldier_White)
    ///     and are bound automatically when PlayerModelLoader fires OnModelReady.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVisualController : MonoBehaviour
    {
        [Header("Core Components (auto-resolved, drag only if needed)")]
        [SerializeField] private CharacterLifecycleController _lifecycle;
        [SerializeField] private NetworkPlayer _networkPlayer;
        [SerializeField] private MonoBehaviour _weaponSystemSource;

        // These are NEVER set in the Inspector — they are injected at runtime via
        // PlayerModelLoader.OnModelReady → BindModel(). The model (Soldier_White) is
        // spawned dynamically, so Inspector drag-and-drop is impossible.
        private PrCharacterRagdoll _ragdoll;
        private PrActorUtils       _actorUtils;
        private PrCharacterIK      _charIK;

        private IWeaponSystem    _weaponSystem;
        private PlayerModelLoader _modelLoader;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_lifecycle == null)
                _lifecycle = GetComponent<CharacterLifecycleController>();

            if (_networkPlayer == null)
                _networkPlayer = GetComponent<NetworkPlayer>();

            _weaponSystem = _weaponSystemSource as IWeaponSystem
                         ?? GetComponent<IWeaponSystem>();

            _modelLoader = GetComponent<PlayerModelLoader>();
            if (_modelLoader != null)
                _modelLoader.OnModelReady += BindModel;
            else
                Debug.LogWarning("[CharacterVisualController] PlayerModelLoader not found on root. " +
                                 "Model components will not be bound automatically.");
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
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleDied()
            => SetDeadVisuals();

        private void HandleRespawned()
            => SetAliveVisuals();

        /// <summary>
        /// Backup / late-join path: fires when _isAlive SyncVar arrives in spawn packet.
        /// Prevents newly-connected clients from seeing a dead player standing upright.
        /// </summary>
        private void HandleAliveChanged(bool isAlive)
        {
            if (isAlive) SetAliveVisuals();
            else         SetDeadVisuals();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? _, WeaponSlotType? newSlot)
            => RefreshIKTargets(newSlot);

        // ── Model Binding ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by PlayerModelLoader.OnModelReady once the character mesh
        /// (e.g. Soldier_White) has been instantiated under the Model child.
        /// Extracts SciFi component references from the model root and applies
        /// the correct initial visual state (alive vs dead).
        /// </summary>
        private void BindModel(GameObject modelRoot)
        {
            _actorUtils = modelRoot.GetComponentInChildren<PrActorUtils>(true);
            _charIK     = modelRoot.GetComponentInChildren<PrCharacterIK>(true);
            _ragdoll    = modelRoot.GetComponentInChildren<PrCharacterRagdoll>(true);

            if (_actorUtils == null)
                Debug.LogWarning($"[CharacterVisualController] PrActorUtils not found on model '{modelRoot.name}'.");
            if (_charIK == null)
                Debug.LogWarning($"[CharacterVisualController] PrCharacterIK not found on model '{modelRoot.name}'.");
            if (_ragdoll == null)
                Debug.LogWarning($"[CharacterVisualController] PrCharacterRagdoll not found on model '{modelRoot.name}'.");

            // Apply the correct visual state immediately so a newly-loaded model
            // reflects the current alive/dead state (important for late-joiners
            // who join while a player is ragdolled).
            bool isAlive = _networkPlayer == null || _networkPlayer.IsAlive;
            if (isAlive)
                SetAliveVisuals();
            else
                SetDeadVisuals();
        }

        // ── Visual State ──────────────────────────────────────────────────────

        private void SetDeadVisuals()
        {
            // 1. Disable IK while ragdoll is active (OnAnimatorIK would fight physics).
            if (_charIK != null)
                _charIK.ikActive = false;

            // 2. Stop the Animator so it doesn't override ragdoll bone transforms.
            if (_actorUtils?.charAnimator != null)
                _actorUtils.charAnimator.enabled = false;

            // 3. Activate physics-driven ragdoll.
            _ragdoll?.ActivateRagdoll();
        }

        private void SetAliveVisuals()
        {
            // 1. Deactivate ragdoll / return bones to kinematic.
            _ragdoll?.DeactivateRagdoll();

            // 2. Re-enable Animator before refreshing IK so targets take effect.
            if (_actorUtils?.charAnimator != null)
                _actorUtils.charAnimator.enabled = true;

            if (_charIK != null)
                _charIK.ikActive = true;

            // 3. Restore IK for the currently active weapon.
            RefreshIKTargets(_weaponSystem?.GetActiveWeaponSlot());
        }

        // ── IK Wiring ─────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the "ArmIK" (or "ArmIK_L" / "ArmIK_R") child Transform under WeaponR
        /// and assigns it to PrCharacterIK so the avatar's hands follow the weapon grip.
        /// The weapon 3D model must be parented to PrActorUtils.WeaponR at runtime.
        /// </summary>
        private void RefreshIKTargets(WeaponSlotType? slot)
        {
            if (_charIK == null || _actorUtils == null) return;

            // Clear current targets.
            _charIK.leftHandTarget  = null;
            _charIK.rightHandTarget = null;

            if (slot == null) return;

            Transform weaponRoot = _actorUtils.WeaponR;
            if (weaponRoot == null) return;

            // Support both unified ("ArmIK") and split ("ArmIK_L" / "ArmIK_R") nodes.
            Transform armIKL = weaponRoot.Find("ArmIK_L") ?? weaponRoot.Find("ArmIK");
            Transform armIKR = weaponRoot.Find("ArmIK_R") ?? weaponRoot.Find("ArmIK");

            _charIK.leftHandTarget  = armIKL;
            _charIK.rightHandTarget = armIKR;
        }
    }
}
