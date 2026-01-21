using UnityEngine;
using NightHunt.Gameplay.Loot;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Input;
using NightHunt.Networking;
using NightHunt.Settings;
using FishNet.Object;
using TMPro;
using NightHunt.Data;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Handles player interactions: loot pickup, doors, beacons, etc.
    /// Uses center-screen raycast for targeting (TPP camera)
    /// Integrates with Unity New Input System via PlayerInputHandler
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float maxInteractionDistance = 5f;
        [SerializeField] private LayerMask interactableLayerMask = -1;

        [Header("UI")]
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private TextMeshProUGUI interactionText;

        [Header("Visual")]
        [SerializeField] private GameObject highlightEffect; // VFX ring/outline for interactables

        private UnityEngine.Camera playerCamera;
        private PlayerInputHandler inputHandler;
        private NetworkInteractionController networkInteractionController;
        private NetworkPlayer networkPlayer;
        private NetworkLootItem currentLootTarget;
        private IInteractable currentInteractable;
        private bool autoLootEnabled = false;

        private void Awake()
        {
            inputHandler = GetComponent<PlayerInputHandler>();
            networkInteractionController = GetComponent<NetworkInteractionController>();
            networkPlayer = GetComponent<NetworkPlayer>();
        }

        private void Start()
        {
            // Get camera reference
            if (networkPlayer != null && networkPlayer.PlayerCamera != null)
            {
                playerCamera = networkPlayer.PlayerCamera.GetComponent<UnityEngine.Camera>();
            }
            if (playerCamera == null)
            {
                playerCamera = UnityEngine.Camera.main;
            }

            // Load AutoLoot setting
            LoadAutoLootSetting();
        }

        private void Update()
        {
            // Only process if local player
            if (networkPlayer == null || !networkPlayer.IsLocalPlayer)
                return;

            // Raycast for interactables
            RaycastForInteractables();

            // Handle interaction input (New Input System)
            HandleInteractionInput();
        }

        /// <summary>
        /// Raycast from camera center-screen to find interactables
        /// </summary>
        private void RaycastForInteractables()
        {
            if (playerCamera == null) return;

            // Center-screen raycast
            Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            RaycastHit hit;

            NetworkLootItem newLootTarget = null;
            IInteractable newInteractable = null;

            if (Physics.Raycast(ray, out hit, maxInteractionDistance, interactableLayerMask))
            {
                // Check for NetworkLootItem (tag = "Loot")
                if (hit.collider.CompareTag("Loot"))
                {
                    newLootTarget = hit.collider.GetComponent<NetworkLootItem>();
                    if (newLootTarget != null && !newLootTarget.IsLooted)
                    {
                        // Check if auto-loot enabled
                        if (autoLootEnabled)
                        {
                            // Auto pickup
                            RequestPickup(newLootTarget);
                            return;
                        }
                    }
                }

                // Check for other interactables
                newInteractable = hit.collider.GetComponent<IInteractable>();
                if (newInteractable != null && newInteractable.CanInteract(gameObject))
                {
                    // Found interactable
                }
            }

            // Update current target
            if (currentLootTarget != newLootTarget || currentInteractable != newInteractable)
            {
                // End previous interaction
                if (currentInteractable != null)
                {
                    currentInteractable.OnInteractionEnd(gameObject);
                }
                HideHighlight();

                // Set new target
                currentLootTarget = newLootTarget;
                currentInteractable = newInteractable;

                // Start new interaction
                if (currentLootTarget != null)
                {
                    ShowInteractionPrompt($"Press E to pick up {GetItemDisplayName(currentLootTarget.ItemId)}");
                    ShowHighlight(currentLootTarget.transform);
                }
                else if (currentInteractable != null)
                {
                    currentInteractable.OnInteractionStart(gameObject);
                    ShowInteractionPrompt(currentInteractable.GetInteractionText());
                    ShowHighlight(hit.collider.transform);
                }
                else
                {
                    HideInteractionPrompt();
                }
            }
        }

        /// <summary>
        /// Handle interaction input using New Input System
        /// </summary>
        private void HandleInteractionInput()
        {
            if (inputHandler == null) return;

            // Check if interact button was pressed
            if (inputHandler.IsInteracting())
            {
                if (currentLootTarget != null)
                {
                    RequestPickup(currentLootTarget);
                }
                else if (currentInteractable != null)
                {
                    // Handle other interactables (chest, beacon, etc.)
                    currentInteractable.Interact(gameObject);
                }
            }
        }

        /// <summary>
        /// Request pickup via network RPC
        /// </summary>
        private void RequestPickup(NetworkLootItem lootItem)
        {
            if (networkInteractionController == null || lootItem == null || lootItem.IsLooted)
                return;

            if (!lootItem.IsSpawned)
                return;

            // Send RPC to server
            networkInteractionController.ServerRpc_RequestInteract(
                unchecked((uint)lootItem.ObjectId),
                "Pickup",
                transform.position
            );
        }

        /// <summary>
        /// Load AutoLoot setting from GameSettings
        /// </summary>
        private void LoadAutoLootSetting()
        {
            autoLootEnabled = PlayerPrefs.GetInt("AutoLoot", 0) == 1;
        }

        /// <summary>
        /// Get item display name from config
        /// </summary>
        private string GetItemDisplayName(string itemId)
        {
            // Prefer new config structure, fallback to legacy
            var baseConfig = GameConfigLoader.Instance?.GetItemConfigBase(itemId);
            if (baseConfig != null && !string.IsNullOrEmpty(baseConfig.DisplayName))
                return baseConfig.DisplayName;

            var legacy = GameConfigLoader.Instance?.GetItemConfig(itemId);
            return legacy != null && !string.IsNullOrEmpty(legacy.DisplayName) ? legacy.DisplayName : itemId;
        }

        /// <summary>
        /// Show interaction prompt
        /// </summary>
        private void ShowInteractionPrompt(string text)
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }

            if (interactionText != null)
            {
                interactionText.text = text;
            }
        }

        /// <summary>
        /// Hide interaction prompt
        /// </summary>
        private void HideInteractionPrompt()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }

        /// <summary>
        /// Show highlight effect on interactable
        /// </summary>
        private void ShowHighlight(Transform target)
        {
            if (highlightEffect != null && target != null)
            {
                // Position highlight effect at target (could use outline shader instead)
                highlightEffect.transform.position = target.position;
                highlightEffect.SetActive(true);
            }
        }

        /// <summary>
        /// Hide highlight effect
        /// </summary>
        private void HideHighlight()
        {
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }

        /// <summary>
        /// Show interaction prompt
        /// </summary>
        private void ShowInteractionPrompt(IInteractable interactable)
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }

            if (interactionText != null)
            {
                interactionText.text = interactable.GetInteractionText();
            }
        }
    }

    /// <summary>
    /// Interface for interactable objects
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
        string GetInteractionText();
        void OnInteractionStart(GameObject interactor);
        void OnInteractionEnd(GameObject interactor);
    }

    /// <summary>
    /// Loot item interactable
    /// </summary>
    public class LootInteractable : MonoBehaviour, IInteractable
    {
        private LootItem lootItem;

        private void Awake()
        {
            lootItem = GetComponent<LootItem>();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (lootItem == null || lootItem.IsPickedUp)
                return false;

            var inventory = interactor.GetComponent<InventorySystem>();
            return inventory != null;
        }

        public void Interact(GameObject interactor)
        {
            if (lootItem == null) return;

            var inventory = interactor.GetComponent<InventorySystem>();
            if (inventory != null)
            {
                if (inventory.AddItem(lootItem.ItemId, 1))
                {
                    // Item picked up, destroy loot
                    Destroy(gameObject);
                }
            }
        }

        public string GetInteractionText()
        {
            if (lootItem == null) return "";
            return $"Press E to pick up {lootItem.ItemId}";
        }

        public void OnInteractionStart(GameObject interactor) { }
        public void OnInteractionEnd(GameObject interactor) { }
    }

    /// <summary>
    /// Beacon interactable
    /// </summary>
    public class BeaconInteractable : MonoBehaviour, IInteractable
    {
        private RespawnBeacon beacon;

        private void Awake()
        {
            beacon = GetComponent<RespawnBeacon>();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (beacon == null || !beacon.IsActive)
                return false;

            // Can interact to place or destroy beacon
            return true;
        }

        public void Interact(GameObject interactor)
        {
            // Handle beacon interaction
            // Would need to check if placing or destroying
        }

        public string GetInteractionText()
        {
            return "Press E to interact with beacon";
        }

        public void OnInteractionStart(GameObject interactor) { }
        public void OnInteractionEnd(GameObject interactor) { }
    }
}

