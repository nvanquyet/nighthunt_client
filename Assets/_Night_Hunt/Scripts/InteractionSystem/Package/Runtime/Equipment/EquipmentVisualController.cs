using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Equipment
{
    /// <summary>
    /// Controls visual representation of equipped items.
    /// </summary>
    public class EquipmentVisualController : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Transform equipmentParent;
        [SerializeField] private Dictionary<EquipmentSlot, Transform> equipmentSlots = new Dictionary<EquipmentSlot, Transform>();
        
        private Dictionary<EquipmentSlot, GameObject> equipmentVisuals = new Dictionary<EquipmentSlot, GameObject>();

        private void Awake()
        {
            if (equipmentParent == null)
            {
                equipmentParent = transform;
            }

            InitializeEquipmentSlots();
        }

        /// <summary>
        /// Initialize equipment slot transforms.
        /// </summary>
        private void InitializeEquipmentSlots()
        {
            // Create default slots if not assigned
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (!equipmentSlots.ContainsKey(slot))
                {
                    GameObject slotObj = new GameObject($"Slot_{slot}");
                    slotObj.transform.SetParent(equipmentParent);
                    equipmentSlots[slot] = slotObj.transform;
                }
            }
        }

        /// <summary>
        /// Spawn equipment visual.
        /// </summary>
        public GameObject SpawnEquipmentVisual(EquipmentDataBase equipmentData, EquipmentSlot slot)
        {
            if (equipmentData == null || equipmentData.EquipmentPrefab == null)
                return null;

            if (!equipmentSlots.ContainsKey(slot))
                return null;

            Transform slotTransform = equipmentSlots[slot];
            GameObject visual = Instantiate(equipmentData.EquipmentPrefab, slotTransform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Store reference
            equipmentVisuals[slot] = visual;

            return visual;
        }

        /// <summary>
        /// Destroy equipment visual.
        /// </summary>
        public void DestroyEquipmentVisual(GameObject visual)
        {
            if (visual != null)
            {
                // Remove from dictionary
                EquipmentSlot? slotToRemove = null;
                foreach (var kvp in equipmentVisuals)
                {
                    if (kvp.Value == visual)
                    {
                        slotToRemove = kvp.Key;
                        break;
                    }
                }
                if (slotToRemove.HasValue)
                {
                    equipmentVisuals.Remove(slotToRemove.Value);
                }

                Destroy(visual);
            }
        }

        /// <summary>
        /// Get equipment visual GameObject for a slot.
        /// </summary>
        public GameObject GetEquipmentVisual(EquipmentSlot slot)
        {
            if (equipmentVisuals.ContainsKey(slot))
            {
                return equipmentVisuals[slot];
            }
            return null;
        }
    }
}
