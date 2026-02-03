using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.QuickSlot
{
    /// <summary>
    /// Handles quickslot input (Ctrl+1/2/3/4 hotkeys).
    /// </summary>
    public class QuickSlotInputHandler : MonoBehaviour
    {
        [SerializeField] private QuickSlotConfig config;
        
        private bool isInventoryOpen;
        private QuickSlotManager quickSlotManager;
        
        void Awake()
        {
            // Use ComponentFinder to find QuickSlotManager in hierarchy
            quickSlotManager = ComponentFinder.FindInHierarchy<QuickSlotManager>(this);
        }
        
        void OnEnable()
        {
            InventoryEvents.OnInventoryOpened += () => isInventoryOpen = true;
            InventoryEvents.OnInventoryClosed += () => isInventoryOpen = false;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnInventoryOpened -= () => isInventoryOpen = true;
            InventoryEvents.OnInventoryClosed -= () => isInventoryOpen = false;
        }
        
        void Update()
        {
            // Block if inventory open
            if (isInventoryOpen) return;
            
            // Check Ctrl+1/2/3/4
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) UseQuickSlot(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) UseQuickSlot(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) UseQuickSlot(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) UseQuickSlot(3);
            }
        }
        
        void UseQuickSlot(int index)
        {
            if (quickSlotManager == null) return;
            
            var item = quickSlotManager.GetSlot(index);
            if (item == null) return;
            
            if (item.Definition.ItemType == ItemType.Consumable)
            {
                // Start consume with progress bar (can cancel)
                var consumableUsage = ComponentFinder.FindInHierarchy<ConsumableUsage>(this);
                if (consumableUsage != null)
                {
                    consumableUsage.StartConsume(item);
                }
            }
            else if (item.Definition.ItemType == ItemType.Throwable)
            {
                // Instant equip to hand (like switch weapon)
                EquipThrowable(item);
            }
        }
        
        private void EquipThrowable(ItemInstance item)
        {
            // Fire event for combat system (handle weapon switching)
            QuickSlotEvents.FireRequestEquipThrowable(item);
        }
    }
}
