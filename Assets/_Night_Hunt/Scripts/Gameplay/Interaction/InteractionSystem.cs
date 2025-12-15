using UnityEngine;
using NightHunt.Gameplay.Loot;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Handles player interactions: loot pickup, doors, beacons, etc.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private LayerMask interactionLayers = -1;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("UI")]
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private UnityEngine.UI.Text interactionText;

        private CharacterStats characterStats;
        private InventorySystem inventorySystem;
        private IInteractable currentInteractable;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            inventorySystem = GetComponent<InventorySystem>();
        }

        private void Update()
        {
            CheckForInteractables();
            HandleInteractionInput();
        }

        /// <summary>
        /// Check for nearby interactables
        /// </summary>
        private void CheckForInteractables()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, interactionLayers);
            
            IInteractable closestInteractable = null;
            float closestDistance = float.MaxValue;

            foreach (var collider in colliders)
            {
                IInteractable interactable = collider.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestInteractable = interactable;
                    }
                }
            }

            // Update current interactable
            if (currentInteractable != closestInteractable)
            {
                if (currentInteractable != null)
                {
                    currentInteractable.OnInteractionEnd(gameObject);
                }

                currentInteractable = closestInteractable;

                if (currentInteractable != null)
                {
                    currentInteractable.OnInteractionStart(gameObject);
                    ShowInteractionPrompt(currentInteractable);
                }
                else
                {
                    HideInteractionPrompt();
                }
            }
        }

        /// <summary>
        /// Handle interaction input
        /// </summary>
        private void HandleInteractionInput()
        {
            if (UnityEngine.Input.GetKeyDown(interactKey) && currentInteractable != null)
            {
                currentInteractable.Interact(gameObject);
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

