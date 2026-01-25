using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
     public abstract class ItemDataBase : ScriptableObject
     {
          [Header("Base Info")]
          public string itemId;
          public string displayName;
          [TextArea] public string description;
          public Sprite icon;
          public GameObject worldPrefab;
    
          [Header("Properties")]
          public ItemCategory category;
          public ItemRarity rarity;
          public float weight;
          public int maxStack = 1;
          public bool isDroppable = true;
    
          [Header("Grid")]
          public Vector2Int gridSize = Vector2Int.one;
    
          public virtual bool CanStack(ItemInstance other)
          {
               return other.itemDataId == itemId && maxStack > 1;
          }
     }

     public enum ItemCategory
     {
          Weapon, Armor, Helmet, Backpack,
          Consumable, Ammo, Attachment, Misc
     }

     public enum ItemRarity
     {
          Common, Uncommon, Rare, Epic, Legendary
     }
}