using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.UI;

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

        /// <summary>True while the inventory screen is open; drives auto-show/hide logic in HandleNearbyLootablesChanged.</summary>
        private bool _inventoryIsOpen;

        /// <summary>True while we are spectating another player. Local scanner events are suppressed for UI; the spectated player's scanner drives LootContainerUI instead.</summary>
        private bool _isSpectating;

        /// <summary>The ProximityInteractScanner belonging to the currently spectated player. Null when not spectating.</summary>
        private ProximityInteractScanner _spectatedPlayerScanner;

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
            if (proximityScanner != null)
                proximityScanner.OnNearbyLootablesChanged += HandleNearbyLootablesChanged;
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            if (proximityScanner != null)
                proximityScanner.OnNearbyLootablesChanged -= HandleNearbyLootablesChanged;

            // Clean up spectated scanner subscription.
            if (_spectatedPlayerScanner != null)
            {
                _spectatedPlayerScanner.OnNearbyLootablesChanged -= HandleSpectatedNearbyLootablesChanged;
                _spectatedPlayerScanner = null;
            }

            _inventoryIsOpen = false;
            _isSpectating   = false;

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
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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
                Debug.Log($"[PlayerInteractionSystem] Hold started ({holdTarget.HoldDuration:F1}s): {target.InteractLabel}");
                return;
            }

                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                    Debug.Log($"[Interact] {target.InteractLabel}");
                target.Interact(gameObject);
                return;
            }

            // No valid interact (for debugging: distinguish between no target vs blocked)
            if (target == null)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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
                            .GetField("_maxPickupDistance",
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
                        if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                            Debug.Log($"[Interact] InteractPerformed, target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m)");
                }
                else
                {
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                    Debug.Log("[Interact] Hold canceled.");
            }
        }

        public void HandlePickupPerformed()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] PickupPerformed received.");

            if (pickupAllMode && proximityScanner != null)
                PickupAllNearby();
            else
                PickupSingle();
        }

        public void HandleLogNearbyPerformed()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] LogNearbyPerformed received.");

            if (proximityScanner != null)
                proximityScanner.LogNearby();
            else
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                    Debug.Log("[Interact] ProximityScanner not assigned.");
        }

        public void TogglePickupAllMode()
        {
            pickupAllMode = !pickupAllMode;
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                Debug.Log($"[Interact] Pickup mode: {(pickupAllMode ? "ALL nearby" : "Single (aimed)")}");
        }

        public bool IsPickupAllMode => pickupAllMode;

        /// <summary>
        /// Inventory-open flow: refresh proximity and open/show the closest lootable.
        /// This keeps Tab consistent with the normal interact path while avoiding a raycast requirement.
        /// </summary>
        public bool TryOpenNearestLootFromInventory()
        {
            return HandleInventoryOpened();
        }

        public bool HandleInventoryOpened()
        {
            if (!IsLocalPlayer)
            {
                Debug.Log("[LOOT_TAB_FLOW] Inventory OPEN skipped: this interaction system is not local.");
                return false;
            }

            _inventoryIsOpen = true;

            if (proximityScanner == null)
            {
                Debug.LogWarning("[LOOT_TAB_FLOW] Cannot open nearest loot: ProximityInteractScanner is null.");
                return false;
            }

            proximityScanner.ForceScan();
            proximityScanner.LogNearby();

            var nearbyLootables = proximityScanner.NearbyLootables;
            var worldItems = CollectNearbyWorldItems();
            Debug.Log($"[LOOT_TAB_FLOW] Inventory OPEN. NearbyLootables={nearbyLootables.Count} NearbyInteractables={proximityScanner.NearbyInteractables.Count} WorldItems={worldItems.Count}");

            for (int i = 0; i < nearbyLootables.Count; i++)
            {
                var lootable = nearbyLootables[i];
                if (lootable == null)
                    continue;

                var interactable = lootable as IInteractable;
                if (interactable == null)
                {
                    Debug.LogWarning($"[LOOT_TAB_FLOW] Lootable[{i}] does not implement IInteractable.");
                    continue;
                }

                bool canInteract = interactable.CanInteract(gameObject);
                string label = interactable.InteractLabel;
                int itemCount = lootable.GetStorage()?.Count ?? 0;
                Debug.Log($"[LOOT_TAB_FLOW] Candidate[{i}] type={lootable.GetType().Name} label='{label}' open={lootable.IsOpen} items={itemCount} canInteract={canInteract} storage={FormatLootStorage(lootable.GetStorage())}");

                if (!canInteract)
                    continue;

                if (!lootable.IsOpen)
                    continue;

                LootContainerUI.Instance?.ShowOpenedLootableFromInventory(lootable, gameObject, "InventoryOpen:already-open");
                Debug.Log($"[LOOT_TAB_FLOW] Inventory OPEN showed already-open lootable '{label}'.");
                return true;
            }

            var nearby = proximityScanner.NearbyInteractables;
            Debug.Log($"[LOOT_TAB_FLOW] No container/corpse in proximity. NearbyInteractables={nearby.Count}");
            for (int i = 0; i < nearby.Count; i++)
            {
                var interactable = nearby[i];
                if (interactable == null)
                    continue;

                bool canInteract = interactable.CanInteract(gameObject);
                Debug.Log($"[LOOT_TAB_FLOW] Nearby[{i}] type={interactable.GetType().Name} label='{interactable.InteractLabel}' canInteract={canInteract}");
            }

            if (worldItems.Count > 0 && LootContainerUI.Instance != null)
            {
                LootContainerUI.Instance.ShowWorldItems(worldItems);
                return true;
            }

            for (int i = 0; i < nearbyLootables.Count; i++)
            {
                var lootable = nearbyLootables[i];
                if (lootable is not IInteractable interactable || !interactable.CanInteract(gameObject))
                    continue;

                interactable.Interact(gameObject);
                Debug.Log($"[LOOT_TAB_FLOW] Inventory OPEN requested open for closed lootable '{interactable.InteractLabel}'. Waiting for server/open event.");
                return true;
            }

            Debug.Log("[LOOT_TAB_FLOW] Inventory OPEN: no interactable loot container/corpse/world item in proximity.");
            return false;
        }

        public bool HandleInventoryClosed()
        {
            if (!IsLocalPlayer)
            {
                Debug.Log("[LOOT_TAB_FLOW] Inventory CLOSE skipped: this interaction system is not local.");
                return false;
            }

            _inventoryIsOpen = false;

            if (proximityScanner == null)
            {
                Debug.LogWarning("[LOOT_TAB_FLOW] Inventory CLOSE: ProximityInteractScanner is null, hiding loot UI.");
                return false;
            }

            proximityScanner.ForceScan();
            proximityScanner.LogNearby();

            var nearbyLootables = proximityScanner.NearbyLootables;
            Debug.Log($"[LOOT_TAB_FLOW] Inventory CLOSE. NearbyLootables={nearbyLootables.Count}. Ground nearby will NOT be shown while inventory is closed.");

            for (int i = 0; i < nearbyLootables.Count; i++)
            {
                var lootable = nearbyLootables[i];
                if (lootable == null)
                    continue;

                var interactable = lootable as IInteractable;
                bool canInteract = interactable != null && interactable.CanInteract(gameObject);
                string label = interactable?.InteractLabel ?? lootable.GetType().Name;
                int itemCount = lootable.GetStorage()?.Count ?? 0;
                Debug.Log($"[LOOT_TAB_FLOW] CloseCandidate[{i}] type={lootable.GetType().Name} label='{label}' open={lootable.IsOpen} items={itemCount} canInteract={canInteract} storage={FormatLootStorage(lootable.GetStorage())}");

                if (!lootable.IsOpen || !canInteract)
                    continue;

                bool shown = LootContainerUI.Instance != null &&
                             LootContainerUI.Instance.ShowOpenedLootableFromInventory(lootable, gameObject, "InventoryClose:keep-opened-lootable");
                Debug.Log($"[LOOT_TAB_FLOW] Inventory CLOSE keep-opened-lootable label='{label}' shown={shown}.");
                return shown;
            }

            Debug.Log("[LOOT_TAB_FLOW] Inventory CLOSE: no valid opened container/corpse nearby. Loot UI should hide.");
            return false;
        }

        public bool RefreshNearbyWorldItemsForInventory()
        {
            if (!IsLocalPlayer || proximityScanner == null)
                return false;

            proximityScanner.ForceScan();

            var nearbyLootables = proximityScanner.NearbyLootables;
            for (int i = 0; i < nearbyLootables.Count; i++)
            {
                var lootable = nearbyLootables[i];
                if (lootable == null || !lootable.IsOpen) continue;
                if (lootable is not IInteractable interactable || !interactable.CanInteract(gameObject)) continue;

                // Open lootable in range — show it if not already displayed, then skip world-item refresh.
                if (LootContainerUI.Instance != null && !LootContainerUI.Instance.IsShowingOpenedLootable)
                    LootContainerUI.Instance.ShowOpenedLootableFromInventory(lootable, gameObject, "InventoryRefresh:open-lootable");
                return false;
            }

            var nearby = proximityScanner.NearbyInteractables;
            var worldItems = CollectNearbyWorldItems();
            for (int i = 0; i < nearby.Count; i++)
            {
                if (nearby[i] != null)
                    Debug.Log($"[LOOT_TAB_FLOW] RefreshNearby interactable[{i}] type={nearby[i].GetType().Name} label='{nearby[i].InteractLabel}' canInteract={nearby[i].CanInteract(gameObject)}");
            }

            if (worldItems.Count > 0 && LootContainerUI.Instance != null)
            {
                LootContainerUI.Instance.ShowWorldItems(worldItems);
                Debug.Log($"[LOOT_TAB_FLOW] Realtime nearby world item refresh: {worldItems.Count} item(s).");
                return true;
            }

            if (LootContainerUI.Instance != null && LootContainerUI.Instance.IsShowingWorldItems)
            {
                LootContainerUI.Instance.Hide();
                Debug.Log("[LOOT_TAB_FLOW] Realtime nearby world item refresh: no items left, hiding panel.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fired by <see cref="ProximityInteractScanner.OnNearbyLootablesChanged"/> on every scan tick
        /// when the nearby-lootable list changes.
        ///
        /// Inventory OPEN  → show the closest open lootable immediately (container takes priority over
        ///                    world items; the 0.5 s timer in GameHUDController handles world-item refresh).
        /// Inventory CLOSED → auto-show open containers/corpses that enter range; hide when they leave.
        ///                    Closed containers are intentionally ignored (player must press E to open).
        /// </summary>
        // ── Spectate API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GameHUDController when spectating a new player.
        /// Unsubscribes from the previous spectated scanner (if any), then subscribes to
        /// <paramref name="spectatedPlayer"/>'s ProximityInteractScanner.
        /// The scanner's natural tick (≤0.15 s) will trigger the first panel update.
        /// </summary>
        public void BeginSpectate(NetworkPlayer spectatedPlayer)
        {
            if (!IsLocalPlayer) return;

            // Unsubscribe from any previous spectated scanner first.
            if (_spectatedPlayerScanner != null)
            {
                _spectatedPlayerScanner.OnNearbyLootablesChanged -= HandleSpectatedNearbyLootablesChanged;
                _spectatedPlayerScanner = null;
            }

            _isSpectating = true;

            if (spectatedPlayer != null)
            {
                _spectatedPlayerScanner = ComponentResolver.Find<ProximityInteractScanner>(spectatedPlayer)
                    .OnSelf()
                    .InChildren()
                    .Resolve();

                if (_spectatedPlayerScanner != null)
                    _spectatedPlayerScanner.OnNearbyLootablesChanged += HandleSpectatedNearbyLootablesChanged;
                else
                    Debug.LogWarning($"[SPECTATE] ProximityInteractScanner not found on spectated player '{spectatedPlayer.name}'.");
            }

            Debug.Log($"[SPECTATE] BeginSpectate: player={spectatedPlayer?.name} scanner={(_spectatedPlayerScanner != null ? "found" : "null")}");
        }

        /// <summary>
        /// Called by GameHUDController when spectating ends (respawn or stop spectating).
        /// Cleans up the spectated scanner subscription and hides the loot panel.
        /// </summary>
        public void EndSpectate()
        {
            if (!IsLocalPlayer) return;

            if (_spectatedPlayerScanner != null)
            {
                _spectatedPlayerScanner.OnNearbyLootablesChanged -= HandleSpectatedNearbyLootablesChanged;
                _spectatedPlayerScanner = null;
            }

            _isSpectating = false;
            LootContainerUI.Instance?.HideSpectateView();
            Debug.Log("[SPECTATE] EndSpectate: spectate mode ended.");
        }

        // ── Proximity-change handlers ─────────────────────────────────────────────

        private void HandleNearbyLootablesChanged(IReadOnlyList<ILootable> nearbyLootables)
        {
            if (!IsLocalPlayer || LootContainerUI.Instance == null) return;
            // While spectating, the spectated player's scanner drives the UI.
            if (_isSpectating) return;

            if (_inventoryIsOpen)
            {
                // Inventory open: an open lootable takes priority over world items.
                // Show it immediately so the player doesn't have to wait for the 0.5 s timer.
                for (int i = 0; i < nearbyLootables.Count; i++)
                {
                    var lootable = nearbyLootables[i];
                    if (lootable == null || !lootable.IsOpen) continue;
                    if (lootable is not IInteractable interactable || !interactable.CanInteract(gameObject)) continue;

                    if (!LootContainerUI.Instance.IsShowingOpenedLootable)
                        LootContainerUI.Instance.ShowOpenedLootableFromInventory(lootable, gameObject, "ProximityChange:inventory-open");
                    return;
                }
                // No open lootable — the 0.5 s timer refreshes world items as before.
            }
            else
            {
                // Inventory closed: auto-show the first open container/corpse that enters range.
                for (int i = 0; i < nearbyLootables.Count; i++)
                {
                    var lootable = nearbyLootables[i];
                    if (lootable == null || !lootable.IsOpen) continue;
                    if (lootable is not IInteractable interactable || !interactable.CanInteract(gameObject)) continue;

                    if (!LootContainerUI.Instance.IsShowingOpenedLootable)
                    {
                        LootContainerUI.Instance.ShowOpenedLootableFromInventory(lootable, gameObject, "ProximityChange:auto-show");
                        Debug.Log($"[LOOT_TAB_FLOW] ProximityChange: auto-showed '{interactable.InteractLabel}' (inventory closed).");
                    }
                    return;
                }

                // No open lootable left in range.
                // LootContainerUI.Update() handles the walk-away distance check for interactive opens.
                // This branch catches container-closed / container-despawned events specifically.
                if (LootContainerUI.Instance.IsShowingOpenedLootable)
                {
                    LootContainerUI.Instance.Hide();
                    Debug.Log("[LOOT_TAB_FLOW] ProximityChange: no open lootable in range, hiding loot panel.");
                }
            }
        }

        /// <summary>
        /// Fired by the SPECTATED player's ProximityInteractScanner.
        ///
        /// Rules:
        ///   • Show the first open lootable near the spectated player in read-only mode.
        ///   • Auto-hide when no open lootable remains in range.
        ///   • Closed containers are ignored (spectator cannot open them).
        /// </summary>
        private void HandleSpectatedNearbyLootablesChanged(IReadOnlyList<ILootable> nearbyLootables)
        {
            if (!IsLocalPlayer || !_isSpectating || LootContainerUI.Instance == null) return;

            string spectatedName = NightHunt.Gameplay.Spectator.SpectateManager.Instance
                ?.GetCurrentPlayer()?.DisplayName ?? "???";

            for (int i = 0; i < nearbyLootables.Count; i++)
            {
                var lootable = nearbyLootables[i];
                if (lootable == null || !lootable.IsOpen) continue;

                // Show if not already showing this lootable in spectate mode.
                if (!LootContainerUI.Instance.IsShowingOpenedLootable)
                    LootContainerUI.Instance.ShowForSpectating(lootable, spectatedName);
                return;
            }

            // No open lootable near spectated player — hide spectate panel.
            if (LootContainerUI.Instance.IsSpectateMode)
            {
                LootContainerUI.Instance.HideSpectateView();
                Debug.Log("[SPECTATE] No open lootable near spectated player — hiding panel.");
            }
        }

        private System.Collections.Generic.List<WorldItem> CollectNearbyWorldItems()
        {
            var result = new System.Collections.Generic.List<WorldItem>();

            if (proximityScanner == null)
                return result;

            var nearby = proximityScanner.NearbyWorldItems;
            for (int i = 0; i < nearby.Count; i++)
            {
                if (IsValidNearbyWorldItem(nearby[i]))
                    AddUniqueWorldItem(result, nearby[i]);
            }

            Debug.Log($"[LOOT_TAB_FLOW] CollectNearbyWorldItems count={result.Count} radius={proximityScanner.ScanRadius:F2} scannerWorldItems={nearby.Count}");
            return result;
        }

        private bool IsValidNearbyWorldItem(WorldItem worldItem)
            => worldItem != null && !worldItem.IsPickupPending && worldItem.CanInteract(gameObject);

        private static void AddUniqueWorldItem(System.Collections.Generic.List<WorldItem> list, WorldItem worldItem)
        {
            if (!list.Contains(worldItem))
                list.Add(worldItem);
        }

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
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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
                // Skip hold-type interactables (e.g. closed containers, doors with HoldDuration > 0).
                // Those must be opened via the E-hold path, not the instant F-pickup.
                if (target is IHoldInteractable hold && hold.HoldDuration > 0f)
                {
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                        Debug.Log($"[Pickup] Skipping hold-type interactable on F key: {target.InteractLabel} (hold={hold.HoldDuration:F1}s). Use E to interact.");
                }
                else
                {
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                        Debug.Log($"[Pickup] Single: {target.InteractLabel}");
                    target.Interact(gameObject);
                    return;
                }
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
                            .GetField("_maxPickupDistance",
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
                        if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                            Debug.Log($"[Pickup] Target found but CanInteract == false: {target.InteractLabel} (dist={dist:F2}m)");
                }
                else
                {
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                Debug.Log($"[Pickup] PickupAll: triggered {count} pickup(s).");
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        // PlayerInteractionSystem luôn nằm trên player GameObject — GetComponent<NetworkObject>() là chính xác.
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

        // ── Input subscription helpers ──────────────────────────────────────────

        private void HandleOwnerReady(NetworkPlayer player)
        {
            if (player == _networkPlayer)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInteractionDebugLogs)
                Debug.Log("[PlayerInteractionSystem] Unsubscribed from InteractionInputHandler events.");
        }

        private static string FormatLootStorage(IReadOnlyList<ItemInstanceData> storage)
        {
            if (storage == null) return "null";
            if (storage.Count == 0) return "[]";

            var parts = new List<string>(storage.Count);
            for (int i = 0; i < storage.Count; i++)
            {
                var item = storage[i];
                parts.Add($"{i}:{item.DefinitionID}x{item.Quantity}#{ShortId(item.InstanceID)}");
            }
            return "[" + string.Join(", ", parts) + "]";
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "null";
            return id.Length <= 8 ? id : id.Substring(0, 8);
        }
    }
}



