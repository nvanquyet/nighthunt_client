using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Pickup.Handlers
{
    /// <summary>
    /// ScriptableObject configuration for pickup settings per player.
    /// </summary>
    [CreateAssetMenu(fileName = "PickupSettings", menuName = "NightHunt/InteractionSystem/PickupSettings")]
    public class PickupSettings : ScriptableObject
    {
        [Header("Auto Pickup")]
        [SerializeField] private bool autoPickupEnabled = false;
        [SerializeField] private float autoPickupRadius = 2f;
        [SerializeField] private ItemCategory[] autoPickupCategories = new ItemCategory[] 
        { 
            ItemCategory.Ammo, 
            ItemCategory.Consumable 
        };

        [Header("Manual Pickup")]
        [SerializeField] private bool showPickupPrompt = true;
        [SerializeField] private float pickupPromptRange = 5f;

        // Properties
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
