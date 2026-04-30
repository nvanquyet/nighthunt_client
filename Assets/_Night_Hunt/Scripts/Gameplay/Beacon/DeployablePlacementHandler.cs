using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using NightHunt.Core;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Core.State;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using UnityEngine;

namespace NightHunt.Gameplay.Beacon
{
    /// <summary>
    /// Generic placement controller for deployable items. Beacon definitions keep their
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
        [SerializeField] private float _placementDistance = 3f;
        [SerializeField] private float _placementCheckRadius = 0.5f;
        [SerializeField] private LayerMask _placementBlockerMask = 0;

        private ItemDefinition _activeDefinition;
        private string _activeItemInstanceId;
        private GameObject _previewInstance;
        private bool _isInPlacementMode;
        private bool _placementValid;
        private readonly Collider[] _placementOverlapBuffer = new Collider[24];
        private readonly RaycastHit[] _surfaceHits = new RaycastHit[16];

        private IInventorySystem _inventorySystem;
        private IAimSystem _aimSystem;
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

            _aimSystem = ComponentResolver.Find<IAimSystem>(this)
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
                return false;
            }

            if (def is not BeaconDefinition && def is not DeployableDefinition)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] BeginDeploy rejected: unsupported definition type={def?.GetType().Name ?? "null"} item={item?.InstanceID ?? "null"}");
                return false;
            }

            Debug.Log($"[DEPLOY_FLOW] BeginDeploy accepted: def={def.ItemID} item={item?.InstanceID ?? "null"} owner={IsOwner}");
            StartPlacement(def, item.InstanceID);
            return true;
        }

        public void CancelDeploy() => StopPlacement();

        public bool ConfirmDeploy()
        {
            if (!IsOwner || !_isInPlacementMode)
            {
                Debug.Log($"[DEPLOY_FLOW] ConfirmDeploy skipped: owner={IsOwner} active={_isInPlacementMode} def={_activeDefinition?.ItemID ?? "null"}");
                return false;
            }

            if (!TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out bool valid) || !valid)
            {
                Debug.Log($"[DEPLOY_FLOW] ConfirmDeploy rejected: refreshedValid={valid} cachedValid={_placementValid} def={_activeDefinition?.ItemID ?? "null"} point={point:F2}");
                return false;
            }

            Debug.Log($"[DEPLOY_FLOW] ConfirmDeploy accepted: point={point:F2} def={_activeDefinition?.ItemID ?? "null"} item={_activeItemInstanceId ?? "null"}");
            RequestPlacement(point, rotation);
            return true;
        }

        private void StartPlacement(ItemDefinition definition, string itemInstanceId)
        {
            if (!IsOwner || definition == null)
            {
                Debug.LogWarning($"[DEPLOY_FLOW] StartPlacement skipped: IsOwner={IsOwner} definition={definition?.ItemID ?? "null"}");
                return;
            }

            if (_isInPlacementMode)
            {
                Debug.Log("[DEPLOY_FLOW] StartPlacement: existing placement mode active, canceling old preview first.");
                StopPlacement();
            }

            _activeDefinition = definition;
            _activeItemInstanceId = itemInstanceId;
            _isInPlacementMode = true;

            GameObject previewPrefab = ResolvePlacementPreview(definition);
            if (previewPrefab != null)
            {
                _previewInstance = Instantiate(previewPrefab);
                Debug.Log($"[DEPLOY_FLOW] Preview spawned: def={definition.ItemID} preview={previewPrefab.name}");
            }
            else
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Preview missing: def={definition.ItemID}. Placement still runs but no visual preview will show.");
            }
        }

        private void StopPlacement()
        {
            _isInPlacementMode = false;
            _activeDefinition = null;
            _activeItemInstanceId = null;
            _placementValid = false;

            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
        }

        private void ResolveCamera()
        {
            if (_cameraTransform != null)
                return;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        private void OnPlayerDied() => StopPlacement();

        private void UpdatePreviewPosition()
        {
            if (_previewInstance == null)
            {
                return;
            }

            if (!TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out bool valid))
                return;

            _previewInstance.transform.SetPositionAndRotation(point, rotation);
            _placementValid = valid;

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
                Vector3 aimPoint = _aimSystem.FinalAimGroundPos;
                Vector3 rayOrigin = aimPoint + Vector3.up * 8f;
                if (TryRaycastPlacementSurface(rayOrigin, Vector3.down, 20f, surfaceMask, out RaycastHit aimHit))
                {
                    Vector3 forward = Vector3.ProjectOnPlane(_aimSystem.FinalAimDir, Vector3.up);
                    BuildPlacementResult(aimHit, forward, blockerMask, out point, out rotation, out valid);
                    return true;
                }

                point = aimPoint;
                Vector3 aimForward = Vector3.ProjectOnPlane(_aimSystem.FinalAimDir, Vector3.up);
                rotation = aimForward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aimForward) : Quaternion.identity;
                valid = false;
                return true;
            }

            if (_cameraTransform == null)
            {
                point = transform.position;
                rotation = Quaternion.identity;
                valid = false;
                return false;
            }

            Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);

            if (TryRaycastPlacementSurface(ray.origin, ray.direction, distance * 2f, surfaceMask, out RaycastHit hit))
            {
                Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);
                BuildPlacementResult(hit, forward, blockerMask, out point, out rotation, out valid);
                return true;
            }

            point = _cameraTransform.position + _cameraTransform.forward * distance;
            rotation = Quaternion.identity;
            valid = false;
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
            for (int i = 0; i < count; i++)
            {
                Collider col = _surfaceHits[i].collider;
                if (col == null)
                    continue;

                if (_previewInstance != null && col.transform.IsChildOf(_previewInstance.transform))
                    continue;

                if (_networkPlayer != null && col.transform.IsChildOf(_networkPlayer.transform))
                    continue;

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

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            bool slopeOk = slope <= ResolveMaxSlope(_activeDefinition);
            bool noOverlap = !HasBlockingOverlap(
                point,
                ResolveCheckRadius(_activeDefinition),
                overlapMask,
                hit.collider);
            valid = slopeOk && noOverlap;
        }

        private void RequestPlacement(Vector3 position, Quaternion rotation)
        {
            string instanceId = _activeItemInstanceId;
            string definitionId = _activeDefinition?.ItemID;

            StopPlacement();
            Debug.Log($"[DEPLOY_FLOW] Sending CmdRequestPlaceDeployable def={definitionId} item={instanceId} pos={position:F2}");
            CmdRequestPlaceDeployable(position, rotation, definitionId, instanceId);
        }

        [ServerRpc(RequireOwnership = true)]
        private void CmdRequestPlaceDeployable(
            Vector3 position,
            Quaternion rotation,
            string definitionId,
            string itemInstanceId)
        {
            if (_networkPlayer == null || string.IsNullOrEmpty(definitionId))
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: networkPlayer={(_networkPlayer != null ? "ok" : "null")} definitionId='{definitionId}'");
                return;
            }

            if (!ValidateHeldItem(itemInstanceId, definitionId))
            {
                Debug.LogWarning($"[DEPLOY_FLOW] Server reject: held item validation failed def={definitionId} item={itemInstanceId}");
                return;
            }

            var def = ItemDatabase.GetDefinition(definitionId);
            if (!ValidateServerPlacement(def, position))
            {
                Debug.LogWarning($"[DeployablePlacementHandler] Placement rejected by server validation. def={definitionId} pos={position:F2}");
                return;
            }

            bool placed = def switch
            {
                BeaconDefinition => TryPlaceBeacon(position, rotation, definitionId),
                DeployableDefinition deployable => TryPlaceGenericDeployable(deployable, position, rotation, definitionId),
                _ => false
            };

            if (!placed || string.IsNullOrEmpty(itemInstanceId))
                return;

            ResolveInventory()?.RemoveItem(itemInstanceId, 1);
            Debug.Log($"[DeployablePlacementHandler] Consumed deployable item {definitionId}.");
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

        private bool TryPlaceGenericDeployable(
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

            if (deployable is GenericDeployable generic)
                generic.Initialize(_networkPlayer.TeamId, def.MaxHP, def.DeployableKind, definitionId);
            else
                deployable.Initialize(_networkPlayer.TeamId, def.MaxHP);

            InstanceFinder.ServerManager.Spawn(go, Owner);
            deployable.StartPlacement();

            Debug.Log($"[DeployablePlacementHandler] Placed deployable {definitionId} kind={def.DeployableKind} at {position}.");
            return true;
        }

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

        private float ResolvePlacementDistance(ItemDefinition def)
            => def is DeployableDefinition deployable ? deployable.PlacementDistance : _placementDistance;

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
            return configured.value == 0 || configured.value == ~0
                ? NightHuntLayers.MaskGroundAim
                : configured;
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

        private bool ValidateServerPlacement(ItemDefinition def, Vector3 position)
        {
            if (def == null || _networkPlayer == null)
                return false;

            float maxDistance = ResolvePlacementDistance(def) + 0.75f;
            Vector3 fromPlayer = position - _networkPlayer.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > maxDistance * maxDistance)
                return false;

            LayerMask surfaceMask = ResolvePlacementSurfaceMask(def);
            LayerMask blockerMask = ResolvePlacementBlockerMask();
            float radius = ResolveCheckRadius(def);

            // Find the actual supporting surface near the client-requested point.
            Collider surface = null;
            if (Physics.Raycast(position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > ResolveMaxSlope(def))
                    return false;

                if (Vector3.Distance(hit.point, position) > 0.35f)
                    return false;

                surface = hit.collider;
            }
            else if (LayerMask.NameToLayer(NightHuntLayers.Ground) >= 0)
            {
                return false;
            }

            return !HasBlockingOverlap(position, radius, blockerMask, surface);
        }

        private bool HasBlockingOverlap(Vector3 point, float radius, LayerMask mask, Collider surfaceCollider)
        {
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

                return true;
            }

            return false;
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
