using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Input.Handlers.Interaction
{
    /// <summary>
    /// Handles interaction / pickup input (E = Interact, F = Pickup / Pickup-All).
    ///
    /// DESIGN (SRP):
    ///   - Uses <see cref="RaycastDetector.CurrentInteractable"/> (IInteractable) as the
    ///     primary interaction target.
    ///   - Optionally uses <see cref="ProximityInteractScanner"/> when PickupAll mode
    ///     is active (F key held).
    ///   - Toggle single / all pickup with <see cref="TogglePickupAllMode"/>.
    ///   - Press Tab (configurable) to log the nearby list to console.
    /// </summary>
    public class InteractionInputHandler : MonoBehaviour, IInputHandler
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

        // ── Private state ────────────────────────────────────────────────────────

        private InputActionMap playerMap;
        private InputAction interactAction;  // E
        private InputAction pickupAction;    // F
        private InputAction logNearbyAction; // Tab (optional)

        private bool inputEnabled = false;
        private float holdTimer = 0f;
        private bool isHolding = false;
        private ContainerLootSource holdingContainer;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (raycastDetector == null)
                raycastDetector = GetComponent<RaycastDetector>();

            if (proximityScanner == null)
                proximityScanner = GetComponent<ProximityInteractScanner>();
        }

        private void OnEnable()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void OnDisable()
        {
            DisableInput();
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        private void Update()
        {
            if (!inputEnabled) return;
            HandleHoldInteract();
        }

        // ── IInputHandler ────────────────────────────────────────────────────────

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap() => playerMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            // Lazy init – lấy map nếu chưa có
            if (playerMap == null)
            {
                if (InputLayerManager.Instance == null)
                {
                    Debug.LogError("[InteractionInputHandler] InputLayerManager.Instance is null!");
                    return;
                }

                playerMap = InputLayerManager.Instance.PlayerMap;
                if (playerMap == null)
                {
                    Debug.LogWarning("[InteractionInputHandler] Player action map not found!");
                    return;
                }

                interactAction  = playerMap.FindAction("Interact");
                pickupAction    = playerMap.FindAction("Pickup");
                logNearbyAction = playerMap.FindAction("LogNearby");
            }

            // Chỉ set true sau khi đảm bảo có map
            inputEnabled = true;

            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
                interactAction.canceled  += OnInteractCanceled;
            }

            if (pickupAction != null)
                pickupAction.performed += OnPickupPerformed;

            if (logNearbyAction != null)
                logNearbyAction.performed += OnLogNearbyPerformed;
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;
            inputEnabled = false;

            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
                interactAction.canceled  -= OnInteractCanceled;
            }

            if (pickupAction != null)
                pickupAction.performed -= OnPickupPerformed;

            if (logNearbyAction != null)
                logNearbyAction.performed -= OnLogNearbyPerformed;

            isHolding = false;
            holdTimer = 0f;
            holdingContainer = null;
        }

        // ── Input event handlers ─────────────────────────────────────────────────

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            // Primary path: use IInteractable from RaycastDetector
            var target = raycastDetector?.CurrentInteractable;
            if (target != null && target.CanInteract(gameObject))
            {
                // Special case: ContainerLootSource may require a hold interaction
                if (target is ContainerLootSource container && ShouldHold(container))
                {
                    isHolding = true;
                    holdTimer = 0f;
                    holdingContainer = container;
                    Debug.Log($"[Interact] Hold started: {target.InteractLabel}");
                    return;
                }

                Debug.Log($"[Interact] {target.InteractLabel}");
                target.Interact(gameObject);
                return;
            }

            // Legacy fallback for any target not yet implementing IInteractable
            HandleLegacyInteract();
        }

        private void OnInteractCanceled(InputAction.CallbackContext ctx)
        {
            if (isHolding)
            {
                isHolding = false;
                holdTimer = 0f;
                holdingContainer = null;
                Debug.Log("[Interact] Hold canceled.");
            }
        }

        private void OnPickupPerformed(InputAction.CallbackContext ctx)
        {
            if (pickupAllMode && proximityScanner != null)
                PickupAllNearby();
            else
                PickupSingle();
        }

        private void OnLogNearbyPerformed(InputAction.CallbackContext ctx)
        {
            if (proximityScanner != null)
                proximityScanner.LogNearby();
            else
                Debug.Log("[Interact] ProximityScanner not assigned.");
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

            // Legacy: direct WorldPickup path
            var pickup = raycastDetector?.CurrentWorldPickup;
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
                if (item is IPickupable && item.CanInteract(gameObject))
                {
                    item.Interact(gameObject);
                    count++;
                }
            }

            Debug.Log($"[Pickup] PickupAll: triggered {count} pickup(s).");
        }

        // ── Hold interact ────────────────────────────────────────────────────────

        private void HandleHoldInteract()
        {
            if (!isHolding || holdingContainer == null) return;

            if (interactAction != null && interactAction.IsPressed())
            {
                holdTimer += Time.deltaTime;
                float holdDuration = 1.5f; // TODO: read from LootableConfig

                if (holdTimer >= holdDuration)
                {
                    Debug.Log($"[Interact] Hold complete: {holdingContainer.InteractLabel}");
                    var conn = GetLocalConnection();
                    if (conn != null)
                        holdingContainer.RequestOpen(conn);

                    isHolding = false;
                    holdTimer = 0f;
                    holdingContainer = null;
                }
            }
            else
            {
                // Released early
                isHolding = false;
                holdTimer = 0f;
                holdingContainer = null;
            }
        }

        private static bool ShouldHold(ContainerLootSource container)
        {
            // Hold only if container hasn't been opened yet
            return !container.IsOpen && !container.HasRolled;
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

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Toggle between single-item and all-nearby pickup on F press.</summary>
        public void TogglePickupAllMode()
        {
            pickupAllMode = !pickupAllMode;
            Debug.Log($"[Interact] Pickup mode: {(pickupAllMode ? "ALL nearby" : "Single (aimed)")}");
        }

        public bool IsPickupAllMode => pickupAllMode;

        // ── Helper ───────────────────────────────────────────────────────────────

        private NetworkConnection GetLocalConnection()
        {
            if (NightHunt.Networking.NetworkGameManager.Instance != null)
            {
                var nm = NightHunt.Networking.NetworkGameManager.Instance.NetworkManager;
                if (nm?.ClientManager != null)
                    return nm.ClientManager.Connection;
            }

            var fallback = Object.FindFirstObjectByType<NetworkManager>();
            if (fallback?.ClientManager != null)
                return fallback.ClientManager.Connection;

            Debug.LogWarning("[InteractionInputHandler] Could not find NetworkManager!");
            return null;
        }
    }
}
