using System;
using NightHunt.Inventory.Core.Enums;
using UnityEngine;

namespace NightHunt.Inventory.Domain.Weapon
{
    /// <summary>
    /// Configuration for weapon slots.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponSlotConfig", menuName = "NightHunt/Inventory/Weapon Slot Config")]
    public class WeaponSlotConfig : ScriptableObject
    {
        public WeaponSlotData[] Slots;
        
        [Serializable]
        public class WeaponSlotData
        {
            public WeaponSlotType SlotType;
            public Sprite SlotIcon;
            public string SlotName;
            public string SlotDescription;
        }
        
        public WeaponSlotData GetSlotData(WeaponSlotType slotType)
        {
            return Array.Find(Slots, s => s.SlotType == slotType);
        }
    }
}