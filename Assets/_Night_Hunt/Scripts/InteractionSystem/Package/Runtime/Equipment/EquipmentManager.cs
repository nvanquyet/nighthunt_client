using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Items.Attachments;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Equipment
{
    /// <summary>
    /// Manages equipped items on the player.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        private Dictionary<EquipmentSlot, ItemInstance> equippedItems = new Dictionary<EquipmentSlot, ItemInstance>();
        private Dictionary<EquipmentSlot, GameObject> equipmentVisuals = new Dictionary<EquipmentSlot, GameObject>();
        private Dictionary<EquipmentSlot, EquipmentDataBase> equippedData = new Dictionary<EquipmentSlot, EquipmentDataBase>();
        private EquipmentVisualController visualController;

        private void Awake()
        {
            visualController = GetComponent<EquipmentVisualController>();
            if (visualController == null)
            {
                visualController = gameObject.AddComponent<EquipmentVisualController>();
            }
        }

        /// <summary>
        /// Equip an item.
        /// </summary>
        public bool EquipItem(EquipmentSlot slot, ItemInstance item, EquipmentDataBase equipmentData)
        {
            // Unequip existing item if any
            if (equippedItems.ContainsKey(slot))
            {
                UnequipItem(slot);
            }

            // Equip new item
            equippedItems[slot] = item;
            if (equipmentData != null)
            {
                equippedData[slot] = equipmentData;
            }

            // Spawn visual
            if (equipmentData != null && equipmentData.EquipmentPrefab != null)
            {
                GameObject visual = visualController.SpawnEquipmentVisual(equipmentData, slot);
                if (visual != null)
                {
                    equipmentVisuals[slot] = visual;
                }
            }

            // Invoke event
            InventoryEvents.InvokeItemEquipped(slot, item);

            return true;
        }

        /// <summary>
        /// Unequip an item.
        /// </summary>
        public bool UnequipItem(EquipmentSlot slot)
        {
            if (!equippedItems.ContainsKey(slot))
                return false;

            ItemInstance item = equippedItems[slot];

            // Remove visual
            if (equipmentVisuals.ContainsKey(slot))
            {
                visualController.DestroyEquipmentVisual(equipmentVisuals[slot]);
                equipmentVisuals.Remove(slot);
            }

            equippedItems.Remove(slot);
            equippedData.Remove(slot);

            // Invoke event
            InventoryEvents.InvokeItemUnequipped(slot, item);

            return true;
        }

        /// <summary>
        /// Get equipped item at slot.
        /// </summary>
        public ItemInstance? GetEquippedItem(EquipmentSlot slot)
        {
            if (equippedItems.ContainsKey(slot))
                return equippedItems[slot];
            return null;
        }

        /// <summary>
        /// Get all equipped items.
        /// </summary>
        public Dictionary<EquipmentSlot, ItemInstance> GetAllEquippedItems()
        {
            return new Dictionary<EquipmentSlot, ItemInstance>(equippedItems);
        }

        /// <summary>
        /// Get equipped data for a slot.
        /// </summary>
        public EquipmentDataBase GetEquippedData(EquipmentSlot slot)
        {
            if (equippedData.ContainsKey(slot))
                return equippedData[slot];
            return null;
        }

        /// <summary>
        /// Get equipment visual GameObject for a slot.
        /// </summary>
        public GameObject GetEquipmentVisual(EquipmentSlot slot)
        {
            if (equipmentVisuals.ContainsKey(slot))
                return equipmentVisuals[slot];
            return null;
        }
    }
}
