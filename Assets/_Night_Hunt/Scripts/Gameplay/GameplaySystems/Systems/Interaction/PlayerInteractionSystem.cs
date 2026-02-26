using FishNet.Connection;
using FishNet.Managing;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;
using UnityEngine;

namespace NightHunt.GameplaySystems.Interaction
{
    /// <summary>
    /// Player-side interaction logic: raycast target selection, pickup, container/corpse loot, hold-to-open, etc.
    ///
    /// DESIGN:
    /// - This class owns ONLY gameplay logic (IInteractable / IPickupable, distance checks, RPC calls).
    /// - Input is provided by <see cref="InteractionInputHandler"/> via C# events.
    /// - Lives on the player GameObject (alongside NetworkPlayer, RaycastDetector, ProximityInteractScanner).
    /// - Only the local player instance subscribes to input events.
    /// </summary>
    public class PlayerInteractionSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RaycastDetector raycastDetector;

        [Tooltip("Optional: proximity scanner for 'pickup all nearby' feature.")]
        [SerializeField] private ProximityInteractScanner proximityScanner;

        [Header("Pickup Mode")]
        [Tooltip("When true, pressing F picks up ALL items within proximity scanner radius.\n" +
                 "When false, only the item aimed at is picked up.\n" +
                 "Toggle at runtime with TogglePickupAllMode().")]
        [SerializeField] private bool pickupAllMode = false;

        // ── Hold state ─────────────────────────────────────────────────────────────

        private bool _isHolding;
        private float _holdTimer;
        /// <summary>Current hold target — any IHoldInteractable (WorldContainer, WorldDoor, WorldSwitch...).</summary>
        private IHoldInteractable _holdingInteractable;

        // Networking
        private NetworkPlayer _networkPlayer;

        private bool IsLocalPlayer
        {
            get
            {
                // If no NetworkPlayer is present, assume local (single-player / editor tests)
                return _networkPlayer == null || _networkPlayer.IsLocalPlayer;
            }
        }

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (raycastDetector == null)
                raycastDetector = GetComponent<RaycastDetector>();

            if (proximityScanner == null)
                proximityScanner = GetComponent<ProximityInteractScanner>();

