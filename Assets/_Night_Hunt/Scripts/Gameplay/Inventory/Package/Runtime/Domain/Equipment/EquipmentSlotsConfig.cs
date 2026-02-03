using System;
using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Configuration for equipment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentConfig", menuName = "Inventory/EquipmentConfig")]
    public class EquipmentSlotsConfig : ScriptableObject
    {
        public EquipmentSlotData[] Slots;
        
        [Serializable]
        public class EquipmentSlotData
        {
            public EquipmentSlotType SlotType;
            public Sprite SlotIcon;
            public string SlotName; // For tooltip
            public string SlotDescription;
        }
        
        public EquipmentSlotData GetSlotData(EquipmentSlotType slotType)
        {
            return System.Array.Find(Slots, s => s.SlotType == slotType);
        }
    }
}
