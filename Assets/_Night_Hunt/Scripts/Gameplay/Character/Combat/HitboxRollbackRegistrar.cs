using System.Reflection;
using FishNet.Component.ColliderRollback;
using FishNet.Object;
using NightHunt.Core;
using NightHunt.Gameplay.Character;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Late-wires FishNet <see cref="ColliderRollback"/> to the player's hitbox GameObjects
    /// AFTER the character model has been dynamically loaded by <see cref="PlayerModelLoader"/>.
    ///
    /// ══════════════════════════════════════════════════════
    ///  WHY THIS EXISTS
    /// ══════════════════════════════════════════════════════
    ///  Hitbox colliders are NOT in the player prefab — they live inside the character
    ///  model that is instantiated at runtime by PlayerModelLoader (index-based skin system).
    ///  FishNet's ColliderRollback snaps collider positions each tick so the server can
    ///  rewind hitboxes to the exact tick the client reported firing (lag compensation).
    ///
    ///  Problem: ColliderRollback.OnStartServer fires before the model is loaded, so
    ///  its _colliderParents array is empty and no snapshots are taken for hitboxes.
    ///
    ///  Solution: Subscribe to PlayerModelLoader.OnModelReady (fires each time a skin is
    ///  loaded or changed). When the model is ready, collect all child GameObjects on the
    ///  PlayerHitBox layer, inject them into ColliderRollback._colliderParents via
    ///  reflection, then re-call InitializeRollingColliders() so future ticks are snapped.
    ///
    ///  Flow with hitbox rewind:
    ///    1. Client fires → RequestAuthoritativeShotServerRpc(..., PreciseTick shotTick)
    ///    2. Server: RollbackManager.Rollback(shotTick) → all ColliderRollback objects
    ///       rewind to the tick the client had when pulling the trigger
    ///    3. Server: Physics.RaycastNonAlloc → hits rolled-back hitbox positions
    ///    4. Server: RollbackManager.Return() → restores current positions
    ///
    /// ══════════════════════════════════════════════════════
    ///  PREFAB SETUP  (one-time, no per-model work needed)
    /// ══════════════════════════════════════════════════════
    ///  A. NetworkManager GO:
    ///     • Add RollbackManager component.
    ///     • MaximumRollbackTime ≥ 0.5 s (1.0–1.25 s covers 200–400 ms ping).
    ///
    ///  B. Player prefab root GO:
    ///     • Add HitboxRollbackRegistrar  (this component).
    ///     • Add ColliderRollback         (leave _colliderParents empty — filled at runtime).
    ///
    ///  C. Character model prefabs:
    ///     • Ensure hitbox child GOs are on the "PlayerHitBox" layer
    ///       (PlayerModelLoader.SetupModelLayers already handles this automatically).
    ///
    ///  D. WeaponSystem calls RollbackManager.Rollback/Return automatically —
    ///     no further wiring required.
    ///
    /// ══════════════════════════════════════════════════════
    ///  NOTES
    /// ══════════════════════════════════════════════════════
    ///  • ColliderRollback.OnStartServer registers with RollbackManager and sets
    ///    _maxSnapshots. We only replace the rolling-collider list, not those.
    ///  • Reflection targets are stable FishNet internal fields; version changes may
    ///    require updating the field/method names below (_colliderParents, InitializeRollingColliders).
    ///  • Skin changes (player model swaps) re-fire OnModelReady → colliders re-wired automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitboxRollbackRegistrar : NetworkBehaviour
    {
        // ── Reflection cache (filled once per type, shared across instances) ───
        private static readonly FieldInfo  _colliderParentsField  = typeof(ColliderRollback)
            .GetField("_colliderParents",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo _deinitMethod = typeof(ColliderRollback)
            .GetMethod("DeinitializeRollingColliders",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo _initMethod = typeof(ColliderRollback)
            .GetMethod("InitializeRollingColliders",
                BindingFlags.Instance | BindingFlags.NonPublic);

        // ── Runtime state ─────────────────────────────────────────────────────
        private PlayerModelLoader _modelLoader;
        private ColliderRollback  _colliderRollback;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _modelLoader      = GetComponentInChildren<PlayerModelLoader>()
                             ?? GetComponentInParent<PlayerModelLoader>();
            _colliderRollback = GetComponent<ColliderRollback>();

            if (_colliderRollback == null)
                Debug.LogWarning(
                    "[HitboxRollbackRegistrar] No ColliderRollback found on this GameObject. " +
                    "Add ColliderRollback to the player prefab root (leave _colliderParents empty).",
                    this);

            if (_colliderParentsField == null || _initMethod == null || _deinitMethod == null)
                Debug.LogError(
                    "[HitboxRollbackRegistrar] Reflection failed — FishNet internal API may have changed. " +
                    "Check field name '_colliderParents' and method 'InitializeRollingColliders' in ColliderRollback.",
                    this);
        }

        private void OnDestroy()
        {
            if (_modelLoader != null)
                _modelLoader.OnModelReady -= OnModelReady;
        }

        // ── FishNet lifecycle ─────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_modelLoader == null)
            {
                Debug.LogWarning("[HitboxRollbackRegistrar] PlayerModelLoader not found — hitbox rewind disabled.", this);
                return;
            }

            _modelLoader.OnModelReady += OnModelReady;

            // If the model was already loaded before OnStartServer fired (e.g. host mode
            // where OnStartClient runs first and loads the model), wire up immediately.
            if (_modelLoader.CurrentModelInstance != null)
                OnModelReady(_modelLoader.CurrentModelInstance);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_modelLoader != null)
                _modelLoader.OnModelReady -= OnModelReady;
        }

        // ── Model-ready handler ───────────────────────────────────────────────

        /// <summary>
        /// Called every time a character model is loaded or swapped.
        /// Rewires ColliderRollback._colliderParents to the model's hitbox GOs.
        /// Server-only — client models don't need rollback snapshots.
        /// </summary>
        private void OnModelReady(GameObject modelRoot)
        {
            if (!IsServerStarted) return;
            if (_colliderRollback == null || _colliderParentsField == null
                || _initMethod == null || _deinitMethod == null) return;

            // Collect all child GOs on the PlayerHitBox layer that have at least one Collider.
            int hitboxLayer = NightHuntLayers.IdPlayerHitBox;
            var hitboxGOs   = new System.Collections.Generic.List<GameObject>();

            foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child.gameObject.layer == hitboxLayer
                    && child.GetComponent<Collider>() != null)
                {
                    hitboxGOs.Add(child.gameObject);
                }
            }

            if (hitboxGOs.Count == 0)
            {
                Debug.LogWarning(
                    $"[HitboxRollbackRegistrar] No colliders found on layer '{NightHuntLayers.PlayerHitBox}' " +
                    $"inside model '{modelRoot.name}'. Check that PlayerModelLoader.SetupModelLayers is working.",
                    modelRoot);
                return;
            }

            // Replace _colliderParents and re-initialize the rolling collider list.
            _colliderParentsField.SetValue(_colliderRollback, hitboxGOs.ToArray());
            _deinitMethod.Invoke(_colliderRollback, null);
            _initMethod.Invoke(_colliderRollback, null);

            Debug.Log(
                $"[HitboxRollbackRegistrar] Wired {hitboxGOs.Count} hitbox collider(s) " +
                $"from model '{modelRoot.name}' into ColliderRollback.",
                this);
        }
    }
}
