using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Pickup.Handlers
{
    /// <summary>
    /// ScriptableObject configuration for pickup and interaction settings.
    /// Single source of truth for all detection ranges, layers, and LOS settings.
    /// Uses ItemCategory enum from ItemDataBase for auto pickup filtering.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionSettings", menuName = "NightHunt/InteractionSystem/InteractionSettings")]
    public class PickupSettings : ScriptableObject
    {
        [Header("Pickup Detection")]
        [SerializeField] private float pickupDetectionRange = 5f;
        [SerializeField] private LayerMask pickupLayers = -1;
        [SerializeField] private float maxPickupDistance = 5f;
        [SerializeField] private LayerMask pickupLineOfSightLayers = -1;

        [Header("Interaction Detection")]
        [SerializeField] private float interactionDetectionRange = 5f;
        [SerializeField] private LayerMask interactionLayers = -1;

        [Header("Auto Pickup")]
        [SerializeField] private bool autoPickupEnabled = false;
        [SerializeField] private float autoPickupRadius = 2f;
        [SerializeField] private ItemCategory[] autoPickupCategories = new ItemCategory[] 
        { 
            ItemCategory.Ammo, 
            ItemCategory.Consumable 
        };

        [Header("Manual Pickup (Legacy - kept for compatibility)")]
        [SerializeField] private bool showPickupPrompt = true;
        [SerializeField] private float pickupPromptRange = 5f;

        // Pickup Detection Properties
        public float PickupDetectionRange
        {
            get => pickupDetectionRange;
            set => pickupDetectionRange = value;
        }

        public LayerMask PickupLayers
        {
            get => pickupLayers;
            set => pickupLayers = value;
        }

        public float MaxPickupDistance
        {
            get => maxPickupDistance;
            set => maxPickupDistance = value;
        }

        public LayerMask PickupLineOfSightLayers
        {
            get => pickupLineOfSightLayers;
            set => pickupLineOfSightLayers = value;
        }

        // Interaction Detection Properties
        public float InteractionDetectionRange
        {
            get => interactionDetectionRange;
            set => interactionDetectionRange = value;
        }

        public LayerMask InteractionLayers
        {
            get => interactionLayers;
            set => interactionLayers = value;
        }

        // Auto Pickup Properties
        public bool AutoPickupEnabled
        {
            get => autoPickupEnabled;
            set => autoPickupEnabled = value;
        }

        public float AutoPickupRadius
        {
            get => autoPickupRadius;
            set => autoPickupRadius = value;
        }

        public ItemCategory[] AutoPickupCategories
        {
            get => autoPickupCategories;
            set => autoPickupCategories = value;
        }

        // Legacy Properties (kept for compatibility)
        public bool ShowPickupPrompt
        {
            get => showPickupPrompt;
            set => showPickupPrompt = value;
        }

        public float PickupPromptRange
        {
            get => pickupPromptRange;
            set => pickupPromptRange = value;
        }

        /// <summary>
        /// Check if a category is allowed for auto pickup.
        /// </summary>
        public bool IsCategoryAllowedForAutoPickup(ItemCategory category)
        {
            if (!autoPickupEnabled)
                return false;

            if (autoPickupCategories == null || autoPickupCategories.Length == 0)
                return true;

            foreach (var allowedCategory in autoPickupCategories)
            {
                if (allowedCategory == category)
                    return true;
            }

            return false;
        }
    }
}
