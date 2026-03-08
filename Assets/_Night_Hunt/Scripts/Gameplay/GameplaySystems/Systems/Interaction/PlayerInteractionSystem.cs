using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
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

        [Header("Debug")]
        [SerializeField] private bool debugInputLogs = true;

        // ── Hold state ─────────────────────────────────────────────────────────────

        private bool _isHolding;
        private float _holdTimer;
        /// <summary>Current hold target — any IHoldInteractable (WorldContainer, WorldDoor, WorldSwitch...).</summary>
        private IHoldInteractable _holdingInteractable;

        // Networking
        private NetworkPlayer _networkPlayer;
        private bool _inputSubscribed;

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

            // In case Network/ownership is not ready yet when this component enables,
            // listen for NetworkPlayer owner-ready callback to safely subscribe input.
            NetworkPlayer.OnOwnerReady += HandleOwnerReady;
        }

        private void OnEnable()
        {
            TrySubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();

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
            if (debugInputLogs)
                Debug.Log("[PlayerInteractionSystem] InteractPerformed received.");

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

            // No valid interact (for debugging: distinguish between no target vs blocked)
            if (target == null)
            {
                Debug.Log("[Interact] InteractPerformed received but no interactable under crosshair.");
            }
            else
            {
                float dist = float.NaN;
                float maxDist = float.NaN;

                if (target is Component c)
                {
                    dist = Vector3.Distance(c.transform.position, transform.position);

                    // If this is a WorldItem, try to read its maxPickupDistance for better context
                    if (target is NightHunt.GameplaySystems.Loot.WorldItem worldItem)
                    {
                        var field = typeof(NightHunt.GameplaySystems.Loot.WorldItem)
                            .GetField("maxPickupDistance",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                            maxDist = (float)field.GetValue(worldItem);
                    }
                }

                if (!float.IsNaN(dist))
                {
                    if (!float.IsNaN(maxDist))
                        Debug.Log($"[Interact] InteractPerformed, target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m, max={maxDist:F2}m)");
                    else
                        Debug.Log($"[Interact] InteractPerformed, target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m)");
                }
                else
                {
                    Debug.Log($"[Interact] InteractPerformed, target found but CanInteract == false: {target.InteractLabel}");
                }
            }

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
            if (debugInputLogs)
                Debug.Log("[PlayerInteractionSystem] PickupPerformed received.");

            if (pickupAllMode && proximityScanner != null)
                PickupAllNearby();
            else
                PickupSingle();
        }

        public void HandleLogNearbyPerformed()
        {
            if (debugInputLogs)
                Debug.Log("[PlayerInteractionSystem] LogNearbyPerformed received.");

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

        /// <summary>True while a hold-interaction is in progress.</summary>
        public bool IsHolding => _isHolding;

        /// <summary>
        /// Normalised progress [0..1] of the current hold interaction.
        /// Returns 0 when no hold is active.
        /// </summary>
        public float HoldProgress
        {
            get
            {
                if (!_isHolding || _holdingInteractable == null || _holdingInteractable.HoldDuration <= 0f)
                    return 0f;
                return Mathf.Clamp01(_holdTimer / _holdingInteractable.HoldDuration);
            }
        }

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

            if (target != null && !target.CanInteract(gameObject))
            {
                float dist = float.NaN;
                float maxDist = float.NaN;

                if (target is Component c)
                {
                    dist = Vector3.Distance(c.transform.position, transform.position);

                    if (target is NightHunt.GameplaySystems.Loot.WorldItem worldItem)
                    {
                        var field = typeof(NightHunt.GameplaySystems.Loot.WorldItem)
                            .GetField("maxPickupDistance",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                            maxDist = (float)field.GetValue(worldItem);
                    }
                }

                if (!float.IsNaN(dist))
                {
                    if (!float.IsNaN(maxDist))
                        Debug.Log($"[Pickup] Target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m, max={maxDist:F2}m)");
                    else
                        Debug.Log($"[Pickup] Target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m)");
                }
                else
                {
                    Debug.Log($"[Pickup] Target found but CanInteract == false: {target.InteractLabel}");
                }
            }

            // Legacy: direct WorldItem path
            var pickup = raycastDetector?.CurrentWorldItem;
            if (pickup != null)
            {
                var playerNob = GetLocalPlayerNob();
                if (playerNob != null)
                {
                    Debug.Log($"[Pickup] Legacy path: {pickup.ItemDefinitionID} x{pickup.Quantity}");
                    pickup.RequestPickup(playerNob);
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

        // ── Helper ───────────────────────────────────────────────────────────────

        // PlayerInteractionSystem luôn nằm trên player GameObject — GetComponent<NetworkObject>() là chính xác.
        private NetworkObject GetLocalPlayerNob() => GetComponent<NetworkObject>();

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

        // ── Input subscription helpers ──────────────────────────────────────────

        private void HandleOwnerReady(NetworkPlayer player)
        {
            if (player == _networkPlayer)
            {
                if (debugInputLogs)
                    Debug.Log("[PlayerInteractionSystem] NetworkPlayer owner ready → trying to subscribe input.");
                TrySubscribeInput();
            }
        }

        private void TrySubscribeInput()
        {
            if (_inputSubscribed)
                return;

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

            _inputSubscribed = true;

            if (debugInputLogs)
                Debug.Log("[PlayerInteractionSystem] Subscribed to InteractionInputHandler events (local player).");
        }

        private void UnsubscribeInput()
        {
            NetworkPlayer.OnOwnerReady -= HandleOwnerReady;

            if (!_inputSubscribed)
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

            _inputSubscribed = false;

            if (debugInputLogs)
                Debug.Log("[PlayerInteractionSystem] Unsubscribed from InteractionInputHandler events.");
        }
    }
}

