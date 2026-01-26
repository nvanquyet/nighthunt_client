using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Items;
using UnityEngine;

namespace NightHunt.InteractionSystem.Equipment
{
    public class WeaponEquipHandler : MonoBehaviour
    {
        [Header("Weapon Settings")] [SerializeField]
        private EquipmentManager equipmentManager;

        [SerializeField] private Animator characterAnimator;

        [Header("Animation")] [SerializeField] private string weaponTypeParameter = "WeaponType";

        private WeaponController currentWeapon;

        public void EquipWeapon(EquipmentSlot slot)
        {
            ItemInstance? item = equipmentManager.GetEquippedItem(slot);
            if (!item.HasValue) return;

            WeaponData weaponData = ItemDatabaseManager.Instance.GetItemData<WeaponData>(item.Value.itemDataId);
            if (weaponData == null) return;

            // Get weapon visual from equipment manager
            Transform mountPoint = equipmentManager.GetMountPoint(slot);
            if (mountPoint == null) return;

            // Find WeaponController component
            currentWeapon = mountPoint.GetComponentInChildren<WeaponController>();
            if (currentWeapon != null)
            {
                currentWeapon.Initialize(weaponData, item.Value);
            }

            // Update animations
            UpdateWeaponAnimations(weaponData);
        }

        public void UnequipWeapon(EquipmentSlot slot)
        {
            currentWeapon = null;

            // Reset animations
            if (characterAnimator != null)
            {
                characterAnimator.SetInteger(weaponTypeParameter, 0);
            }
        }

        private void UpdateWeaponAnimations(WeaponData weaponData)
        {
            if (characterAnimator == null) return;

            int animType = weaponData.weaponType switch
            {
                WeaponType.Rifle => 1,
                WeaponType.Pistol => 2,
                WeaponType.Shotgun => 3,
                WeaponType.Sniper => 4,
                _ => 0
            };

            characterAnimator.SetInteger(weaponTypeParameter, animType);
        }

        public WeaponController GetCurrentWeapon()
        {
            return currentWeapon;
        }
    }

}