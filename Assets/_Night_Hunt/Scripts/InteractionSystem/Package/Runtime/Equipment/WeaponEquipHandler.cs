using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Items.Data;
using NightHunt.InteractionSystem.Items.Attachments;

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
            equipmentManager = GetComponent<EquipmentManager>();
            if (equipmentManager == null)
            {
                equipmentManager = GetComponentInParent<EquipmentManager>();
            }
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
                attachmentManager = GetComponent<AttachmentManager>();
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
