using System;
using NightHunt.Inventory.Core.Enums;
using UnityEngine;

namespace NightHunt.Inventory.Domain.Equipment
{
    /// <summary>
    /// Configuration for equipment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentConfig", menuName = "NightHunt/Inventory/Equipment Config")]
    public class EquipmentSlotsConfig : ScriptableObject
    {
        public EquipmentSlotData[] Slots;
        
        [Serializable]
        public class EquipmentSlotData
        {
            public EquipmentSlotType SlotType;
            public Sprite SlotIcon;
            public string SlotName;
            public string SlotDescription;
        }
        
        public EquipmentSlotData GetSlotData(EquipmentSlotType slotType)
        {
            return Array.Find(Slots, s => s.SlotType == slotType);
        }
    }
}