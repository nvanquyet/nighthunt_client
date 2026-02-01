using UnityEngine;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.UI;

namespace NightHunt.Gameplay.UI.Config
{
    /// <summary>
    /// Config for equipment parts (body parts that can have equipment)
    /// Defines which body parts exist and their slot types
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentPartsConfig", menuName = "NightHunt/UI/EquipmentPartsConfig")]
    public class EquipmentPartsConfig : ScriptableObject
    {
        [Header("Equipment Parts")]
        [Tooltip("Prefab for the equipment slot UI (spawned dynamically)")]
        public GameObject slotPrefab;
        
        [Tooltip("List of body parts that can have equipment slots")]
        public List<EquipmentPartData> parts = new List<EquipmentPartData>();
        
        [System.Serializable]
        public class EquipmentPartData
        {
            [Tooltip("Unique ID for this part (e.g., 'Head', 'Body', 'Legs', 'Arms')")]
            public string partId;

            [Tooltip("Display name shown in UI")]
            public string displayName;

            [Tooltip("Slot type for this part")]
            public EquipmentSlotType slotType;

            [Tooltip("Icon for this part (optional)")]
            public Sprite partIcon;
        }

        /// <summary>
        /// Get part data by ID
        /// </summary>
        public EquipmentPartData GetPartById(string partId)
        {
            return parts.Find(p => p.partId == partId);
        }

        /// <summary>
        /// Get part data by slot type
        /// </summary>
        public EquipmentPartData GetPartBySlotType(EquipmentSlotType slotType)
        {
            return parts.Find(p => p.slotType == slotType);
        }
    }
}
