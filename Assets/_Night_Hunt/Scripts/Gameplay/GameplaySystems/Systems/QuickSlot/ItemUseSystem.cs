using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Systems;
using NightHunt.Gameplay.Character;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.ItemUse
{
    /// <summary>
    /// Orchestrates item usage on the server, routing consumables to
    /// <see cref="ConsumableHandler"/> and throwables to <see cref="ThrowableHandler"/>.
    /// </summary>
    public class ItemUseSystem : NetworkBehaviour, IItemUseSystem, IDisposable
    {
        #region Serialized Fields

        [Header("References")] [SerializeField]
        private WeaponSystem _weaponSystemComponent;

        [SerializeField] private PlayerStatSystem _statSystemComponent;
        [SerializeField] private InventorySystem _inventorySystemComponent;

        [Header("Handlers")] [SerializeField] private ConsumableHandler _consumableHandler;
        [SerializeField] private ThrowableHandler _throwableHandler;

        [Header("Settings")]
        [Tooltip("Fallback use duration when ConsumableDefinition.UsageDuration == 0")]
        [SerializeField]
        private float _defaultUseTime = 3.5f;

        [Header("Visual")]
        [Tooltip("PrActorUtils in the player prefab — provides WeaponR hand bone. " +
                 "Auto-resolved via GetComponentInChildren if not assigned.")]
        [SerializeField] private PrActorUtils _actorUtils;

        #endregion

        #region Runtime State

        private IWeaponSystem _weaponSystem;
        private IPlayerStatSystem _statSystem;
        private IInventorySystem _inventorySystem;
        private bool _isUsingItem;
        private ItemInstance _currentItem;
        private WeaponSlotType? _previousWeaponSlot;
        private Coroutine _useCoroutine;
        private GameObject _itemInHandModel;   // throwable model instantiated on WeaponR bone
        // Last confirmed throw target — set in ExecuteThrow so DetachItemFromHand
        // can toss the hand model in the right direction without touching any client-only statics.
        private Vector3 _pendingThrowTarget;

        #endregion

        #region Properties

        public bool IsUsingItem => _isUsingItem;
        public ItemInstance CurrentItem => _currentItem;

        #endregion

        #region Events

        public event Action<ItemInstance> OnItemUseStarted;
        public event Action<ItemInstance> OnItemUseCompleted;
        public event Action<ItemInstance> OnItemUseCancelled;
        public event Action<ItemInstance, float> OnItemUseProgress;
        public event Action OnThrowExecuted;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            ValidateReferences();
            InitializeHandlers();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate()
        {
            ValidateReferences();
        }
#endif

        private void ValidateReferences()
        {
            _weaponSystem = ComponentResolver.Find<IWeaponSystem>(this)
                .UseExisting(_weaponSystemComponent)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning("[ItemUseSystem] IWeaponSystem not found")
                .Resolve();

            if (_weaponSystem is WeaponSystem wsConcrete)
                _weaponSystemComponent = wsConcrete;

            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .UseExisting(_statSystemComponent)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning("[ItemUseSystem] IPlayerStatSystem not found")
                .Resolve();

            if (_statSystem is PlayerStatSystem ssConcrete)
                _statSystemComponent = ssConcrete;

            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                .UseExisting(_inventorySystemComponent)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning("[ItemUseSystem] IInventorySystem not found")
                .Resolve();

            if (_inventorySystem is InventorySystem invConcrete)
                _inventorySystemComponent = invConcrete;

            if (_weaponSystem == null)
                Debug.LogError("[ItemUseSystem] IWeaponSystem is null!");

            if (_statSystem == null)
                Debug.LogError("[ItemUseSystem] IPlayerStatSystem is null!");

            if (_inventorySystem == null)
                Debug.LogError("[ItemUseSystem] IInventorySystem is null!");
        }

        private void InitializeHandlers()
        {
            // Auto-create handlers if not assigned
            if (_consumableHandler == null)
            {
                _consumableHandler = gameObject.AddComponent<ConsumableHandler>();
                if (_statSystem == null)
                    Debug.LogWarning("[ItemUseSystem] ConsumableHandler created but IPlayerStatSystem is null — consumable effects won't apply.");
                _consumableHandler.Initialize(_statSystem);
            }

            if (_throwableHandler == null)
            {
                _throwableHandler = gameObject.AddComponent<ThrowableHandler>();
                _throwableHandler.Initialize(transform);
            }

            // Auto-resolve PrActorUtils for hand-bone item model spawning.
            if (_actorUtils == null)
                _actorUtils = ComponentResolver.Find<PrActorUtils>(this)
                    .OnSelf().InChildren().InRootChildren()
                    .OrLogWarning("[ItemUseSystem] PrActorUtils not found — throwable hand model will be skipped")
                    .Resolve();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Main entry point for item use. Routes to the server automatically when called
        /// from the owning client (dedicated server or host mode).
        /// For the return value on client→server calls the result is always true
        /// (fire-and-forget via ServerRpc); the authoritative outcome fires via events.
        /// </summary>
        public bool UseItem(ItemInstance item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ItemUseSystem] UseItem: item is null");
                return false;
            }

            // Client in dedicated-server mode: route to server via RPC.
            if (!IsServerInitialized)
            {
                if (IsOwner)
                {
                    RequestUseItemServerRpc(item.InstanceID);
                    return true; // Optimistic — outcome fires via events.
                }
                Debug.LogWarning("[ItemUseSystem] UseItem: caller does not own this object.");
                return false;
            }

            return UseItemServer(item);
        }

        /// <summary>
        /// ServerRpc sent by the owning client to initiate item use on the server.
        /// Uses the InstanceID so FishNet does not need to serialise the full ItemInstance.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestUseItemServerRpc(string instanceID)
        {
            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[ItemUseSystem] RequestUseItemServerRpc: item '{instanceID}' not found on server.");
                return;
            }
            UseItemServer(item);
        }

        [Server]
        private bool UseItemServer(ItemInstance item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ItemUseSystem] UseItemServer: item is null");
                return false;
            }

            if (_isUsingItem)
            {
                Debug.LogWarning("[ItemUseSystem] Already using an item");
                return false;
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                Debug.LogError($"[ItemUseSystem] No definition: {item.DefinitionID}");
                return false;
            }

            if (def is ConsumableDefinition cd)
                return BeginConsumable(item, cd);

            if (def is ThrowableDefinition td)
                return BeginThrowable(item, td);

            if (def.Type == ItemType.Deployable)
                return BeginDeployable(item, def);

            Debug.LogWarning($"[ItemUseSystem] Unsupported item type: {def.GetType().Name}");
            return false;
        }

        /// <summary>
        /// Execute throw (called by input when Fire pressed during throw-mode).
        /// Starts a coroutine that waits <see cref="ThrowableDefinition.PrepareTime"/> seconds
        /// (pull-pin / wind-up animation window) before spawning the projectile.
        /// The coroutine is stored in <see cref="_useCoroutine"/> so <see cref="CancelUse"/>
        /// can abort mid-prepare via StopCoroutine.
        /// </summary>
        [Server]
        public void ExecuteThrow(Vector3 aimTarget)
        {
            if (!_isUsingItem || _currentItem == null)
            {
                Debug.LogWarning("[ItemUseSystem] ExecuteThrow: no active throwable");
                return;
            }

            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID) as ThrowableDefinition;
            if (def == null)
            {
                Debug.LogError("[ItemUseSystem] Current item is not throwable");
                return;
            }

            // Reuse the shared _useCoroutine slot so CancelUse() can interrupt mid-prepare.
            _pendingThrowTarget = aimTarget;
            _useCoroutine = StartCoroutine(PrepareAndThrow(def, aimTarget));
        }

        [Server]
        private IEnumerator PrepareAndThrow(ThrowableDefinition def, Vector3 aimTarget)
        {
            if (def.PrepareTime > 0f)
            {
                yield return new WaitForSeconds(def.PrepareTime);
            }

            // Guard: might have been cancelled during wind-up.
            if (!_isUsingItem || _currentItem == null)
                yield break;

            _throwableHandler.SpawnProjectile(def, transform, aimTarget);
            DetachItemFromHand();
            OnThrowExecuted?.Invoke();

            var item = _currentItem;
            ConsumeItem(item);
            CompleteUse(item);
        }

        /// <summary>
        /// Called by the owning client (CombatInputHandler.BeginFire / ItemAimController
        /// mobile ConfirmAim) to trigger ExecuteThrow on the server.
        /// FishNet ServerRpcs from the same client are ordered, so a prior
        /// item-selection RPC (→ BeginThrowable) is guaranteed to complete
        /// before this one processes.
        /// </summary>
        // BUG 2 FIX: aimTarget is the client-side world position of the cursor.
        // The server cannot read ItemAimController.AimWorldTarget (client-only static);
        // passing it as a ServerRpc parameter sends the correct position to the server.
        [ServerRpc(RequireOwnership = true)]
        public void RequestExecuteThrow(Vector3 aimTarget)
        {
            ExecuteThrow(aimTarget);
        }

        /// <summary>
        /// Request cancel of the in-progress item use from the owning client.
        /// Routes to <see cref="CancelUse"/> on the server. Safe to call when
        /// no item is in use (no-op via CancelUse's guard).
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestCancelUse()
        {
            CancelUse();
        }

        /// <summary>
        /// Cancel in-progress use
        /// </summary>
        [Server]
        public void CancelUse()
        {
            if (!_isUsingItem)
                return;

            // Check if cancellation allowed
            var def = ItemDatabase.GetDefinition(_currentItem?.DefinitionID);
            if (def != null && !def.CanCancelUsage)
            {
                Debug.Log("[ItemUseSystem] This item cannot be cancelled");
                return;
            }

            // Stop coroutine if running
            if (_useCoroutine != null)
            {
                StopCoroutine(_useCoroutine);
                _useCoroutine = null;
            }

            var item = _currentItem;
            _isUsingItem = false;
            _currentItem = null;

            OnItemUseCancelled?.Invoke(item);
            DestroyItemInHand();
            RestoreWeapon();
        }

        #endregion

        #region Consumable Flow

        private bool BeginConsumable(ItemInstance item, ConsumableDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;

            // Show item in hand while consuming (uses EquippedPrefab as fallback)
            SpawnItemInHandGeneric(def);

            float duration = def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime;
            _useCoroutine = StartCoroutine(ConsumableRoutine(item, def, duration));

            OnItemUseStarted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Consumable started: '{def.DisplayName}' ({duration:F1}s) " +
                      $"prefab={(def.HeldPrefab != null ? def.HeldPrefab.name : "none")}");

            return true;
        }

        private IEnumerator ConsumableRoutine(ItemInstance item, ConsumableDefinition def, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                OnItemUseProgress?.Invoke(item, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            // Apply effects via handler
            _consumableHandler.ApplyEffects(def);

            // Consume & complete
            ConsumeItem(item);
            CompleteUse(item);
        }

        #endregion

        #region Throwable Flow

        private bool BeginThrowable(ItemInstance item, ThrowableDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;

            // Spawn item model in right-hand bone so the player visually holds it while aiming.
            SpawnItemInHand(def);

            OnItemUseStarted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Throw mode: '{def.DisplayName}'. Press Fire to throw.");

            return true;
        }

        #endregion

        #region Deployable Flow

        /// <summary>
        /// Stub for deployable items (beacons, etc.).
        /// TODO: implement placement-mode UI (range indicator + confirm/cancel).
        ///
        /// Item type distinctions to implement later:
        ///   • Beacon (ReviveBeacon) — spawns a world object at the placed position;
        ///     teammates can respawn at it.  Does NOT affect the placing player directly.
        ///   • SupplyBeacon — spawns a loot crate or airdrop marker.
        ///   • General Deployable — any trap / equipment placed in the world.
        /// </summary>
        private bool BeginDeployable(ItemInstance item, ItemDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;

            SpawnItemInHandGeneric(def);
            OnItemUseStarted?.Invoke(item);

            float usable = (def as UsableItemDefinition)?.UsageDuration ?? 0f;
            float duration = usable > 0f ? usable : 2f;
            _useCoroutine = StartCoroutine(DeployableRoutine(item, def, duration));

            Debug.Log($"[ItemUseSystem] Deployable '{def.DisplayName}': placing in {duration:F1}s...");
            return true;
        }

        private IEnumerator DeployableRoutine(ItemInstance item, ItemDefinition def, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                OnItemUseProgress?.Invoke(item, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            Debug.Log($"[ItemUseSystem] Deployable '{def.DisplayName}' placed at {transform.position:F1}.");

            ConsumeItem(item);
            CompleteUse(item);
        }

        #endregion

        #region Hand Model

        /// <summary>
        /// Instantiate the throwable's <see cref="PhysicalItemDefinition.HeldPrefab"/> on the
        /// right-hand bone (WeaponR) so the player visually holds the item while in
        /// throw-aim mode.  Uses the same parent and local transform as WeaponModelController.
        /// </summary>
        private void SpawnItemInHand(ThrowableDefinition def)
        {
            SpawnItemInHandGeneric(def);
        }

        /// <summary>
        /// Generic hand-model spawner — tries ThrowableDefinition.HeldPrefab first,
        /// falls back to ItemDefinition.EquippedPrefab (for consumables / deployables).
        /// </summary>
        private void SpawnItemInHandGeneric(ItemDefinition def)
        {
            DestroyItemInHand();
            GameObject prefab = null;
            if (def is ThrowableDefinition td)
                prefab = td.HeldPrefab;
            if (prefab == null)
                prefab = def.HeldPrefab;   // virtual on ItemDefinition, overridden in PhysicalItemDefinition
            if (prefab == null) return;
            Transform parent = (_actorUtils?.WeaponR != null) ? _actorUtils.WeaponR : transform;
            _itemInHandModel = Instantiate(prefab, parent);
            _itemInHandModel.transform.localPosition = Vector3.zero;
            _itemInHandModel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void DestroyItemInHand()
        {
            if (_itemInHandModel != null)
            {
                Destroy(_itemInHandModel);
                _itemInHandModel = null;
            }
        }

        /// <summary>
        /// Unparent the hand model from WeaponR and launch it toward the throw target
        /// for a visual "item flies away" effect after the throw is confirmed.
        /// The real projectile is spawned separately by <see cref="ThrowableHandler"/>;
        /// this model is purely cosmetic.
        /// If the prefab has a <see cref="Rigidbody"/>, physics launch is applied.
        /// </summary>
        private void DetachItemFromHand()
        {
            if (_itemInHandModel == null) return;

            // Detach from WeaponR bone so it is no longer parented to the hand.
            _itemInHandModel.transform.SetParent(null, worldPositionStays: true);

            // Launch the hand model toward the confirmed throw target.
            // Use the instance field _pendingThrowTarget (set in ExecuteThrow) instead of
            // reading ItemAimController.AimWorldTarget which is a client-only static and
            // would be Vector3.zero on a dedicated server.
            if (_itemInHandModel.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 toTarget = _pendingThrowTarget - _itemInHandModel.transform.position;
                rb.linearVelocity = toTarget.normalized * 8f;
                rb.useGravity     = true;
            }

            Destroy(_itemInHandModel, 1.5f);
            _itemInHandModel = null;
        }

        #endregion

        #region Weapon Holster/Restore

        private void HolsterAndSave()
        {
            if (_weaponSystem == null)
                return;

            _previousWeaponSlot = _weaponSystem.GetActiveWeaponSlot();
            if (_previousWeaponSlot.HasValue)
                _weaponSystem.HolsterWeapon();
        }

        private void RestoreWeapon()
        {
            if (_weaponSystem == null)
                return;

            if (_previousWeaponSlot.HasValue)
                _weaponSystem.SelectWeapon(_previousWeaponSlot.Value);

            _previousWeaponSlot = null;
        }

        #endregion

        #region Shared Helpers

        private void ConsumeItem(ItemInstance item)
        {
            _inventorySystem?.RemoveItem(item.InstanceID, 1);
        }

        private void CompleteUse(ItemInstance item)
        {
            _isUsingItem = false;
            _currentItem = null;
            _useCoroutine = null;

            OnItemUseCompleted?.Invoke(item);
            DestroyItemInHand();
            RestoreWeapon();
        }

        #endregion

        #region Debug

        [ContextMenu("Log State")]
        public void LogState()
        {
            Debug.Log($"[ItemUseSystem] Using={_isUsingItem} | " +
                      $"Item={_currentItem?.DefinitionID ?? "none"} | " +
                      $"PrevSlot={_previousWeaponSlot}");
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            // Cancel any ongoing item use
            if (_isUsingItem)
            {
                CancelUse();
            }

            // Stop any running coroutines
            if (_useCoroutine != null)
            {
                StopCoroutine(_useCoroutine);
                _useCoroutine = null;
            }
        }

        #endregion
    }
}