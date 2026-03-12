using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Character.Data;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Network-synced character model loader.
    ///
    /// WHY THIS EXISTS:
    ///   PlayerPrefab root is a "bare" NetworkObject. The character mesh (e.g. Soldier_White)
    ///   is NOT baked into the prefab — it is instantiated at runtime under the "Model" child
    ///   after the server sends the player's skin selection (CharacterModelIndex).
    ///   Because of this, CharacterVisualController and CharacterAnimationController
    ///   CANNOT drag-and-drop PrActorUtils / PrCharacterIK / PrCharacterRagdoll in the
    ///   Inspector. Instead they subscribe to OnModelReady and bind at runtime.
    ///
    /// FLOW:
    ///   1. Server calls SetModelIndex(int) after character selection is received.
    ///   2. FishNet replicates _modelIndex SyncVar to all clients.
    ///   3. Each client calls ApplyModelIndex() → looks up CharacterDatabase → Instantiate
    ///      the CharacterDefinition.ModelPrefab under the "Model" child.
    ///   4. OnModelReady fires with the root of the new model GameObject.
    ///   5. CharacterVisualController + CharacterAnimationController bind references.
    ///
    /// BAKED-IN MODEL SUPPORT (testing / default):
    ///   If _characterDatabase is null OR the database entry has no ModelPrefab,
    ///   but "Model" child already has a child (e.g. Soldier_White dragged in during
    ///   Editor setup), OnModelReady fires immediately with the existing child —
    ///   no Instantiate needed.
    ///
    /// INSPECTOR SETUP:
    ///   • _characterDatabase — drag CharacterDatabase.asset here (one per project).
    ///   • _modelParent       — drag the "Model" child here. Auto-found by name if null.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerModelLoader : NetworkBehaviour
    {
        [Header("Character Database")]
        [Tooltip("Shared CharacterDatabase ScriptableObject.\n" +
                 "Create via right-click → NightHunt/Character/Character Database.\n" +
                 "Leave null to fall back to the baked-in Model child (Editor test mode).")]
        [SerializeField] private CharacterDatabase _characterDatabase;

        [Header("References")]
        [Tooltip("The 'Model' child transform. Leave null — auto-found by name in Awake.")]
        [SerializeField] private Transform _modelParent;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired on every client (and the server) once the model GameObject is ready
        /// (either freshly instantiated or already present as a baked child).
        /// The argument is the root of the model (e.g. Soldier_White).
        /// </summary>
        public event System.Action<GameObject> OnModelReady;

        // ── State ──────────────────────────────────────────────────────────────

        // Server-authoritative index into _modelPrefabs.
        private readonly SyncVar<int> _modelIndex = new SyncVar<int>(
            0, new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));

        private GameObject _currentModelInstance;

        // ── Unity ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_modelParent == null)
            {
                Transform found = transform.Find("Model");
                _modelParent = found;
                if (_modelParent == null)
                    Debug.LogError("[PlayerModelLoader] Awake: Could not find 'Model' child. " +
                                   "Please assign _modelParent in the Inspector.");
                else
                    Debug.Log($"[PlayerModelLoader] Awake: Found 'Model' child. childCount={_modelParent.childCount} db={(_characterDatabase != null ? _characterDatabase.name : "NULL")}");
            }
            else
            {
                Debug.Log($"[PlayerModelLoader] Awake: _modelParent already assigned='{_modelParent.name}' childCount={_modelParent.childCount} db={(_characterDatabase != null ? _characterDatabase.name : "NULL")}");
            }
        }

        // ── FishNet ────────────────────────────────────────────────────────────

        public override void OnStartClient()
        {
            base.OnStartClient();
            _modelIndex.OnChange += OnModelIndexChanged;

            Debug.Log($"[PlayerModelLoader] OnStartClient: ObjId={ObjectId} IsOwner={IsOwner} IsServer={IsServerInitialized} _modelIndex={_modelIndex.Value} _modelParent={((_modelParent != null) ? _modelParent.name : "NULL")} db={(_characterDatabase != null ? _characterDatabase.name : "NULL")}");

            // Apply the value we already have from the spawn packet (handles late-joiners).
            ApplyModelIndex(_modelIndex.Value);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _modelIndex.OnChange -= OnModelIndexChanged;
        }

        // ── Server API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Server-side: set which character skin the player is using.
        /// Replicates to all clients; each client will (re-)instantiate the model.
        /// Call this after the player's character selection is confirmed.
        /// </summary>
        public void SetModelIndex(int index)
        {
            if (!IsServerInitialized)
            {
                Debug.LogError("[PlayerModelLoader] SetModelIndex must be called on the server.");
                return;
            }
            Debug.Log($"[PlayerModelLoader] SetModelIndex: index={index} (prev={_modelIndex.Value}) ObjId={ObjectId}");
            _modelIndex.Value = index;
        }

        // ── Internal ───────────────────────────────────────────────────────────

        private void OnModelIndexChanged(int prev, int next, bool asServer)
        {
            Debug.Log($"[PlayerModelLoader] OnModelIndexChanged: {prev} → {next} asServer={asServer} ObjId={ObjectId}");
            // OnStartClient already called ApplyModelIndex for the initial value.
            // This callback fires only on subsequent changes, and only on clients.
            if (!asServer)
                ApplyModelIndex(next);
        }

        private void ApplyModelIndex(int index)
        {
            Debug.Log($"[PlayerModelLoader] ApplyModelIndex ENTRY: index={index} _modelParent={(_modelParent != null ? _modelParent.name : "NULL")} db={(_characterDatabase != null ? _characterDatabase.name : "NULL")} ObjId={ObjectId}");

            if (_modelParent == null)
            {
                Debug.LogError($"[PlayerModelLoader] ApplyModelIndex: ABORT — _modelParent is null. ObjId={ObjectId}");
                return;
            }

            // ── No database configured → fall back to baked-in child ───────────
            if (_characterDatabase == null)
            {
                Debug.LogWarning($"[PlayerModelLoader] ApplyModelIndex: _characterDatabase is NULL. Checking baked children (count={_modelParent.childCount})...");
                if (_modelParent.childCount > 0)
                {
                    _currentModelInstance = _modelParent.GetChild(0).gameObject;
                    Debug.Log($"[PlayerModelLoader] ApplyModelIndex: Using baked child '{_currentModelInstance.name}'. Firing OnModelReady.");
                    SetupModelLayers(_currentModelInstance);
                    OnModelReady?.Invoke(_currentModelInstance);
                }
                else
                {
                    Debug.LogWarning("[PlayerModelLoader] _characterDatabase is not assigned and 'Model' " +
                                     "has no children. Assign CharacterDatabase.asset to PlayerModelLoader, " +
                                     "or bake a model as a child of Model for Editor testing.");
                }
                return;
            }

            // ── Look up definition in database ────────────────────────────────
            CharacterDefinition def = _characterDatabase.GetByIndex(index);

            Debug.Log($"[PlayerModelLoader] ApplyModelIndex: db.GetByIndex({index}) → def={(def != null ? def.CharacterId : "NULL")} prefab={(def?.ModelPrefab != null ? def.ModelPrefab.name : "NULL")}");

            if (def == null || def.ModelPrefab == null)
            {
                // Database entry missing ModelPrefab → try baked child as fallback
                if (_modelParent.childCount > 0)
                {
                    Debug.LogWarning($"[PlayerModelLoader] CharacterDatabase entry [{index}] has no ModelPrefab. " +
                                     "Using existing child as fallback.");
                    _currentModelInstance = _modelParent.GetChild(0).gameObject;
                    SetupModelLayers(_currentModelInstance);
                    OnModelReady?.Invoke(_currentModelInstance);
                }
                else
                {
                    Debug.LogError($"[PlayerModelLoader] CharacterDatabase entry [{index}] has no ModelPrefab " +
                                   "and 'Model' child is empty. Assign ModelPrefab in the CharacterDefinition asset.");
                }
                return;
            }

            // ── Destroy previous model ────────────────────────────────────────
            if (_currentModelInstance != null)
            {
                Debug.Log($"[PlayerModelLoader] ApplyModelIndex: Destroying previous model '{_currentModelInstance.name}'.");
                Destroy(_currentModelInstance);
            }

            // ── Instantiate new model ─────────────────────────────────────────
            Debug.Log($"[PlayerModelLoader] ApplyModelIndex: Instantiating '{def.ModelPrefab.name}' under '{_modelParent.name}'.");
            _currentModelInstance = Instantiate(def.ModelPrefab, _modelParent);
            _currentModelInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _currentModelInstance.transform.localScale = Vector3.one;

            SetupModelLayers(_currentModelInstance);

            Debug.Log($"[PlayerModelLoader] ApplyModelIndex: SUCCESS — spawned '{_currentModelInstance.name}'. Firing OnModelReady. ObjId={ObjectId}");
            OnModelReady?.Invoke(_currentModelInstance);
        }

        // ── Layer Setup ────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the correct physics/rendering layers on the player hierarchy:
        ///   • Root (this GameObject)          → "Character"
        ///   • Model root (e.g. Soldier_White) → "PlayerCharacter"
        ///   • All children of model root      → "PlayerHitBox"
        /// </summary>
        private void SetupModelLayers(GameObject modelRoot)
        {
            int characterLayer  = LayerMask.NameToLayer("Character");
            int playerCharLayer = LayerMask.NameToLayer("PlayerCharacter");
            int hitboxLayer     = LayerMask.NameToLayer("PlayerHitBox");

            if (characterLayer  == -1) Debug.LogWarning("[PlayerModelLoader] Layer 'Character' not found in Project Settings.");
            if (playerCharLayer == -1) Debug.LogWarning("[PlayerModelLoader] Layer 'PlayerCharacter' not found in Project Settings.");
            if (hitboxLayer     == -1) Debug.LogWarning("[PlayerModelLoader] Layer 'PlayerHitBox' not found in Project Settings.");

            // 1. Player prefab root → "Character"
            if (characterLayer != -1)
                gameObject.layer = characterLayer;

            // 2. Model root (Soldier_White, etc.) → "PlayerCharacter"
            if (playerCharLayer != -1)
                modelRoot.layer = playerCharLayer;

            // 3. All children of model root (bones, hitboxes, meshes) → "PlayerHitBox"
            if (hitboxLayer != -1)
            {
                Transform[] children = modelRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.gameObject == modelRoot) continue; // model root already set above
                    child.gameObject.layer = hitboxLayer;
                }
            }

            // 4. Disable model-root Rigidbody (make kinematic).
            //    Soldier_White prefab has isKinematic=false on its root Rigidbody.
            //    When parented under the player, gravity still acts on it independently
            //    every physics tick and fights the CharacterController on the player root
            //    → the model "kéo tụt" / drifts down.
            //    PrCharacterRagdoll.InitializeRagdoll() deliberately skips the root GO
            Rigidbody modelRb = ComponentResolver.Find<Rigidbody>(modelRoot)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] Rigidbody not found")
                .Resolve();
            if (modelRb != null)
            {
                modelRb.isKinematic      = true;
                modelRb.detectCollisions = false;
            }

            // 5. Disable model-root CapsuleCollider.
            //    CharacterController on the player root is the authoritative physics capsule.
            CapsuleCollider modelCap = ComponentResolver.Find<CapsuleCollider>(modelRoot)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CapsuleCollider not found")
                .Resolve();
            if (modelCap != null)
                modelCap.enabled = false;

            // 6. Disable root motion immediately.
            //    The prefab stores m_ApplyRootMotion=1. PrActorUtils.Update() overwrites it
            //    false each frame, but charAnimator is cached in PrActorUtils.Awake() now —
            Animator modelAnim = ComponentResolver.Find<Animator>(modelRoot)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] Animator not found")
                .Resolve();
            if (modelAnim != null)
                modelAnim.applyRootMotion = false;

            Debug.Log($"[PlayerModelLoader] SetupModelLayers: root='{gameObject.name}'({LayerMask.LayerToName(gameObject.layer)}) " +
                      $"modelRoot='{modelRoot.name}'({LayerMask.LayerToName(modelRoot.layer)}) " +
                      $"rb=kinematic={modelRb != null} col=disabled={modelCap != null} rootMotion=off " +
                      $"children={modelRoot.GetComponentsInChildren<Transform>(true).Length - 1} → 'PlayerHitBox'");
        }
    }
}
