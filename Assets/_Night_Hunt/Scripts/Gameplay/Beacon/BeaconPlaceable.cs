using FishNet.Object;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.Core.State;
using NightHunt.Networking;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Beacon
{
    /// <summary>
    /// Client-side controller that manages the beacon placement workflow.
    ///
    /// FLOW:
    ///   1. Player loots a beacon item → goes to inventory.
    ///   2. Player selects the deployable item → ItemSelectionSystem detects the
    ///      Deployable item type and fires RpcBeginDeployment back to the
    ///      owning client, which calls <see cref="BeginDeploy"/>.
    ///   3. This component shows a placement ghost preview until Confirm/Cancel.
    ///   4. On Confirm → <see cref="CmdRequestPlaceBeacon"/> ServerRpc:
    ///        • Server validates the player still holds the item.
    ///        • Delegates to <see cref="BeaconManager.TryPlaceBeacon"/>.
    ///        • If successful, consumes 1× beacon from inventory.
    ///   5. On player death, placement is cancelled automatically.
    ///
    /// Attach to the player root (same GO as NetworkPlayer).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeaconPlaceable : NetworkBehaviour, IDeployableHandler
    {
        [Header("References")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private NetworkPlayer _networkPlayer;

        [Header("Settings")]
        [SerializeField] private float _placementDistance    = 3f;
        [SerializeField] private float _placementCheckRadius = 0.5f;

        // ── Runtime state (client) ─────────────────────────────────────────────
        private BeaconDefinition _activeDefinition;
        private string           _activeItemInstanceId;   // consumed on server after success
        private GameObject       _previewInstance;
        private bool             _isInPlacementMode;
        private bool             _placementValid;

        // ── Server-side systems (resolved in Awake) ────────────────────────────
        private IInventorySystem             _inventorySystem;
        private CharacterLifecycleController _lifecycle;

        // ──────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (_networkPlayer == null)
                _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkPlayer not found")
        .Resolve();

            if (_cameraTransform == null)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null) _cameraTransform = cam.transform;
            }

            // Inventory system used server-side to validate & consume the beacon item.
            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] IInventorySystem not found")
        .Resolve();

            // Cancel placement mode when the player dies.
            _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CharacterLifecycleController not found")
        .Resolve();
            if (_lifecycle != null) _lifecycle.OnDied += OnPlayerDied;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Re-resolve camera for late-joining clients.
            if (_cameraTransform == null)
            {
                var cam = UnityEngine.Camera.main;
                if (cam != null) _cameraTransform = cam.transform;
            }
        }

        private void Update()
        {
            if (!IsOwner || !_isInPlacementMode) return;
            UpdatePreviewPosition();
            HandleInput();
        }

        private void OnDestroy()
        {
            CancelPlacement();
            if (_lifecycle != null) _lifecycle.OnDied -= OnPlayerDied;
        }

        private void OnPlayerDied() => CancelPlacement();

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region IDeployableHandler

        /// <summary>
        /// Entry point called by ItemSelectionSystem (via TargetRpc) when a Deployable
        /// item is selected. Runs on the OWNING CLIENT only.
        /// </summary>
        public bool BeginDeploy(ItemInstance item, ItemDefinition def)
        {
            if (def is not BeaconDefinition beaconDef) return false;
            BeginPlacement(beaconDef, item.InstanceID);
            return true;
        }

        public void CancelDeploy() => CancelPlacement();

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Begin placement mode for a specific beacon definition.
        /// <paramref name="itemInstanceId"/> is passed to the ServerRpc so the
        /// server can verify the player holds the item and consume it on success.
        /// </summary>
        public void BeginPlacement(BeaconDefinition definition, string itemInstanceId = null)
        {
            if (!IsOwner) return;
            if (_isInPlacementMode) CancelPlacement();

            _activeDefinition     = definition;
            _activeItemInstanceId = itemInstanceId;
            _isInPlacementMode    = true;

            if (definition.PlacementPreviewPrefab != null)
                _previewInstance = Instantiate(definition.PlacementPreviewPrefab);
        }

        /// <summary>Cancel placement and destroy any preview ghost.</summary>
        public void CancelPlacement()
        {
            _isInPlacementMode    = false;
            _activeDefinition     = null;
            _activeItemInstanceId = null;
            _placementValid       = false;

            if (_previewInstance != null)
            {
                Destroy(_previewInstance);
                _previewInstance = null;
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Placement logic (client)

        private void UpdatePreviewPosition()
        {
            if (_previewInstance == null || _cameraTransform == null) return;

            if (TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out bool valid))
            {
                _previewInstance.transform.SetPositionAndRotation(point, rotation);
                _placementValid = valid;

                // Tint preview green (valid) or red (invalid)
                var renderers = _previewInstance.GetComponentsInChildren<Renderer>();
                Color tint = valid
                    ? new Color(0.3f, 1f, 0.3f, 0.5f)
                    : new Color(1f,   0.3f, 0.3f, 0.5f);
                foreach (var r in renderers)
                    r.material.color = tint;
            }
        }

        private bool TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out bool valid)
        {
            Ray       ray  = new Ray(_cameraTransform.position, _cameraTransform.forward);
            LayerMask mask = _activeDefinition != null ? _activeDefinition.PlacementLayerMask : ~0;

            if (Physics.Raycast(ray, out RaycastHit hit, _placementDistance * 2f, mask))
            {
                point    = hit.point;
                rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up));

                float slope    = Vector3.Angle(hit.normal, Vector3.up);
                float maxSlope = _activeDefinition?.MaxPlacementSlope ?? 30f;
                bool  slopeOk  = slope <= maxSlope;
                bool  noOverlap = !Physics.CheckSphere(point, _placementCheckRadius, mask);
                valid = slopeOk && noOverlap;
                return true;
            }

            point    = _cameraTransform.position + _cameraTransform.forward * _placementDistance;
            rotation = Quaternion.identity;
            valid    = false;
            return true;
        }

        private void HandleInput()
        {
            bool confirm = UnityEngine.Input.GetButtonDown(
                _activeDefinition?.PlaceAction  ?? "Interact");
            bool cancel  = UnityEngine.Input.GetButtonDown(
                _activeDefinition?.CancelAction ?? "AltFire");

            if (cancel)
            {
                CancelPlacement();
                return;
            }

            if (confirm && _placementValid)
            {
                TryGetPlacementPoint(out Vector3 point, out Quaternion rotation, out _);
                ConfirmPlacement(point, rotation);
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Server RPC

        private void ConfirmPlacement(Vector3 position, Quaternion rotation)
        {
            // Cache before CancelPlacement clears the state fields.
            string instanceId  = _activeItemInstanceId;
            string definitionId = _activeDefinition?.ItemID;

            CancelPlacement();   // Destroy preview immediately on client.
            CmdRequestPlaceBeacon(position, rotation, definitionId, instanceId);
        }

        /// <summary>
        /// Server: validate the player still holds the beacon item, delegate
        /// spawn to BeaconManager, then consume the item on success.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void CmdRequestPlaceBeacon(
            Vector3  position,
            Quaternion rotation,
            string   definitionId,
            string   itemInstanceId)
        {
            if (_networkPlayer == null) return;
            int teamId = _networkPlayer.TeamId;

            // ── Validate inventory possession (server-authoritative) ──────────
            // _inventorySystem was resolved in Awake; on the server it IS the
            // authoritative InventorySystem component on this GameObject.
            if (!string.IsNullOrEmpty(itemInstanceId))
            {
                var inv  = _inventorySystem ?? ComponentResolver.Find<IInventorySystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] IInventorySystem not found")
        .Resolve();
                var held = inv?.GetItemByInstanceID(itemInstanceId);
                if (held == null)
                {
                    Debug.LogWarning("[BeaconPlaceable] Placement rejected – item no longer in inventory.");
                    return;
                }
            }

            // ── Request spawn via BeaconManager ──────────────────────────────
            var manager = BeaconManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[BeaconPlaceable] BeaconManager not found.");
                return;
            }

            bool placed = manager.TryPlaceBeacon(teamId, position, rotation, Owner, definitionId);

            // ── Consume item only on success ──────────────────────────────────
            if (placed && !string.IsNullOrEmpty(itemInstanceId))
            {
                var inv = _inventorySystem ?? ComponentResolver.Find<IInventorySystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] IInventorySystem not found")
        .Resolve();
                inv?.RemoveItem(itemInstanceId, 1);
                Debug.Log("[BeaconPlaceable] Beacon item consumed from inventory.");
            }
        }

        #endregion
    }
}
