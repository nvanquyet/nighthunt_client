using System.Linq;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Pickup
{
     [CreateAssetMenu(fileName = "PickupSettings", menuName = "NightHunt/Pickup Settings")]
     public class PickupSettings : ScriptableObject
     {
          [Header("Auto Pickup")]
          public bool autoPickupEnabled = false;
          public float autoPickupRadius = 2f;
    
          [Header("Categories")]
          public ItemCategory[] autoPickupCategories = new ItemCategory[]
          {
               ItemCategory.Ammo,
               ItemCategory.Consumable
          };
    
          [Header("Filters")]
          public ItemRarity minimumRarity = ItemRarity.Common;
          public bool autoPickupWeapons = false;
          public bool autoPickupAttachments = false;
    
          public bool ShouldAutoPickup(ItemDataBase itemData)
          {
               if (!autoPickupEnabled) return false;
               if (itemData.rarity < minimumRarity) return false;
        
               return autoPickupCategories.Contains(itemData.category);
          }
     }
}