using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Manages equipment slots (Helmet, Armor, Backpack).
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        [SerializeField] private EquipmentSlotsConfig config;
        
        private Dictionary<EquipmentSlotType, ItemInstance> equippedItems;
        
        public event System.Action<ItemInstance> OnItemEquipped;
        public event System.Action<ItemInstance> OnItemUnequipped;
        
        void Awake()
        {
            equippedItems = new Dictionary<EquipmentSlotType, ItemInstance>();
        }
        
        public EquipResult TryEquip(ItemInstance item, EquipmentSlotType targetSlot)
        {
            // Validate slot type matches
            if (item.Definition.EquipmentSlot != targetSlot)
                return EquipResult.Fail("Item cannot be equipped in this slot");
            
            // Check if slot occupied
            if (equippedItems.ContainsKey(targetSlot))
            {
                // Swap: unequip old, equip new
                var oldItem = equippedItems[targetSlot];
                equippedItems[targetSlot] = item;
                
                // Fire events for stat changes
                OnItemUnequipped?.Invoke(oldItem);
                OnItemEquipped?.Invoke(item);
                
                return EquipResult.Swapped(oldItem);
            }
            else
            {
                // Direct equip
                equippedItems[targetSlot] = item;
                OnItemEquipped?.Invoke(item);
                return EquipResult.Success();
            }
        }
        
        public bool TryUnequip(EquipmentSlotType slotType, out ItemInstance unequippedItem)
        {
            if (equippedItems.TryGetValue(slotType, out unequippedItem))
            {
                equippedItems.Remove(slotType);
                OnItemUnequipped?.Invoke(unequippedItem);
                return true;
            }
            return false;
        }
        
        public ItemInstance GetEquipped(EquipmentSlotType slotType)
        {
            return equippedItems.TryGetValue(slotType, out var item) ? item : null;
        }
        
        public List<ItemInstance> GetAllEquipped()
        {
            return equippedItems.Values.ToList();
        }
    }
}
