using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Items.Data;
using NightHunt.InteractionSystem.Items.Attachments;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Equipment
{
    /// <summary>
    /// Handles weapon-specific equipment logic.
    /// </summary>
    public class WeaponEquipHandler : MonoBehaviour
    {
        private EquipmentManager equipmentManager;
        private AttachmentManager attachmentManager;

        private void Awake()
        {
            // Use ComponentFinder to search in hierarchy (component might be in parent or child)
            equipmentManager = ComponentFinder.FindComponentInHierarchy<EquipmentManager>(gameObject, includeInactive: false);
        }

        /// <summary>
        /// Equip a weapon.
        /// </summary>
        public void EquipWeapon(ItemInstance weaponInstance, WeaponData weaponData)
        {
            if (weaponData == null)
                return;

            // Equip via equipment manager
            equipmentManager?.EquipItem(weaponData.EquipmentSlot, weaponInstance, weaponData);

            // Setup attachment manager for weapon
            SetupAttachmentManager(weaponData);
        }

        /// <summary>
        /// Setup attachment manager for weapon.
        /// </summary>
        private void SetupAttachmentManager(WeaponData weaponData)
        {
            if (attachmentManager == null)
            {
                // Use ComponentFinder to search in hierarchy (component might be in child)
                attachmentManager = ComponentFinder.FindComponentInHierarchy<AttachmentManager>(gameObject, includeInactive: false);
                if (attachmentManager == null)
                {
                    GameObject attachmentObj = new GameObject("AttachmentManager");
                    attachmentObj.transform.SetParent(transform);
                    attachmentManager = attachmentObj.AddComponent<AttachmentManager>();
                }
            }

            // Initialize attachment manager with weapon data
            // This will be handled by AttachmentManager itself
        }
    }
}
