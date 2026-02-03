using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.QuickSlot
{
    /// <summary>
    /// Manages quickslot items (consumables and throwables).
    /// </summary>
    public class QuickSlotManager : MonoBehaviour
    {
        [SerializeField] private QuickSlotConfig config;
        
        private ItemInstance[] quickSlots;
        
        public int SlotCount => config != null ? config.SlotCount : 4;
        
        void Awake()
        {
            int slotCount = config != null ? config.SlotCount : 4;
            quickSlots = new ItemInstance[slotCount];
        }
        
        public bool TrySetSlot(int index, ItemInstance item)
        {
            if (index < 0 || index >= quickSlots.Length) return false;
            
            // Validate item type
            if (item != null && 
                item.Definition.ItemType != ItemType.Consumable && 
                item.Definition.ItemType != ItemType.Throwable)
            {
                return false;
            }
            
            quickSlots[index] = item;
            return true;
        }
        
        public ItemInstance GetSlot(int index)
        {
            if (index < 0 || index >= quickSlots.Length) return null;
            return quickSlots[index];
        }
        
        public List<ItemInstance> GetAllItems()
        {
            return quickSlots.Where(s => s != null).ToList();
        }
    }
}