            _networkPlayer = GetComponent<NetworkPlayer>();
        }

        private void OnEnable()
        {
            if (!IsLocalPlayer)
                return;

            var inputManager = InputManager.Instance;
            if (inputManager == null)
                return;

            var handler = inputManager.InteractionHandler;
            if (handler == null)
                return;

            handler.InteractPerformed  += HandleInteractPerformed;
            handler.InteractCanceled   += HandleInteractCanceled;
            handler.PickupPerformed    += HandlePickupPerformed;
            handler.LogNearbyPerformed += HandleLogNearbyPerformed;
        }

        private void OnDisable()
        {
            if (!IsLocalPlayer)
                return;

            var inputManager = InputManager.Instance;
            if (inputManager != null)
            {
                var handler = inputManager.InteractionHandler;
                if (handler != null)
                {
                    handler.InteractPerformed  -= HandleInteractPerformed;
                    handler.InteractCanceled   -= HandleInteractCanceled;
                    handler.PickupPerformed    -= HandlePickupPerformed;
                    handler.LogNearbyPerformed -= HandleLogNearbyPerformed;
                }
            }

            // Reset local hold state
            _isHolding = false;
            _holdTimer = 0f;
            _holdingInteractable = null;
        }

        private void Update()
        {
            if (!IsLocalPlayer)
                return;

            HandleHoldInteract(Time.deltaTime);
        }

        // ── Public API (called by InteractionInputHandler via events) ────────────

        public void HandleInteractPerformed()
        {
            // Primary path: use IInteractable from RaycastDetector
            var target = raycastDetector?.CurrentInteractable;
            if (target != null && target.CanInteract(gameObject))
            {
            // IHoldInteractable: WorldContainer, WorldDoor, WorldSwitch, etc.
            // HoldDuration == 0 → Instant mode (call Interact immediately).
            if (target is IHoldInteractable holdTarget && holdTarget.HoldDuration > 0)
            {
                _isHolding = true;
                _holdTimer = 0f;
                _holdingInteractable = holdTarget;
                Debug.Log($"[Interact] Hold started ({holdTarget.HoldDuration:F1}s): {target.InteractLabel}");
                return;
            }

                Debug.Log($"[Interact] {target.InteractLabel}");
                target.Interact(gameObject);
                return;
            }

            // Legacy fallback for any target not yet implementing IInteractable
            HandleLegacyInteract();
        }

        public void HandleInteractCanceled()
        {
            if (_isHolding)
            {
                _isHolding = false;
                _holdTimer = 0f;
                _holdingInteractable = null;
                Debug.Log("[Interact] Hold canceled.");
            }
        }

        public void HandlePickupPerformed()
        {
            if (pickupAllMode && proximityScanner != null)
                PickupAllNearby();
            else
                PickupSingle();
        }

        public void HandleLogNearbyPerformed()
        {
            if (proximityScanner != null)
                proximityScanner.LogNearby();
            else
                Debug.Log("[Interact] ProximityScanner not assigned.");
        }

        public void TogglePickupAllMode()
        {
            pickupAllMode = !pickupAllMode;
            Debug.Log($"[Interact] Pickup mode: {(pickupAllMode ? "ALL nearby" : "Single (aimed)")}");
        }

        public bool IsPickupAllMode => pickupAllMode;

        // ── Hold interact ────────────────────────────────────────────────────────

        private void HandleHoldInteract(float deltaTime)
        {
            if (!_isHolding || _holdingInteractable == null)
                return;

            _holdTimer += deltaTime;

            if (_holdTimer >= _holdingInteractable.HoldDuration)
            {
                Debug.Log($"[Interact] Hold complete: {_holdingInteractable.InteractLabel}");
                (_holdingInteractable as IInteractable)?.Interact(gameObject);

                _isHolding = false;
                _holdTimer = 0f;
                _holdingInteractable = null;
            }
        }

        // ── Pickup helpers ───────────────────────────────────────────────────────

        /// <summary>Pick up the single item currently aimed at (raycast target).</summary>
        private void PickupSingle()
        {
            // IInteractable path (preferred)
            var target = raycastDetector?.CurrentInteractable;
            if (target != null && target.CanInteract(gameObject))
            {
                Debug.Log($"[Pickup] Single: {target.InteractLabel}");
                target.Interact(gameObject);
                return;
            }

            // Legacy: direct WorldItem path
            var pickup = raycastDetector?.CurrentWorldItem;
            if (pickup != null)
            {
                var conn = GetLocalConnection();
                if (conn != null)
                {
                    Debug.Log($"[Pickup] Legacy path: {pickup.ItemDefinitionID} x{pickup.Quantity}");
                    pickup.RequestPickup(conn);
                }
            }
        }

        /// <summary>Pick up ALL IPickupable objects within ProximityScanner radius.</summary>
        private void PickupAllNearby()
        {
            if (proximityScanner == null)
            {
                Debug.LogWarning("[Pickup] PickupAll requested but ProximityScanner is null.");
                PickupSingle(); // graceful fallback
                return;
            }

            var nearby = proximityScanner.NearbyInteractables;
            if (nearby.Count == 0)
            {
                Debug.Log("[Pickup] PickupAll: nothing nearby.");
                return;
            }

            int count = 0;
            foreach (var item in nearby)
            {
                if (item is IPickupable pickupable && item.CanInteract(gameObject))
                {
                    item.Interact(gameObject);
                    count++;
                }
            }

            Debug.Log($"[Pickup] PickupAll: triggered {count} pickup(s).");
        }

        // ── Legacy fallback ──────────────────────────────────────────────────────

        /// <summary>
        /// Called when the raycast target doesn't implement IInteractable.
        /// Keeps backward compatibility for typed checks.
        /// </summary>
        private void HandleLegacyInteract()
        {
            if (raycastDetector == null) return;

            var container = raycastDetector.CurrentContainer;
            if (container != null)
            {
                var conn = GetLocalConnection();
                if (conn != null) container.RequestOpen(conn);
                return;
            }

            var corpse = raycastDetector.CurrentCorpse;
            if (corpse != null)
            {
                var conn = GetLocalConnection();
                if (conn != null) corpse.RequestOpen(conn);
            }
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        private NetworkConnection GetLocalConnection()
        {
            if (NetworkGameManager.Instance != null)
            {
                var nm = NetworkGameManager.Instance.NetworkManager;
                if (nm?.ClientManager != null)
                    return nm.ClientManager.Connection;
            }

            var fallback = Object.FindFirstObjectByType<NetworkManager>();
            if (fallback?.ClientManager != null)
                return fallback.ClientManager.Connection;

            Debug.LogWarning("[PlayerInteractionSystem] Could not find NetworkManager!");
            return null;
        }
    }
}

