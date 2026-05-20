using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using NightHunt.Core;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Aim;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using NightHunt.Diagnostics;
using UnityEngine;

namespace NightHunt.Gameplay.Beacon
{
    /// <summary>
    /// Placement controller for deployable items. Beacon definitions keep their
    /// existing BeaconManager spawn path; other DeployableDefinition assets spawn their
    /// NetworkDeployablePrefab here.
    /// Attach to the player root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeployablePlacementHandler : NetworkBehaviour, IDeployableHandler
    {
        [Header("References")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private NetworkPlayer _networkPlayer;

        [Header("Fallback Settings")]
        [Tooltip("Fallback only when AimSystem/VisionRange stat is unavailable.")]
        [SerializeField] private float _fallbackPlacementDistance = 3f;
        [SerializeField] private float _placementCheckRadius = 0.5f;
        [SerializeField] private LayerMask _placementBlockerMask = 0;
        [SerializeField, Min(0.05f)] private float _previewDebugLogInterval = 0.25f;

        private ItemDefinition _activeDefinition;
        private string _activeItemInstanceId;
        private GameObject _previewInstance;
        private bool _isInPlacementMode;
        private bool _placementValid;
        private bool _placementLocked;
        private Vector3 _capturedPlacementPoint;
        private Quaternion _capturedPlacementRotation = Quaternion.identity;
        private string _lastPlacementRejectReason;
        private Collider _lastBlockingCollider;
        private readonly Collider[] _placementOverlapBuffer = new Collider[24];
        private readonly RaycastHit[] _surfaceHits = new RaycastHit[16];
        private const float MaxPlacementSurfaceHeightAbovePlayer = 2.5f;
        private float _nextPreviewDebugLogTime;

        private IInventorySystem _inventorySystem;
        private IAimSystem _aimSystem;
        private IPlayerStatSystem _statSystem;
        private CharacterLifecycleController _lifecycle;

        public bool IsDeploying => _isInPlacementMode;

        private void Awake()
        {
            if (_networkPlayer == null)
                _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] NetworkPlayer not found")
                    .Resolve();

            ResolveCamera();

            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IInventorySystem not found")
                .Resolve();

            ResolveAimSystem();

            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrDefault(null)
                .Resolve();

            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CharacterLifecycleController not found")
                .Resolve();

            if (_lifecycle != null)
                _lifecycle.OnDied += OnPlayerDied;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ResolveCamera();
            ResolveAimSystem();
        }

        private void Update()
        {
            if (!IsOwner || !_isInPlacementMode)
                return;

            UpdatePreviewPosition();
        }

        private void OnDestroy()
        {
            StopPlacement();
            if (_lifecycle != null)
                _lifecycle.OnDied -= OnPlayerDied;
        }

        public bool BeginDeploy(ItemInstance item, ItemDefinition def)
        {
            if (item == null)
            {
                Debug.LogWarning("[DEPLOY_FLOW] BeginDeploy rejected: item is null.");
                PhaseTestLog.Warning(PhaseTestLogCategory.Deploy, "BeginDeployRejected", "reason=item-null", this);
                return false;
            }

            if (def is not BeaconDefinition && def is not DeployableDefinition)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] BeginDeploy rejected: unsupported definition type={def?.GetType().Name ?? "null"} item={item?.InstanceID ?? "null"}");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "BeginDeployRejected",
                    $"reason=unsupported-def defType={def?.GetType().Name ?? "null"} item={item.InstanceID}",
                    this);
                return false;
            }

            LogDeploy($"BeginDeploy accepted: def={def.ItemID} item={item?.InstanceID ?? "null"} owner={IsOwner}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "BeginDeploy",
                $"def={def.ItemID} defType={def.GetType().Name} item={item.InstanceID} owner={IsOwner}",
                this);
            StartPlacement(def, item.InstanceID);
            return true;
        }

        public void CancelDeploy() => StopPlacement();

        public bool ConfirmDeploy()
        {
            if (!TryCapturePlacement(out Vector3 point, out Quaternion rotation))
                return false;

            LogDeploy($"ConfirmDeploy legacy request: point={point:F2} rot={rotation.eulerAngles:F1} def={_activeDefinition?.ItemID ?? "null"} item={_activeItemInstanceId ?? "null"}");
            RequestPlacement(point, rotation);
            return true;
        }

        public bool TryCapturePlacement(out Vector3 point, out Quaternion rotation)
        {
            point = default;
            rotation = Quaternion.identity;

            if (!IsOwner || !_isInPlacementMode)
            {
                LogDeploy($"TryCapturePlacement skipped: owner={IsOwner} active={_isInPlacementMode} def={_activeDefinition?.ItemID ?? "null"}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Deploy,
                    "TryCapturePlacementSkipped",
                    $"owner={IsOwner} active={_isInPlacementMode} def={_activeDefinition?.ItemID ?? "null"}",
                    this);
                return false;
            }

            if (!TryGetPlacementPoint(out point, out rotation, out bool valid) || !valid)
            {
                LogDeploy($"TryCapturePlacement rejected: refreshedValid={valid} cachedValid={_placementValid} def={_activeDefinition?.ItemID ?? "null"} point={point:F2} reason={_lastPlacementRejectReason ?? "unknown"}");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "TryCapturePlacementRejected",
                    $"valid={valid} cachedValid={_placementValid} def={_activeDefinition?.ItemID ?? "null"} point={point:F2} reason={_lastPlacementRejectReason ?? "unknown"} blocker={(_lastBlockingCollider != null ? _lastBlockingCollider.name : "null")}",
                    this);
                return false;
            }

            _placementLocked = true;
            _capturedPlacementPoint = point;
            _capturedPlacementRotation = rotation;
            if (_previewInstance != null)
                _previewInstance.transform.SetPositionAndRotation(point, rotation);

            LogDeploy($"TryCapturePlacement accepted: point={point:F2} rot={rotation.eulerAngles:F1} def={_activeDefinition?.ItemID ?? "null"} item={_activeItemInstanceId ?? "null"}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "TryCapturePlacementAccepted",
                $"def={_activeDefinition?.ItemID ?? "null"} item={_activeItemInstanceId ?? "null"} point={point:F2} rot={rotation.eulerAngles:F1}",
                this);
            return true;
        }

        private void StartPlacement(ItemDefinition definition, string itemInstanceId)
        {
            if (!IsOwner || definition == null)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] StartPlacement skipped: IsOwner={IsOwner} definition={definition?.ItemID ?? "null"}");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "StartPlacementSkipped",
                    $"owner={IsOwner} def={definition?.ItemID ?? "null"}",
                    this);
                return;
            }

            if (_isInPlacementMode)
            {
                LogDeploy("StartPlacement: existing placement mode active, canceling old preview first.");
                StopPlacement();
            }

            _activeDefinition = definition;
            _activeItemInstanceId = itemInstanceId;
            _isInPlacementMode = true;
            _placementLocked = false;
            _nextPreviewDebugLogTime = 0f;
            _aimSystem?.SetCursorVisible(true);

            GameObject previewPrefab = ResolvePlacementPreview(definition);
            if (previewPrefab != null)
            {
                _previewInstance = Instantiate(previewPrefab);
                PreparePreviewInstance(definition, previewPrefab.name);
                LogDeploy($"Preview spawned: def={definition.ItemID} preview={previewPrefab.name} range={ResolvePlacementDistance(definition):F2}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Deploy,
                    "DeployPreviewSpawned",
                    $"def={definition.ItemID} preview={previewPrefab.name} range={ResolvePlacementDistance(definition):F2}",
                    this);
            }
            else
            {
                _previewInstance = CreateRuntimePlacementPreview(definition);
                PreparePreviewInstance(definition, _previewInstance != null ? _previewInstance.name : "null");
                Debug.LogWarning($"[DEPLOY_FLOW] Preview missing: def={definition.ItemID}. Using runtime fallback placement preview.");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "DeployPreviewFallback",
                    $"def={definition.ItemID} preview={_previewInstance?.name ?? "null"}",
                    this);
            }
        }

        private void StopPlacement()
        {
            if (_isInPlacementMode)
            {
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Deploy,
                    "StopPlacement",
                    $"def={_activeDefinition?.ItemID ?? "null"} item={_activeItemInstanceId ?? "null"} locked={_placementLocked} valid={_placementValid}",
                    this);
            }

            _isInPlacementMode = false;
            _activeDefinition = null;
            _activeItemInstanceId = null;
            _placementValid = false;
            _placementLocked = false;
            _capturedPlacementPoint = Vector3.zero;
            _capturedPlacementRotation = Quaternion.identity;

            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }

            if (Application.isMobilePlatform)
                _aimSystem?.SetCursorVisible(false);
        }

        private void ResolveCamera()
        {
            if (_cameraTransform != null)
                return;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        private void ResolveAimSystem()
        {
            if (_aimSystem != null)
                return;

            _aimSystem = ComponentResolver.Find<IAimSystem>(this)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .OrDefault(null)
                .Resolve();

            if (_aimSystem == null)
            {
#if UNITY_2023_1_OR_NEWER
                _aimSystem = FindFirstObjectByType<AimSystem>();
#else
                _aimSystem = FindObjectOfType<AimSystem>();
#endif
            }

            LogDeploy(_aimSystem != null
                ? $"AimSystem resolved: type={_aimSystem.GetType().Name}"
                : "AimSystem not resolved; placement will use mouse camera ray fallback.");
        }

        private void OnPlayerDied() => StopPlacement();

        private void UpdatePreviewPosition()
        {
            if (_previewInstance == null)
            {
                return;
            }

            if (_placementLocked)
            {
                _previewInstance.transform.SetPositionAndRotation(_capturedPlacementPoint, _capturedPlacementRotation);
                return;
            }

            if (!TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out bool valid))
                return;

            _previewInstance.transform.SetPositionAndRotation(point, rotation);
            _placementValid = valid;
            LogPreviewPosition(point, rotation, valid);

            Color tint = valid
                ? new Color(0.3f, 1f, 0.3f, 0.5f)
                : new Color(1f, 0.3f, 0.3f, 0.5f);

            foreach (var r in _previewInstance.GetComponentsInChildren<Renderer>())
                r.material.color = tint;
        }

        private bool TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out bool valid)
        {
            float distance = ResolvePlacementDistance(_activeDefinition);
            LayerMask surfaceMask = ResolvePlacementSurfaceMask(_activeDefinition);
            LayerMask blockerMask = ResolvePlacementBlockerMask();

            if (_aimSystem != null)
            {
                Vector3 aimPoint = ResolveAimPlacementPoint();
                Vector3 rayOrigin = aimPoint + Vector3.up * 8f;
                if (TryRaycastPlacementSurface(rayOrigin, Vector3.down, 20f, surfaceMask, out RaycastHit aimHit))
                {
                    Vector3 forward = ResolveAimPlacementForward(aimHit.point);
                    BuildPlacementResult(aimHit, forward, blockerMask, out point, out rotation, out valid);
                    return true;
                }

                point = aimPoint;
                Vector3 aimForward = ResolveAimPlacementForward(aimPoint);
                rotation = aimForward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aimForward) : Quaternion.identity;
                rotation *= Quaternion.Euler(0f, ResolvePlacementYawOffset(_activeDefinition), 0f);
                valid = false;
                _lastPlacementRejectReason = $"surfaceMiss aimPoint={aimPoint:F2} rayOrigin={rayOrigin:F2} surfaceMask={surfaceMask.value}";
                return true;
            }

            if (_cameraTransform == null)
            {
                point = transform.position;
                rotation = Quaternion.identity;
                valid = false;
                return false;
            }

            UnityEngine.Camera cam = _cameraTransform.GetComponent<UnityEngine.Camera>() ?? UnityEngine.Camera.main;
            Ray ray = cam != null
                ? cam.ScreenPointToRay(UnityEngine.Input.mousePosition)
                : new Ray(_cameraTransform.position, _cameraTransform.forward);

            if (TryRaycastPlacementSurface(ray.origin, ray.direction, distance * 2f, surfaceMask, out RaycastHit hit))
            {
                Vector3 forward = _networkPlayer != null
                    ? Vector3.ProjectOnPlane(hit.point - _networkPlayer.transform.position, Vector3.up)
                    : Vector3.ProjectOnPlane(ray.direction, Vector3.up);
                BuildPlacementResult(hit, forward, blockerMask, out point, out rotation, out valid);
                return true;
            }

            point = ray.origin + ray.direction * distance;
            rotation = Quaternion.identity;
            valid = false;
            _lastPlacementRejectReason = $"cursorSurfaceMiss origin={ray.origin:F2} dir={ray.direction:F2} mouse={UnityEngine.Input.mousePosition:F0} surfaceMask={surfaceMask.value}";
            return true;
        }

        private bool TryRaycastPlacementSurface(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            LayerMask surfaceMask,
            out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(
                origin,
                direction,
                _surfaceHits,
                maxDistance,
                surfaceMask,
                QueryTriggerInteraction.Ignore);

            if (count <= 0)
            {
                hit = default;
                return false;
            }

            System.Array.Sort(_surfaceHits, 0, count, RaycastHitDistanceComparer.Instance);
            float baseY = _networkPlayer != null ? _networkPlayer.transform.position.y : transform.position.y;
            float maxSlope = ResolveMaxSlope(_activeDefinition);
            for (int i = 0; i < count; i++)
            {
                Collider col = _surfaceHits[i].collider;
                if (col == null)
                    continue;

                if (_previewInstance != null && col.transform.IsChildOf(_previewInstance.transform))
                    continue;

                if (_networkPlayer != null && col.transform.IsChildOf(_networkPlayer.transform))
                    continue;

                float slope = Vector3.Angle(_surfaceHits[i].normal, Vector3.up);
                if (slope > maxSlope)
                {
                    _lastPlacementRejectReason = $"surfaceSkipSteep surface={col.name} slope={slope:F1} max={maxSlope:F1}";
                    continue;
                }

                if (_surfaceHits[i].point.y > baseY + MaxPlacementSurfaceHeightAbovePlayer)
                {
                    _lastPlacementRejectReason = $"surfaceSkipHigh surface={col.name} point={_surfaceHits[i].point:F2} playerY={baseY:F2}";
                    continue;
                }

                hit = _surfaceHits[i];
                return true;
            }

            hit = default;
            return false;
        }

        private void BuildPlacementResult(
            RaycastHit hit,
            Vector3 forward,
            LayerMask overlapMask,
            out Vector3 point,
            out Quaternion rotation,
            out bool valid)
        {
            point = hit.point;
            rotation = forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward)
                : Quaternion.identity;
            rotation *= Quaternion.Euler(0f, ResolvePlacementYawOffset(_activeDefinition), 0f);

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            bool slopeOk = slope <= ResolveMaxSlope(_activeDefinition);
            bool noOverlap = !HasBlockingOverlap(
                point,
                ResolveCheckRadius(_activeDefinition),
                overlapMask,
                hit.collider);
            valid = slopeOk && noOverlap;
            _lastPlacementRejectReason = valid
                ? "valid"
                : $"slopeOk={slopeOk} slope={slope:F1} noOverlap={noOverlap} blocker={(_lastBlockingCollider != null ? _lastBlockingCollider.name : "none")} surface={hit.collider?.name ?? "null"}";
        }

        private Vector3 ResolveAimPlacementPoint()
        {
            if (_aimSystem == null)
                return transform.position;

            if (_aimSystem.IsThrowableMode && ItemAimController.AimWorldTarget.sqrMagnitude > 0.0001f)
                return ItemAimController.AimWorldTarget;

            return _aimSystem.FinalAimGroundPos;
        }

        private Vector3 ResolveAimPlacementForward(Vector3 placementPoint)
        {
            Vector3 forward = Vector3.zero;

            if (_aimSystem != null)
                forward = Vector3.ProjectOnPlane(_aimSystem.FinalAimDir, Vector3.up);

            if (_aimSystem != null && _aimSystem.IsThrowableMode && _networkPlayer != null)
                forward = Vector3.ProjectOnPlane(placementPoint - _networkPlayer.transform.position, Vector3.up);

            if (forward.sqrMagnitude <= 0.0001f && _networkPlayer != null)
                forward = Vector3.ProjectOnPlane(placementPoint - _networkPlayer.transform.position, Vector3.up);

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        private void RequestPlacement(Vector3 position, Quaternion rotation)
        {
            string instanceId = _activeItemInstanceId;
            string definitionId = _activeDefinition?.ItemID;

            StopPlacement();
            LogDeploy($"Sending CmdRequestPlaceDeployable def={definitionId} item={instanceId} pos={position:F2} rot={rotation.eulerAngles:F1}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "RequestPlaceDeployable",
                $"def={definitionId ?? "null"} item={instanceId ?? "null"} pos={position:F2} rot={rotation.eulerAngles:F1}",
                this);
            CmdRequestPlaceDeployable(position, rotation, definitionId, instanceId);
        }

        [ServerRpc(RequireOwnership = true)]
        private void CmdRequestPlaceDeployable(
            Vector3 position,
            Quaternion rotation,
            string definitionId,
            string itemInstanceId)
            => PlaceDeployableServer(position, rotation, definitionId, itemInstanceId);

        [Server]
        public bool PlaceDeployableServer(
            Vector3 position,
            Quaternion rotation,
            string definitionId,
            string itemInstanceId)
        {
            if (_networkPlayer == null || string.IsNullOrEmpty(definitionId))
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: networkPlayer={(_networkPlayer != null ? "ok" : "null")} definitionId='{definitionId}'");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "PlaceDeployableRejected",
                    $"reason=missing-player-or-def player={(_networkPlayer != null ? _networkPlayer.DisplayName : "null")} def={definitionId ?? "null"} item={itemInstanceId ?? "null"}",
                    this);
                return false;
            }

            if (!ValidateHeldItem(itemInstanceId, definitionId))
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: held item validation failed def={definitionId} item={itemInstanceId}");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "PlaceDeployableRejected",
                    $"reason=held-item-validation def={definitionId} item={itemInstanceId ?? "null"}",
                    this);
                return false;
            }

            var def = ItemDatabase.GetDefinition(definitionId);
            if (!ValidateServerPlacement(def, position))
            {
                Debug.LogWarning($"[DeployablePlacementHandler] Placement rejected by server validation. def={definitionId} pos={position:F2}");
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Deploy,
                    "PlaceDeployableRejected",
                    $"reason=server-placement def={definitionId} item={itemInstanceId ?? "null"} pos={position:F2}",
                    this);
                return false;
            }

            bool placed = def switch
            {
                BeaconDefinition => TryPlaceBeacon(position, rotation, definitionId),
                DeployableDefinition deployable => TryPlaceNetworkDeployable(deployable, position, rotation, definitionId),
                _ => false
            };

            if (!placed || string.IsNullOrEmpty(itemInstanceId))
            {
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Deploy,
                    "PlaceDeployableResult",
                    $"result={placed} consume=false def={definitionId} item={itemInstanceId ?? "null"} pos={position:F2}",
                    this);
                return placed;
            }

            ResolveInventory()?.RemoveItem(itemInstanceId, 1);
            LogDeploy($"Consumed deployable item {definitionId} instance={itemInstanceId} pos={position:F2}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Deploy,
                "PlaceDeployableResult",
                $"result=true consume=true def={definitionId} item={itemInstanceId} pos={position:F2}",
                this);
            return true;
        }

        private bool ValidateHeldItem(string itemInstanceId, string definitionId)
        {
            if (string.IsNullOrEmpty(itemInstanceId))
                return true;

            var held = ResolveInventory()?.GetItemByInstanceID(itemInstanceId);
            if (held == null || held.DefinitionID != definitionId)
            {
                Debug.LogWarning($"[DeployablePlacementHandler] Placement rejected. Missing/mismatched item instance={itemInstanceId} def={definitionId}.");
                return false;
            }

            return true;
        }

        private IInventorySystem ResolveInventory()
        {
            return _inventorySystem ??= ComponentResolver.Find<IInventorySystem>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IInventorySystem not found")
                .Resolve();
        }

        private bool TryPlaceBeacon(Vector3 position, Quaternion rotation, string definitionId)
        {
            var manager = BeaconManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[DeployablePlacementHandler] BeaconManager not found.");
                return false;
            }

            return manager.TryPlaceBeacon(_networkPlayer.TeamId, position, rotation, Owner, definitionId);
        }

        private bool TryPlaceNetworkDeployable(
            DeployableDefinition def,
            Vector3 position,
            Quaternion rotation,
            string definitionId)
        {
            if (def.NetworkDeployablePrefab == null)
            {
                Debug.LogError($"[DeployablePlacementHandler] {definitionId} missing NetworkDeployablePrefab.");
                return false;
            }

            var go = Instantiate(def.NetworkDeployablePrefab, position, rotation);
            var deployable = go.GetComponent<BaseDeployable>();
            if (deployable == null)
            {
                Debug.LogError($"[DeployablePlacementHandler] {definitionId} prefab must contain BaseDeployable.");
                Destroy(go);
                return false;
            }

            int ownerNetworkObjectId = _networkPlayer != null ? (int)_networkPlayer.ObjectId : 0;
            if (!InitializeNetworkDeployable(deployable, def, definitionId, ownerNetworkObjectId))
            {
                Destroy(go);
                return false;
            }

            InstanceFinder.ServerManager.Spawn(go, Owner);
            deployable.StartPlacement();

            LogDeploy($"Placed deployable {definitionId} kind={def.DeployableKind} at {position:F2} owner={Owner?.ClientId} lifecycle=Initialize>Spawn>StartPlacement");
            return true;
        }

        [Server]
        private bool InitializeNetworkDeployable(
            BaseDeployable deployable,
            DeployableDefinition def,
            string definitionId,
            int ownerNetworkObjectId)
        {
            int maxHpOverride = def.OverridePrefabHealth ? def.MaxHP : 0;

            if (IsTrapKind(def.DeployableKind) && deployable is not TrapDeployable)
            {
                Debug.LogError($"[DeployablePlacementHandler] {definitionId} kind={def.DeployableKind} requires TrapDeployable on prefab '{deployable.name}'.");
                return false;
            }

            if (IsVisionKind(def.DeployableKind) && deployable is not VisionWard)
            {
                Debug.LogError($"[DeployablePlacementHandler] {definitionId} kind={def.DeployableKind} requires VisionWard on prefab '{deployable.name}'.");
                return false;
            }

            if (deployable is VisionWard visionWard)
            {
                visionWard.InitializeWithRadius(
                    _networkPlayer.TeamId,
                    maxHpOverride,
                    def.VisionRadius > 0f ? def.VisionRadius : 0f,
                    ownerNetworkObjectId);
                return true;
            }

            if (deployable is TrapDeployable trap)
            {
                trap.Initialize(_networkPlayer.TeamId, maxHpOverride, def.DeployableKind, definitionId, ownerNetworkObjectId);
                return true;
            }

            if (deployable is SimpleDeployable simple)
                simple.SetDefinitionId(definitionId);

            deployable.Initialize(_networkPlayer.TeamId, maxHpOverride, ownerNetworkObjectId);
            return true;
        }

        private static bool IsTrapKind(DeployableKind kind)
            => kind == DeployableKind.ExplosiveMine || kind == DeployableKind.ShockField;

        private static bool IsVisionKind(DeployableKind kind)
            => kind == DeployableKind.VisionNode || kind == DeployableKind.LightPoint;

        private static GameObject ResolvePlacementPreview(ItemDefinition def)
        {
            return def switch
            {
                BeaconDefinition beacon => beacon.PlacementPreviewPrefab != null
                    ? beacon.PlacementPreviewPrefab
                    : ItemVisualResolver.ResolveVisualPrefab(beacon),
                DeployableDefinition deployable => deployable.PlacementPreviewPrefab != null
                    ? deployable.PlacementPreviewPrefab
                    : ItemVisualResolver.ResolveVisualPrefab(deployable),
                _ => ItemVisualResolver.ResolveVisualPrefab(def)
            };
        }

        private GameObject CreateRuntimePlacementPreview(ItemDefinition def)
        {
            float radius = Mathf.Max(0.15f, ResolveCheckRadius(def));
            var preview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            preview.name = $"RuntimePlacementPreview_{def?.ItemID ?? "Unknown"}";
            preview.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);

            var collider = preview.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = preview.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Standard");

                if (shader != null)
                {
                    var material = new Material(shader);
                    material.color = new Color(0.3f, 1f, 0.3f, 0.35f);
                    renderer.material = material;
                }
            }

            return preview;
        }

        private void PreparePreviewInstance(ItemDefinition def, string sourceName)
        {
            if (_previewInstance == null)
                return;

            int colliderCount = 0;
            foreach (var collider in _previewInstance.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (collider == null)
                    continue;

                collider.enabled = false;
                colliderCount++;
            }

            EnsurePreviewFootprintMarker(def);

            var renderers = _previewInstance.GetComponentsInChildren<Renderer>(includeInactive: true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                if (!r.gameObject.activeSelf)
                    r.gameObject.SetActive(true);
                if (!r.enabled)
                    r.enabled = true;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            string boundsSize = hasBounds ? bounds.size.ToString("F2") : "none";
            LogDeploy($"Preview prepared: def={def?.ItemID ?? "null"} source={sourceName} renderers={renderers.Length} bounds={boundsSize} collidersDisabled={colliderCount} marker=PlacementPreviewMarker");
        }

        private void EnsurePreviewFootprintMarker(ItemDefinition def)
        {
            if (_previewInstance == null ||
                _previewInstance.transform.Find("PlacementPreviewMarker") != null)
                return;

            float radius = Mathf.Max(0.25f, ResolveCheckRadius(def));
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "PlacementPreviewMarker";
            marker.transform.SetParent(_previewInstance.transform, false);
            marker.transform.localPosition = Vector3.up * 0.02f;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = new Vector3(radius * 2f, 0.025f, radius * 2f);

            var collider = marker.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = marker.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader == null)
                return;

            var material = new Material(shader);
            material.color = new Color(0.3f, 1f, 0.3f, 0.5f);
            renderer.material = material;
        }

        private float ResolvePlacementDistance(ItemDefinition def)
        {
            float configured = def is DeployableDefinition deployable
                ? deployable.PlacementDistance
                : _fallbackPlacementDistance;

            float visionRange = 0f;
            if (_aimSystem != null)
                visionRange = _aimSystem.GetVisionRange();
            if (visionRange <= 0f && _statSystem != null)
                visionRange = _statSystem.GetStat(PlayerStatType.VisionRange);

            return visionRange > 0.1f ? visionRange : configured;
        }

        private float ResolveCheckRadius(ItemDefinition def)
            => def is DeployableDefinition deployable ? deployable.PlacementCheckRadius : _placementCheckRadius;

        private static LayerMask ResolvePlacementMask(ItemDefinition def)
            => def switch
            {
                BeaconDefinition beacon => beacon.PlacementLayerMask,
                DeployableDefinition deployable => deployable.PlacementLayerMask,
                _ => ~0
            };

        private static LayerMask ResolvePlacementSurfaceMask(ItemDefinition def)
        {
            LayerMask configured = ResolvePlacementMask(def);
            int surface = configured.value == 0 || configured.value == ~0
                ? NightHuntLayers.MaskPlacementSurface.value
                : configured.value | NightHuntLayers.MaskPlacementSurface.value;

            int nonSurface = LayerMask.GetMask(
                NightHuntLayers.Player,
                NightHuntLayers.PlayerHitBox,
                NightHuntLayers.Projectile,
                NightHuntLayers.Throwable,
                NightHuntLayers.Interactable,
                NightHuntLayers.Items,
                NightHuntLayers.DeadCharacter,
                NightHuntLayers.Zone);

            surface &= ~nonSurface;
            if (surface == 0)
                surface = NightHuntLayers.MaskPlacementSurface.value;

            LayerMask mask = default;
            mask.value = surface;
            return mask;
        }

        private LayerMask ResolvePlacementBlockerMask()
        {
            if (_placementBlockerMask.value != 0)
                return _placementBlockerMask;

            return LayerMask.GetMask(
                NightHuntLayers.Player,
                NightHuntLayers.PlayerHitBox,
                NightHuntLayers.Wall,
                NightHuntLayers.MapStatic,
                NightHuntLayers.MapObstacle,
                NightHuntLayers.DeadCharacter,
                NightHuntLayers.Throwable,
                NightHuntLayers.Zone);
        }

        private static float ResolveMaxSlope(ItemDefinition def)
            => def switch
            {
                BeaconDefinition beacon => beacon.MaxPlacementSlope,
                DeployableDefinition deployable => deployable.MaxPlacementSlope,
                _ => 30f
            };

        private static float ResolvePlacementYawOffset(ItemDefinition def)
            => def switch
            {
                BeaconDefinition beacon => beacon.PlacementYawOffsetDegrees,
                DeployableDefinition deployable => deployable.PlacementYawOffsetDegrees,
                _ => 0f
            };

        private bool ValidateServerPlacement(ItemDefinition def, Vector3 position)
        {
            if (def == null || _networkPlayer == null)
                return false;

            // Use VisionRange from StatSystem as the authoritative max distance on the server.
            // _aimSystem is client-only (null on server), so we must use _statSystem instead.
            float visionRange = _statSystem != null ? _statSystem.GetStat(PlayerStatType.VisionRange) : 0f;
            float configured  = def is DeployableDefinition dd ? dd.PlacementDistance : _fallbackPlacementDistance;
            float maxDistance = (visionRange > 0.1f ? visionRange : configured) + 0.75f;

            Vector3 fromPlayer = position - _networkPlayer.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > maxDistance * maxDistance)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: too far def={def?.ItemID ?? "null"} pos={position:F2} player={_networkPlayer.transform.position:F2} max={maxDistance:F2} visionRange={visionRange:F2}");
                return false;
            }

            LayerMask surfaceMask = ResolvePlacementSurfaceMask(def);
            LayerMask blockerMask = ResolvePlacementBlockerMask();
            float radius = ResolveCheckRadius(def);

            // Find the actual supporting surface near the client-requested point.
            Collider surface = null;
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > ResolveMaxSlope(def))
                {
                    Debug.LogWarning($"[DEPLOY_FLOW] Server reject: slope def={def.ItemID} slope={slope:F1} max={ResolveMaxSlope(def):F1} surface={hit.collider?.name ?? "null"}");
                    return false;
                }

                if (Vector3.Distance(hit.point, position) > 0.35f)
                {
                    Debug.LogWarning($"[DEPLOY_FLOW] Server reject: point not on surface def={def.ItemID} requested={position:F2} surfacePoint={hit.point:F2}");
                    return false;
                }

                surface = hit.collider;
            }
            else if (LayerMask.NameToLayer(NightHuntLayers.Ground) >= 0)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: surface ray miss def={def.ItemID} pos={position:F2} surfaceMask={surfaceMask.value}");
                return false;
            }

            bool blocked = HasBlockingOverlap(position, radius, blockerMask, surface);
            if (blocked)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: overlap def={def.ItemID} pos={position:F2} radius={radius:F2} blocker={(_lastBlockingCollider != null ? _lastBlockingCollider.name : "unknown")}");
                return false;
            }

            return true;
        }

        private bool HasBlockingOverlap(Vector3 point, float radius, LayerMask mask, Collider surfaceCollider)
        {
            _lastBlockingCollider = null;
            int count = Physics.OverlapSphereNonAlloc(
                point,
                radius,
                _placementOverlapBuffer,
                mask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider col = _placementOverlapBuffer[i];
                if (col == null)
                    continue;

                if (col == surfaceCollider)
                    continue;

                if (_previewInstance != null && col.transform.IsChildOf(_previewInstance.transform))
                    continue;

                if (_networkPlayer != null && col.transform.IsChildOf(_networkPlayer.transform))
                    continue;

                _lastBlockingCollider = col;
                return true;
            }

            return false;
        }

        private void LogPreviewPosition(Vector3 point, Quaternion rotation, bool valid)
        {
            if (!DeployDebugEnabled())
                return;

            if (Time.unscaledTime < _nextPreviewDebugLogTime)
                return;

            _nextPreviewDebugLogTime = Time.unscaledTime + Mathf.Max(0.05f, _previewDebugLogInterval);
            string aimRaw   = _aimSystem != null ? _aimSystem.AimWorldPoint.ToString("F2") : "null";
            string aimFinal = _aimSystem != null ? _aimSystem.FinalAimGroundPos.ToString("F2") : "null";
            float  statVision = _statSystem != null ? _statSystem.GetStat(PlayerStatType.VisionRange) : 0f;
            float  visionRange = _aimSystem != null ? _aimSystem.GetVisionRange() : (statVision > 0f ? statVision : _fallbackPlacementDistance);
            LogDeploy($"Preview point={point:F2} rot={rotation.eulerAngles:F1} valid={valid} aimRaw={aimRaw} aimGround={aimFinal} visionRange={visionRange:F2} reason={_lastPlacementRejectReason ?? "none"}");
        }

        private static bool DeployDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableDeployableDebugLogs;
        }

        private static void LogDeploy(string message)
        {
            if (DeployDebugEnabled())
                Debug.Log($"[DEPLOY_FLOW] {message}");
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
