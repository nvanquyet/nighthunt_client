using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;

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
        [SerializeField] private NightHuntDebugConfig _debugConfig;

        // â”€â”€ Hold state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private bool _isHolding;
        private float _holdTimer;
        /// <summary>Current hold target â€” any IHoldInteractable (WorldContainer, WorldDoor, WorldSwitch...).</summary>
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

        // â”€â”€ Unity lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            if (raycastDetector == null)
                raycastDetector = ComponentResolver.Find<RaycastDetector>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] RaycastDetector not found")
        .Resolve();

            if (proximityScanner == null)
                proximityScanner = ComponentResolver.Find<ProximityInteractScanner>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] ProximityInteractScanner not found")
        .Resolve();

            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkPlayer not found")
        .Resolve();

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

        // â”€â”€ Public API (called by InteractionInputHandler via events) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void HandleInteractPerformed()
        {
            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] InteractPerformed received.");

            // Primary path: use IInteractable from RaycastDetector
            var target = raycastDetector?.CurrentInteractable;
            if (target != null && target.CanInteract(gameObject))
            {
            // IHoldInteractable: WorldContainer, WorldDoor, WorldSwitch, etc.
            // HoldDuration == 0 â†’ Instant mode (call Interact immediately).
            if (target is IHoldInteractable holdTarget && holdTarget.HoldDuration > 0)
            {
                _isHolding = true;
                _holdTimer = 0f;
                _holdingInteractable = holdTarget;
                Debug.Log($"[PlayerInteractionSystem] Hold started ({holdTarget.HoldDuration:F1}s): {target.InteractLabel}");
                return;
            }

                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                    Debug.Log($"[Interact] {target.InteractLabel}");
                target.Interact(gameObject);
                return;
            }

            // No valid interact (for debugging: distinguish between no target vs blocked)
            if (target == null)
            {
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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
                        if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                            Debug.Log($"[Interact] InteractPerformed, target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m)");
                }
                else
                {
                    if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                    Debug.Log("[Interact] Hold canceled.");
            }
        }

        public void HandlePickupPerformed()
        {
            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] PickupPerformed received.");

            if (pickupAllMode && proximityScanner != null)
                PickupAllNearby();
            else
                PickupSingle();
        }

        public void HandleLogNearbyPerformed()
        {
            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] LogNearbyPerformed received.");

            if (proximityScanner != null)
                proximityScanner.LogNearby();
            else
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                    Debug.Log("[Interact] ProximityScanner not assigned.");
        }

        public void TogglePickupAllMode()
        {
            pickupAllMode = !pickupAllMode;
            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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

        // â”€â”€ Hold interact â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleHoldInteract(float deltaTime)
        {
            if (!_isHolding || _holdingInteractable == null)
                return;

            _holdTimer += deltaTime;

            if (_holdTimer >= _holdingInteractable.HoldDuration)
            {
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                    Debug.Log($"[Interact] Hold complete: {_holdingInteractable.InteractLabel}");
                (_holdingInteractable as IInteractable)?.Interact(gameObject);

                _isHolding = false;
                _holdTimer = 0f;
                _holdingInteractable = null;
            }
        }

        // â”€â”€ Pickup helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Pick up the single item currently aimed at (raycast target).</summary>
        private void PickupSingle()
        {
            // IInteractable path (preferred)
            var target = raycastDetector?.CurrentInteractable;
            if (target != null && target.CanInteract(gameObject))
            {
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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
                        if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                            Debug.Log($"[Pickup] Target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m)");
                }
                else
                {
                    if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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
                    if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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

            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                Debug.Log($"[Pickup] PickupAll: triggered {count} pickup(s).");
        }

        // â”€â”€ Helper â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // PlayerInteractionSystem luÃ´n náº±m trÃªn player GameObject â€” GetComponent<NetworkObject>() lÃ  chÃ­nh xÃ¡c.
        private NetworkObject GetLocalPlayerNob() => ComponentResolver.Find<NetworkObject>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkObject not found")
        .Resolve();

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

        // â”€â”€ Input subscription helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleOwnerReady(NetworkPlayer player)
        {
            if (player == _networkPlayer)
            {
                if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                    Debug.Log("[PlayerInteractionSystem] NetworkPlayer owner ready â†’ trying to subscribe input.");
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

            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
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

            if (_debugConfig != null && _debugConfig.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] Unsubscribed from InteractionInputHandler events.");
        }
    }
}



