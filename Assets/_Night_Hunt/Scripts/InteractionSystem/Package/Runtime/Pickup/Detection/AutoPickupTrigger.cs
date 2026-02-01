using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Pickup.Handlers;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Pickup.Detection
{
    /// <summary>
    /// Trigger-based auto pickup system.
    /// Automatically picks up items when player enters trigger zone (if auto pickup is enabled).
    /// Uses ItemCategory enum from ItemDataBase for category filtering.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class AutoPickupTrigger : MonoBehaviour
    {
        [Header("Auto Pickup Settings")]
        [SerializeField] private float autoPickupRadius = 2f;
        [SerializeField] private bool checkCategories = true;
        [SerializeField] private ItemCategory[] autoPickupCategories = new ItemCategory[] 
        { 
            ItemCategory.Ammo, 
            ItemCategory.Consumable 
        };

        private SphereCollider triggerCollider;
        private PickupHandler pickupHandler;
        private PickupSettings pickupSettings;

        private void Awake()
        {
            triggerCollider = GetComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = autoPickupRadius;

            pickupHandler = ComponentFinder.FindComponentInHierarchy<PickupHandler>(gameObject, includeInactive: false);
            if (pickupHandler == null)
            {
                pickupHandler = FindObjectOfType<PickupHandler>();
            }

            // Find pickup settings (should be on player or in scene)
            pickupSettings = FindObjectOfType<PickupSettings>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if auto pickup is enabled
            if (pickupSettings == null || !pickupSettings.AutoPickupEnabled)
                return;

            // Check if the collider has an IPickupable component
            IPickupable pickupable = other.GetComponent<IPickupable>();
            if (pickupable == null)
                return;

            // Check if player can pickup
            GameObject player = pickupHandler != null ? pickupHandler.gameObject : gameObject;
            if (!pickupable.CanPickup(player))
                return;

            // Check category filter if enabled
            if (checkCategories && autoPickupCategories.Length > 0)
            {
                ItemDataBase itemData = pickupable.GetItemData();
                if (itemData != null)
                {
                    bool categoryAllowed = false;
                    foreach (var category in autoPickupCategories)
                    {
                        if (itemData.Category == category)
                        {
                            categoryAllowed = true;
                            break;
                        }
                    }

                    if (!categoryAllowed)
                        return;
                }
            }

            // Auto pickup the item
            if (pickupHandler != null)
            {
                pickupHandler.AutoPickup(pickupable);
            }
        }

        /// <summary>
        /// Set the auto pickup radius.
        /// </summary>
        public void SetRadius(float radius)
        {
            autoPickupRadius = radius;
            if (triggerCollider != null)
            {
                triggerCollider.radius = radius;
            }
        }

        /// <summary>
        /// Set the auto pickup categories.
        /// </summary>
        public void SetAutoPickupCategories(ItemCategory[] categories)
        {
            autoPickupCategories = categories;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, autoPickupRadius);
        }
    }
}
