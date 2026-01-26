using System.Collections.Generic;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Items;
using UnityEngine;

namespace NightHunt.InteractionSystem.Equipment
{
    public class EquipmentVisualController : MonoBehaviour
    {
        [Header("Visual Settings")] [SerializeField]
        private bool hidePlayerMeshWhenEquipped = true;

        private Dictionary<EquipmentSlot, GameObject> spawnedVisuals = new Dictionary<EquipmentSlot, GameObject>();
        private EquipmentManager equipmentManager;

        private void Awake()
        {
            equipmentManager = GetComponent<EquipmentManager>();
        }

        public void UpdateEquipmentVisual(EquipmentSlot slot, ItemInstance item)
        {
            // Clear existing visual
            ClearEquipmentVisual(slot);

            // Get item data
            EquipmentDataBase equipData = ItemDatabaseManager.Instance.GetItemData<EquipmentDataBase>(item.itemDataId);
            if (equipData == null || equipData.worldPrefab == null) return;

            // Get mount point
            Transform mountPoint = GetMountPoint(slot);
            if (mountPoint == null) return;

            // Spawn visual
            GameObject visual = Instantiate(equipData.worldPrefab, mountPoint);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            spawnedVisuals[slot] = visual;

            // Hide player mesh parts if needed
            if (hidePlayerMeshWhenEquipped)
            {
                HidePlayerMeshForSlot(slot);
            }
        }

        public void ClearEquipmentVisual(EquipmentSlot slot)
        {
            if (spawnedVisuals.TryGetValue(slot, out GameObject visual))
            {
                Destroy(visual);
                spawnedVisuals.Remove(slot);
            }

            // Restore player mesh
            if (hidePlayerMeshWhenEquipped)
            {
                RestorePlayerMeshForSlot(slot);
            }
        }

        private Transform GetMountPoint(EquipmentSlot slot)
        {
            // Delegate to EquipmentManager
            return equipmentManager.GetMountPoint(slot);
        }

        private void HidePlayerMeshForSlot(EquipmentSlot slot)
        {
            // TODO: Implement based on player model
            // Example: Hide head mesh when helmet equipped
        }

        private void RestorePlayerMeshForSlot(EquipmentSlot slot)
        {
            // TODO: Restore player mesh parts
        }
    }
}