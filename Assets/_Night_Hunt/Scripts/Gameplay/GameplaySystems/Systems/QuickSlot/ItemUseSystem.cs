using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Systems;
using NightHunt.Gameplay.Character;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Core;
using NightHunt.Utilities;
using NightHunt.Gameplay.Beacon;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Diagnostics;

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
        [Tooltip("PrActorUtils in the player prefab - provides WeaponR hand bone. " +
                 "Auto-resolved via GetComponentInChildren if not assigned.")]
        [SerializeField] private PrActorUtils _actorUtils;

        #endregion

        #region Runtime State

        private IWeaponSystem _weaponSystem;
        private IPlayerStatSystem _statSystem;
        private IStatApplyOrchestrator _statOrchestrator;
        private IInventorySystem _inventorySystem;
        private IDeployableHandler _deployableHandler;
        private bool _isUsingItem;
        private ItemInstance _currentItem;
        private WeaponSlotType? _previousWeaponSlot;
        private Coroutine _useCoroutine;
        private GameObject _itemInHandModel;   // throwable model instantiated on WeaponR bone
        // Last confirmed throw target set in ExecuteThrow so DetachItemFromHand
        // can toss the hand model in the right direction without touching any client-only statics.
        private Vector3 _pendingThrowTarget;

        #endregion

        #region Properties

        public bool IsUsingItem => _isUsingItem;
        public ItemInstance CurrentItem => _currentItem;
        public bool IsDeploying => _deployableHandler?.IsDeploying == true;

        #endregion

        #region Events

        public event Action<ItemInstance> OnItemUseStarted;
        public event Action<ItemInstance> OnItemUseCompleted;
        public event Action<ItemInstance> OnItemUseCancelled;
        public event Action<ItemInstance, float> OnItemUseProgress;
        public event Action OnThrowPrepareStarted;
        public event Action OnThrowExecuted;
        public event Action OnDeployStarted;
        public event Action OnDeployCompleted;

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

            _statOrchestrator = ComponentResolver.Find<IStatApplyOrchestrator>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrLogWarning("[ItemUseSystem] IStatApplyOrchestrator not found - consumable temporary modifiers will use legacy stat path.")
                .Resolve();

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

            _deployableHandler = ComponentResolver.Find<IDeployableHandler>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();

            if (_weaponSystem == null)
                Debug.LogError("[ItemUseSystem] IWeaponSystem is null!");

            if (_statSystem == null)
                Debug.LogError("[ItemUseSystem] IPlayerStatSystem is null!");

            if (_inventorySystem == null)
                Debug.LogError("[ItemUseSystem] IInventorySystem is null!");

            if (_deployableHandler == null)
                Debug.LogWarning("[ItemUseSystem] IDeployableHandler not found. Deployable items need DeployablePlacementHandler on the player prefab.");
        }

        private void InitializeHandlers()
        {
            // Auto-create handlers if not assigned
            if (_consumableHandler == null)
            {
                _consumableHandler = gameObject.AddComponent<ConsumableHandler>();
                if (_statSystem == null)
                    Debug.LogWarning("[ItemUseSystem] ConsumableHandler created but IPlayerStatSystem is null - consumable effects won't apply.");
            }
            _consumableHandler.Initialize(_statSystem, _statOrchestrator);

            if (_throwableHandler == null)
            {
                _throwableHandler = gameObject.AddComponent<ThrowableHandler>();
                _throwableHandler.Initialize(transform);
            }

            // Auto-resolve PrActorUtils for hand-bone item model spawning.
            if (_actorUtils == null)
                _actorUtils = ComponentResolver.Find<PrActorUtils>(this)
                    .OnSelf().InChildren().InRootChildren()
                    .OrLogWarning("[ItemUseSystem] PrActorUtils not found - throwable hand model will be skipped")
                    .Resolve();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Main entry point for item use. Routes to the server automatically when called
        /// from the owning client (dedicated server or host mode).
        /// For client-to-server calls the return value is always true
        /// (fire-and-forget via ServerRpc); the authoritative outcome fires via events.
        /// </summary>
        public bool UseItem(ItemInstance item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ITEM_SELECT_FLOW] ItemUse.UseItem failed: item is null.");
                Debug.LogWarning("[ItemUseSystem] UseItem: item is null");
                return false;
            }

            Debug.Log($"[ITEM_SELECT_FLOW] ItemUse.UseItem entry item={item.InstanceID} def={item.DefinitionID} qty={item.Quantity} isServer={IsServerInitialized} isOwner={IsOwner}");
            Debug.Log($"[NH_FLOW][32][ItemUse.UseItem] item={item.InstanceID} def={item.DefinitionID} qty={item.Quantity} isServer={IsServerInitialized} isOwner={IsOwner} using={_isUsingItem} current='{_currentItem?.InstanceID ?? "null"}' owner={Owner?.ClientId}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.ItemUse,
                "UseItem",
                $"item={item.InstanceID} def={item.DefinitionID} qty={item.Quantity} isServer={IsServerInitialized} isOwner={IsOwner} using={_isUsingItem}",
                this);

            // Client in dedicated-server mode: route to server via RPC.
            if (!IsServerInitialized)
            {
                if (IsOwner)
                {
                    Debug.Log($"[ITEM_SELECT_FLOW] ItemUse.UseItem -> RequestUseItemServerRpc item={item.InstanceID}");
                    Debug.Log($"[NH_FLOW][33][ItemUse.UseItem.RouteRpc] item={item.InstanceID} def={item.DefinitionID} owner={Owner?.ClientId}");
                    RequestUseItemServerRpc(item.InstanceID);
                    return true; // Optimistic; outcome fires via events.
                }
                Debug.LogWarning($"[ITEM_SELECT_FLOW] ItemUse.UseItem blocked: caller does not own item-use object item={item.InstanceID}.");
                Debug.LogWarning($"[NH_FLOW][33][ItemUse.UseItem.Rejected] reason=not-owner item={item.InstanceID} owner={Owner?.ClientId}");
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
            Debug.Log($"[ITEM_SELECT_FLOW] RequestUseItemServerRpc received instance='{instanceID}' owner={Owner?.ClientId}");
            Debug.Log($"[NH_FLOW][34][Rpc.UseItem] instance='{instanceID}' owner={Owner?.ClientId} inventory={(_inventorySystem != null ? "ok" : "null")}");
            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[ITEM_SELECT_FLOW] RequestUseItemServerRpc failed: item '{instanceID}' not found on server inventory.");
                Debug.LogWarning($"[NH_FLOW][34][Rpc.UseItem.Rejected] reason=item-not-found instance='{instanceID}' owner={Owner?.ClientId}");
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
                Debug.LogWarning("[NH_FLOW][35][ItemUse.ServerRejected] reason=null-item");
                Debug.LogWarning("[ItemUseSystem] UseItemServer: item is null");
                return false;
            }

            if (_isUsingItem)
            {
                // If currently in deployable placement mode, allow cancelling it to start a new item use.
                // All other in-progress items (throwable wind-up, consumable channeling) are protected.
                if (_deployableHandler?.IsDeploying == true)
                {
                    Debug.Log("[ItemUseSystem] UseItemServer: cancelling active deploy to start new item use.");
                    CancelUse();
                }
                else
                {
                    Debug.LogWarning($"[NH_FLOW][35][ItemUse.ServerRejected] reason=already-using requested={item.InstanceID} current={_currentItem?.InstanceID ?? "null"} deploying={_deployableHandler?.IsDeploying.ToString() ?? "null"}");
                    Debug.LogWarning("[ItemUseSystem] Already using an item");
                    return false;
                }
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                Debug.LogError($"[ITEM_SELECT_FLOW] UseItemServer failed: no definition '{item.DefinitionID}' item={item.InstanceID}.");
                Debug.LogError($"[NH_FLOW][35][ItemUse.ServerRejected] reason=def-not-found item={item.InstanceID} def={item.DefinitionID}");
                Debug.LogError($"[ItemUseSystem] No definition: {item.DefinitionID}");
                return false;
            }

            Debug.Log($"[ITEM_FLOW] [07][UseServer.Route] item={item.InstanceID} def={item.DefinitionID} defType={def.GetType().Name} itemType={def.Type}");
            Debug.Log($"[NH_FLOW][35][ItemUse.ServerRoute] item={item.InstanceID} def={def.ItemID} defType={def.GetType().Name} itemType={def.Type} previousSlot={_previousWeaponSlot?.ToString() ?? "none"} owner={Owner?.ClientId}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.ItemUse,
                "UseServerRoute",
                $"item={item.InstanceID} def={def.ItemID} defType={def.GetType().Name} itemType={def.Type} previousSlot={_previousWeaponSlot?.ToString() ?? "none"}",
                this);

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
                Debug.LogWarning($"[NH_FLOW][38][ItemUse.ExecuteThrowRejected] reason=no-active-throwable target={aimTarget:F2} using={_isUsingItem} current={_currentItem?.InstanceID ?? "null"}");
                Debug.LogWarning("[ItemUseSystem] ExecuteThrow: no active throwable");
                return;
            }

            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID) as ThrowableDefinition;
            if (def == null)
            {
                Debug.LogError($"[NH_FLOW][38][ItemUse.ExecuteThrowRejected] reason=current-not-throwable current={_currentItem.InstanceID} def={_currentItem.DefinitionID}");
                Debug.LogError("[ItemUseSystem] Current item is not throwable");
                return;
            }

            // Reuse the shared _useCoroutine slot so CancelUse() can interrupt mid-prepare.
            if (_useCoroutine != null)
            {
                Debug.LogWarning($"[NH_FLOW][38][ItemUse.ExecuteThrowRejected] reason=already-preparing current={_currentItem.InstanceID}");
                Debug.LogWarning("[ItemUseSystem] ExecuteThrow ignored: throw is already preparing.");
                return;
            }

            Vector3 sanitizedTarget = SanitizeThrowableTarget(def, aimTarget);
            _pendingThrowTarget = sanitizedTarget;
            LogThrowable($"ExecuteThrow target requested={aimTarget:F2} sanitized={sanitizedTarget:F2} item={_currentItem.InstanceID} def={def.ItemID} prepare={def.PrepareTime:F2}");
            Debug.Log($"[NH_FLOW][38][ItemUse.ExecuteThrow] item={_currentItem.InstanceID} def={def.ItemID} target={aimTarget:F2} sanitized={sanitizedTarget:F2} prepare={def.PrepareTime:F2}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Throwable,
                "ExecuteThrow",
                $"item={_currentItem.InstanceID} def={def.ItemID} requested={aimTarget:F2} sanitized={sanitizedTarget:F2} prepare={def.PrepareTime:F2}",
                this);
            RpcThrowPrepareStarted();
            _useCoroutine = StartCoroutine(PrepareAndThrow(def, sanitizedTarget));
        }

        [Server]
        private IEnumerator PrepareAndThrow(ThrowableDefinition def, Vector3 aimTarget)
        {
            // Throwable wind-up is animation timing only. It must not drive progress UI.
            if (def.PrepareTime > 0f)
                yield return new WaitForSeconds(def.PrepareTime);

            // Guard: might have been cancelled during wind-up.
            if (!_isUsingItem || _currentItem == null)
                yield break;

            LogThrowable($"PrepareAndThrow spawning def={def.ItemID} target={aimTarget:F2} player={transform.position:F2}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Throwable,
                "ThrowSpawn",
                $"item={_currentItem.InstanceID} def={def.ItemID} target={aimTarget:F2} player={transform.position:F2}",
                this);
            _throwableHandler.SpawnProjectile(def, transform, aimTarget);
            DetachItemFromHand();
            RpcThrowExecuted();

            var item = _currentItem;
            ConsumeItem(item);
            LogThrowable($"PrepareAndThrow consumed item={item.InstanceID} def={item.DefinitionID} target={aimTarget:F2}");
            CompleteUse(item);
        }

        private Vector3 SanitizeThrowableTarget(ThrowableDefinition def, Vector3 requestedTarget)
        {
            Vector3 origin = transform.position;
            Vector3 flatOffset = requestedTarget - origin;
            flatOffset.y = 0f;

            float maxThrowRange = def != null ? def.GetMaxThrowDistance() : 0f;
            float visionRange = _statSystem != null
                ? _statSystem.GetStat(NightHunt.Gameplay.StatSystem.Core.Types.PlayerStatType.VisionRange)
                : 0f;

            float maxRange = maxThrowRange > 0.1f ? maxThrowRange : 10f;
            if (visionRange > 0.1f)
                maxRange = Mathf.Min(maxRange, visionRange);

            Vector3 direction = flatOffset.sqrMagnitude > 0.0001f
                ? flatOffset.normalized
                : transform.forward;
            Vector3 clamped = origin + direction * Mathf.Clamp(flatOffset.magnitude, 0.5f, maxRange);

            Vector3 grounded = ProjectThrowableTargetToGround(clamped);
            LogThrowable($"Sanitize target requested={requestedTarget:F2} grounded={grounded:F2} maxRange={maxRange:F2} def={def?.ItemID ?? "null"}");
            return grounded;
        }

        private static Vector3 ProjectThrowableTargetToGround(Vector3 target)
        {
            Vector3 rayOrigin = target + Vector3.up * 8f;
            LayerMask surfaceMask = NightHuntLayers.MaskPlacementSurface;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 24f, surfaceMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            Debug.LogWarning($"[THROW_FLOW] Ground projection miss target={target:F2} surfaceMask={surfaceMask.value}. Using clamped fallback.");
            return target;
        }

        /// <summary>
        /// Called by the owning client (CombatInputHandler.BeginFire / ThrowableAimController
        /// mobile ConfirmAim) to trigger ExecuteThrow on the server.
        /// FishNet ServerRpcs from the same client are ordered, so a prior
        /// item-selection RPC is guaranteed to complete
        /// before this one processes.
        /// </summary>
        // BUG 2 FIX: aimTarget is the client-side world position of the cursor.
        // The server cannot read ThrowableAimController.AimWorldTarget (client-only static);
        // passing it as a ServerRpc parameter sends the correct position to the server.
        [ServerRpc(RequireOwnership = true)]
        public void RequestExecuteThrow(Vector3 aimTarget)
        {
            LogThrowable($"RequestExecuteThrow RPC server received target={aimTarget:F2} current={_currentItem?.InstanceID ?? "null"} using={_isUsingItem} owner={Owner?.ClientId}");
            Debug.Log($"[NH_FLOW][37][Rpc.ExecuteThrow] target={aimTarget:F2} current={_currentItem?.InstanceID ?? "null"} using={_isUsingItem} owner={Owner?.ClientId}");
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
            Debug.Log($"[NH_FLOW][37][Rpc.CancelUse] current={_currentItem?.InstanceID ?? "null"} using={_isUsingItem} owner={Owner?.ClientId}");
            CancelUse();
        }

        [ObserversRpc]
        private void RpcThrowPrepareStarted()
        {
            OnThrowPrepareStarted?.Invoke();
        }

        [ObserversRpc]
        private void RpcThrowExecuted()
        {
            OnThrowExecuted?.Invoke();
        }

        [ObserversRpc]
        private void RpcDeployStarted()
        {
            OnDeployStarted?.Invoke();
        }

        [ObserversRpc]
        private void RpcDeployCompleted()
        {
            OnDeployCompleted?.Invoke();
        }

        public bool TryConfirmDeploy()
        {
            _deployableHandler ??= ComponentResolver.Find<IDeployableHandler>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();

            if (_deployableHandler == null)
            {
                Debug.LogWarning("[DEPLOY_FLOW] TryConfirmDeploy failed: IDeployableHandler missing on owner.");
                Debug.LogWarning($"[NH_FLOW][39][ItemUse.TryConfirmDeployRejected] reason=no-handler current={_currentItem?.InstanceID ?? "null"} using={_isUsingItem}");
                return false;
            }

            string instanceId = _currentItem?.InstanceID;
            bool confirmed = _deployableHandler.TryCapturePlacement(out Vector3 position, out Quaternion rotation);
            LogDeploy($"TryConfirmDeploy capture={confirmed} item={instanceId ?? "null"} using={_isUsingItem} pos={position:F2} rot={rotation.eulerAngles:F1}");
            Debug.Log($"[NH_FLOW][39][ItemUse.TryConfirmDeploy] capture={confirmed} item={instanceId ?? "null"} using={_isUsingItem} pos={position:F2} rot={rotation.eulerAngles:F1}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "TryConfirmDeploy",
                $"capture={confirmed} item={instanceId ?? "null"} using={_isUsingItem} pos={position:F2} rot={rotation.eulerAngles:F1}",
                this);

            if (confirmed && !string.IsNullOrEmpty(instanceId))
            {
                if (IsServerInitialized)
                    BeginConfirmedDeployServer(instanceId, position, rotation);
                else
                    RequestConfirmDeployUseServerRpc(instanceId, position, rotation);
            }

            return confirmed;
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestConfirmDeployUseServerRpc(string instanceId, Vector3 position, Quaternion rotation)
            => BeginConfirmedDeployServer(instanceId, position, rotation);

        [Server]
        private bool BeginConfirmedDeployServer(string instanceId, Vector3 position, Quaternion rotation)
        {
            if (!_isUsingItem || _currentItem == null || _currentItem.InstanceID != instanceId)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] BeginConfirmedDeploy ignored: requested={instanceId} current={_currentItem?.InstanceID ?? "null"} using={_isUsingItem}");
                Debug.LogWarning($"[NH_FLOW][41][ItemUse.ConfirmedDeployRejected] reason=current-mismatch requested={instanceId} current={_currentItem?.InstanceID ?? "null"} using={_isUsingItem}");
                return false;
            }

            if (_useCoroutine != null)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] BeginConfirmedDeploy ignored: deploy already in progress item={instanceId}");
                Debug.LogWarning($"[NH_FLOW][41][ItemUse.ConfirmedDeployRejected] reason=already-in-progress item={instanceId}");
                return false;
            }

            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            if (def == null || def.Type != ItemType.Deployable)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] BeginConfirmedDeploy ignored: invalid def={_currentItem.DefinitionID}");
                Debug.LogWarning($"[NH_FLOW][41][ItemUse.ConfirmedDeployRejected] reason=invalid-def item={instanceId} def={_currentItem.DefinitionID}");
                return false;
            }

            float duration = ResolveDeployDuration(def);
            LogDeploy($"BeginConfirmedDeploy accepted: item={instanceId} def={def.ItemID} duration={duration:F2} pos={position:F2} rot={rotation.eulerAngles:F1}");
            Debug.Log($"[NH_FLOW][41][ItemUse.ConfirmedDeploy] item={instanceId} def={def.ItemID} duration={duration:F2} pos={position:F2} rot={rotation.eulerAngles:F1}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "BeginConfirmedDeploy",
                $"item={instanceId} def={def.ItemID} duration={duration:F2} pos={position:F2} rot={rotation.eulerAngles:F1}",
                this);
            RpcDeployStarted();
            OnItemUseProgress?.Invoke(_currentItem, 0f);
            _useCoroutine = StartCoroutine(DeployAfterUseDuration(_currentItem, def, position, rotation, duration));
            return true;
        }

        private IEnumerator DeployAfterUseDuration(ItemInstance item, ItemDefinition def, Vector3 position, Quaternion rotation, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!_isUsingItem || _currentItem == null || _currentItem.InstanceID != item.InstanceID)
                    yield break;

                elapsed += Time.deltaTime;
                OnItemUseProgress?.Invoke(item, Mathf.Clamp01(duration > 0f ? elapsed / duration : 1f));
                yield return null;
            }

            if (!_isUsingItem || _currentItem == null || _currentItem.InstanceID != item.InstanceID)
                yield break;

            _deployableHandler ??= ComponentResolver.Find<IDeployableHandler>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();

            bool placed = _deployableHandler != null &&
                          _deployableHandler.PlaceDeployableServer(position, rotation, def.ItemID, item.InstanceID);

            LogDeploy($"DeployAfterUseDuration result={placed} item={item.InstanceID} def={def.ItemID} pos={position:F2}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "DeployComplete",
                $"result={placed} item={item.InstanceID} def={def.ItemID} pos={position:F2}",
                this);
            _useCoroutine = null;

            if (placed)
            {
                RpcDeployCompleted();
                CompleteUse(item);
            }
            else
                CancelUse();
        }

        private float ResolveDeployDuration(ItemDefinition def)
        {
            return def switch
            {
                DeployableDefinition deployable => Mathf.Max(0f, deployable.DeployDuration),
                BeaconDefinition beacon => Mathf.Max(0f, beacon.DeployDuration),
                _ => 0f
            };
        }

        /// <summary>
        /// Cancel in-progress use
        /// </summary>
        [Server]
        public void CancelUse()
        {
            if (!_isUsingItem)
            {
                Debug.Log($"[NH_FLOW][42][ItemUse.CancelIgnored] reason=not-using current={_currentItem?.InstanceID ?? "null"}");
                return;
            }

            // Check if cancellation allowed
            var def = ItemDatabase.GetDefinition(_currentItem?.DefinitionID);
            if (def != null && !def.CanCancelUsage)
            {
                Debug.Log($"[NH_FLOW][42][ItemUse.CancelRejected] reason=cannot-cancel item={_currentItem?.InstanceID ?? "null"} def={def.ItemID}");
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

            _deployableHandler?.CancelDeploy();
            TargetCancelDeploy(Owner);

            OnItemUseCancelled?.Invoke(item);
            TargetEndItemUseVisual(Owner);
            DestroyItemInHand();
            RestoreWeapon();
            Debug.Log($"[NH_FLOW][42][ItemUse.CancelUse] item={item?.InstanceID ?? "null"} def={item?.DefinitionID ?? "null"} restoreSlot={_previousWeaponSlot?.ToString() ?? "none"}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.ItemUse,
                "CancelUse",
                $"item={item?.InstanceID ?? "null"} def={item?.DefinitionID ?? "null"} restoreSlot={_previousWeaponSlot?.ToString() ?? "none"}",
                this);
        }

        [TargetRpc]
        private void TargetCancelDeploy(NetworkConnection conn)
        {
            var prevItem = _currentItem;
            _isUsingItem = false;
            _currentItem = null;

            _deployableHandler ??= ComponentResolver.Find<IDeployableHandler>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();

            _deployableHandler?.CancelDeploy();
            if (prevItem != null) OnItemUseCancelled?.Invoke(prevItem);
        }

        #endregion

        #region Consumable Flow

        private bool BeginConsumable(ItemInstance item, ConsumableDefinition def)
        {
            Debug.Log($"[ITEM_FLOW] [08][BeginConsumable] item={item.InstanceID} def={def.ItemID} duration={(def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime):F2}");
            Debug.Log($"[NH_FLOW][36][ItemUse.BeginConsumable] item={item.InstanceID} def={def.ItemID} duration={(def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime):F2}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.ItemUse,
                "BeginConsumable",
                $"item={item.InstanceID} def={def.ItemID} duration={(def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime):F2}",
                this);
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;

            // Show item in hand while consuming (uses EquippedPrefab as fallback)
            SpawnItemInHandGeneric(def);

            float duration = def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime;
            _useCoroutine = StartCoroutine(ConsumableRoutine(item, def, duration));

            OnItemUseStarted?.Invoke(item);
            TargetBeginHeldItem(Owner, item.InstanceID, item.DefinitionID);
            var visualPrefab = ItemVisualResolver.ResolveVisualPrefab(def);
            Debug.Log($"[ItemUseSystem] Consumable started: '{def.DisplayName}' ({duration:F1}s) " +
                      $"prefab={(visualPrefab != null ? visualPrefab.name : "runtime fallback")}");

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
            ApplyOwnerTargetedConsumableEffects(def);

            // Consume & complete
            ConsumeItem(item);
            CompleteUse(item);
        }

        [Server]
        private void ApplyOwnerTargetedConsumableEffects(ConsumableDefinition def)
        {
            if (!TryResolveRadarRevealDuration(def, out float duration))
                return;

            var ownerPlayer = ResolveOwnerNetworkPlayer();
            if (ownerPlayer == null)
            {
                Debug.LogWarning($"[ItemUseSystem] Radar reveal skipped for '{def.ItemID}': owner NetworkPlayer not found.");
                return;
            }

            var targetIds = new List<int>(16);
            CollectEnemyPlayerObjectIds(ownerPlayer, targetIds);

            TargetRevealEnemyPlayers(Owner, targetIds.ToArray(), duration);
            Debug.Log($"[ItemUseSystem] Radar reveal '{def.ItemID}' -> owner={ownerPlayer.DisplayName} targets={targetIds.Count} duration={duration:F1}s");
        }

        private static bool TryResolveRadarRevealDuration(ConsumableDefinition def, out float duration)
        {
            duration = 0f;
            var effects = def.GetEffects();
            if (effects == null)
                return false;

            for (int i = 0; i < effects.Length; i++)
            {
                var fx = effects[i];
                if (fx.EffectType != ConsumableEffectType.RevealEnemyPlayers)
                    continue;

                float effectDuration = fx.Duration > 0f ? fx.Duration : (fx.Value > 0f ? fx.Value : 5f);
                duration = Mathf.Max(duration, effectDuration);
            }

            return duration > 0f;
        }

        private NetworkPlayer ResolveOwnerNetworkPlayer()
        {
            var player = GetComponentInParent<NetworkPlayer>();
            if (player != null)
                return player;

            return GetComponentInChildren<NetworkPlayer>();
        }

        private static void CollectEnemyPlayerObjectIds(NetworkPlayer ownerPlayer, List<int> targetIds)
        {
            targetIds.Clear();
            int ownerTeamId = ownerPlayer.TeamId;
            int ownerObjectId = (int)ownerPlayer.ObjectId;

            var players = RegistryService.Instance?.GetAllPlayers();
            if (players != null && players.Length > 0)
            {
                for (int i = 0; i < players.Length; i++)
                    AddRevealTarget(ownerTeamId, ownerObjectId, players[i], targetIds);
                return;
            }

            var serverManager = InstanceFinder.ServerManager;
            if (serverManager?.Objects?.Spawned == null)
                return;

            foreach (var kvp in serverManager.Objects.Spawned)
            {
                var player = kvp.Value != null ? kvp.Value.GetComponent<NetworkPlayer>() : null;
                AddRevealTarget(ownerTeamId, ownerObjectId, player, targetIds);
            }
        }

        private static void AddRevealTarget(int ownerTeamId, int ownerObjectId, NetworkPlayer player, List<int> targetIds)
        {
            if (player == null || !player.IsAlive)
                return;

            int playerObjectId = (int)player.ObjectId;
            if (playerObjectId == ownerObjectId || player.TeamId == ownerTeamId)
                return;

            targetIds.Add(playerObjectId);
        }

        [TargetRpc]
        private void TargetRevealEnemyPlayers(NetworkConnection conn, int[] playerObjectIds, float duration)
        {
            var reveal = GetComponent<RadarSweepRevealClient>();
            if (reveal == null)
                reveal = gameObject.AddComponent<RadarSweepRevealClient>();

            reveal.ShowTargets(playerObjectIds, duration);
        }

        #endregion

        #region Throwable Flow

        private bool BeginThrowable(ItemInstance item, ThrowableDefinition def)
        {
            Debug.Log($"[ITEM_FLOW] [08][BeginThrowable] item={item.InstanceID} def={def.ItemID} prepare={def.PrepareTime:F2}");
            Debug.Log($"[NH_FLOW][36][ItemUse.BeginThrowable] item={item.InstanceID} def={def.ItemID} prepare={def.PrepareTime:F2}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Throwable,
                "BeginThrowable",
                $"item={item.InstanceID} def={def.ItemID} prepare={def.PrepareTime:F2}",
                this);
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;

            // Spawn item model in right-hand bone so the player visually holds it while aiming.
            SpawnItemInHand(def);

            OnItemUseStarted?.Invoke(item);
            TargetBeginHeldItem(Owner, item.InstanceID, item.DefinitionID);
            Debug.Log($"[ItemUseSystem] Throw mode: '{def.DisplayName}'. Press Fire to throw.");

            return true;
        }

        #endregion

        #region Deployable Flow

        /// <summary>
        /// Deployables enter client-side placement mode through IDeployableHandler.
        /// The handler owns preview, confirm/cancel input, server validation, spawn
        /// and inventory consumption. ItemUseSystem only routes the request.
        /// </summary>
        private bool BeginDeployable(ItemInstance item, ItemDefinition def)
        {
            LogDeploy($"BeginDeployable item={item.InstanceID} def={def.ItemID} handler={(_deployableHandler != null ? "ok" : "null")}");
            Debug.Log($"[NH_FLOW][36][ItemUse.BeginDeployable] item={item.InstanceID} def={def.ItemID} handler={(_deployableHandler != null ? "ok" : "null")}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "BeginDeployable",
                $"item={item.InstanceID} def={def.ItemID} handler={(_deployableHandler != null ? "ok" : "null")}",
                this);
            if (_deployableHandler == null)
            {
                Debug.LogWarning($"[ItemUseSystem] Deployable '{def.DisplayName}' selected but no IDeployableHandler is present on the player.");
                Debug.LogWarning($"[NH_FLOW][36][ItemUse.BeginDeployableRejected] reason=no-handler item={item.InstanceID} def={def.ItemID}");
                return false;
            }

            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;

            OnItemUseStarted?.Invoke(item);
            TargetBeginDeployable(Owner, item.InstanceID, item.DefinitionID);

            LogDeploy($"Deployable '{def.DisplayName}': entering placement mode item={item.InstanceID}");
            return true;
        }

        [TargetRpc]
        private void TargetBeginDeployable(NetworkConnection conn, string instanceId, string definitionId)
        {
            var item = _inventorySystem?.GetItemByInstanceID(instanceId);
            var def = ItemDatabase.GetDefinition(definitionId);
            if (item == null || def == null)
            {
                Debug.LogWarning($"[ItemUseSystem] TargetBeginDeployable failed: item={instanceId} def={definitionId}");
                Debug.LogWarning($"[NH_FLOW][43][Target.BeginDeployableRejected] reason=item-or-def-null item={instanceId} def={definitionId}");
                return;
            }

            _deployableHandler ??= ComponentResolver.Find<IDeployableHandler>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();

            if (_deployableHandler == null || !_deployableHandler.BeginDeploy(item, def))
            {
                Debug.LogWarning($"[ItemUseSystem] No deployable handler accepted '{def.DisplayName}'.");
                Debug.LogWarning($"[NH_FLOW][43][Target.BeginDeployableRejected] reason=handler-rejected item={item.InstanceID} def={def.ItemID} handler={(_deployableHandler != null ? "ok" : "null")}");
            }
            else
            {
                _currentItem = item;
                _isUsingItem = true;
                OnItemUseStarted?.Invoke(item);
                LogDeploy($"TargetBeginDeployable active: item={item.InstanceID} def={def.ItemID}");
                Debug.Log($"[NH_FLOW][43][Target.BeginDeployable] item={item.InstanceID} def={def.ItemID} handler={_deployableHandler.GetType().Name}");
            }
        }

        #endregion

        #region Hand Model

        [TargetRpc]
        private void TargetBeginHeldItem(NetworkConnection conn, string instanceId, string definitionId)
        {
            var item = _inventorySystem?.GetItemByInstanceID(instanceId);
            var def = ItemDatabase.GetDefinition(definitionId);
            if (item == null || def == null)
            {
                Debug.LogWarning($"[ItemUseSystem] TargetBeginHeldItem failed: item={instanceId} def={definitionId}");
                Debug.LogWarning($"[NH_FLOW][43][Target.BeginHeldItemRejected] item={instanceId} def={definitionId}");
                return;
            }

            _currentItem = item;
            _isUsingItem = true;
            OnItemUseStarted?.Invoke(item);
            SpawnItemInHandGeneric(def);
            Debug.Log($"[ITEM_FLOW] [09][TargetBeginHeldItem] item={item.InstanceID} def={def.ItemID}");
            Debug.Log($"[NH_FLOW][43][Target.BeginHeldItem] item={item.InstanceID} def={def.ItemID} type={def.Type}");
        }

        [TargetRpc]
        private void TargetEndItemUseVisual(NetworkConnection conn)
        {
            var prevItem = _currentItem;
            _isUsingItem = false;
            _currentItem = null;
            _deployableHandler?.CancelDeploy();
            if (prevItem != null) OnItemUseCompleted?.Invoke(prevItem);
            DestroyItemInHand();
            Debug.Log($"[NH_FLOW][44][Target.EndItemUseVisual] prevItem={prevItem?.InstanceID ?? "null"}");
        }

        /// <summary>
        /// Instantiate the item's <see cref="PhysicalItemDefinition.VisualPrefab"/> on the
        /// right-hand bone (WeaponR) so the player visually holds the item while in
        /// throw-aim mode.  Uses the same parent and local transform as WeaponModelController.
        /// </summary>
        private void SpawnItemInHand(ThrowableDefinition def)
        {
            SpawnItemInHandGeneric(def);
        }

        /// <summary>
        /// Generic hand-model spawner for consumables / throwables / deployables.
        /// </summary>
        private void SpawnItemInHandGeneric(ItemDefinition def)
        {
            DestroyItemInHand();
            GameObject prefab = ItemVisualResolver.ResolveVisualPrefab(def);
            Transform parent = ResolveHandBone();
            _itemInHandModel = prefab != null
                ? Instantiate(prefab, parent)
                : ItemVisualResolver.CreateRuntimeFallback(def, ItemVisualPurpose.Held);
            if (_itemInHandModel.transform.parent != parent)
                _itemInHandModel.transform.SetParent(parent, worldPositionStays: false);
            _itemInHandModel.transform.localPosition = Vector3.zero;
            _itemInHandModel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PhaseTestLog.Log(
                PhaseTestLogCategory.IK,
                "HeldItemSpawned",
                $"def={def?.ItemID ?? "null"} prefab={(prefab != null ? prefab.name : "runtime-fallback")} parent={parent.name} path={BuildPath(parent)} localPos={_itemInHandModel.transform.localPosition:F3} localRot={_itemInHandModel.transform.localEulerAngles:F1}",
                this);
        }

        /// <summary>
        /// Returns the right-hand weapon bone (WeaponR) to attach held-item models.
        /// Preference order:
        ///   1. PrActorUtils.WeaponR assigned in the Inspector.
        ///   2. Deep-child search by common bone names (covers rig variations).
        ///   3. This component's own transform as a last resort (item appears at origin).
        /// </summary>
        private Transform ResolveHandBone()
        {
            if (_actorUtils?.WeaponR != null)
                return _actorUtils.WeaponR;

            // Search the full character hierarchy for the weapon-hand bone by name.
            string[] candidateNames = { "WeaponR", "Weapon_R", "Hand_R", "RightHandItem", "R_Weapon", "WeaponRight" };
            Transform root = transform.root;
            foreach (string boneName in candidateNames)
            {
                Transform found = FindDeepChildByName(root, boneName);
                if (found != null)
                {
                    Debug.LogWarning($"[ItemUseSystem] PrActorUtils.WeaponR was null — resolved hand bone '{boneName}' by name. " +
                                     "Assign WeaponR in PrActorUtils to suppress this search.");
                    return found;
                }
            }

            Debug.LogWarning("[ItemUseSystem] WeaponR bone not found via PrActorUtils or name search. " +
                             "Item will appear at player root. Assign PrActorUtils.WeaponR in the prefab.");
            return transform;
        }

        private static Transform FindDeepChildByName(Transform root, string boneName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t.name == boneName)
                    return t;
            }
            return null;
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
            // reading ThrowableAimController.AimWorldTarget which is a client-only static and
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
            {
                Debug.Log($"[ITEM_FLOW] [08][HolsterWeapon] previousSlot={_previousWeaponSlot.Value}");
                _weaponSystem.HolsterWeapon();
            }
            else
            {
                Debug.Log("[ITEM_FLOW] [08][HolsterWeapon] previousSlot=none");
            }
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
            TargetEndItemUseVisual(Owner);
            DestroyItemInHand();
            RestoreWeapon();
            Debug.Log($"[NH_FLOW][44][ItemUse.CompleteUse] item={item?.InstanceID ?? "null"} def={item?.DefinitionID ?? "null"} restoreSlot={_previousWeaponSlot?.ToString() ?? "none"}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.ItemUse,
                "CompleteUse",
                $"item={item?.InstanceID ?? "null"} def={item?.DefinitionID ?? "null"} restoreSlot={_previousWeaponSlot?.ToString() ?? "none"}",
                this);
        }

        private static string BuildPath(Transform target)
        {
            if (target == null)
                return "null";

            string path = target.name;
            Transform cursor = target.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
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

        private static bool ThrowableDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableThrowableDebugLogs;
        }

        private static bool DeployDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableDeployableDebugLogs;
        }

        private static void LogThrowable(string message)
        {
            if (ThrowableDebugEnabled())
                Debug.Log($"[THROW_FLOW] {message}");
        }

        private static void LogDeploy(string message)
        {
            if (DeployDebugEnabled())
                Debug.Log($"[DEPLOY_FLOW] {message}");
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
