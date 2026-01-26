using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
    // WeaponData extension
    [CreateAssetMenu(fileName = "Weapon", menuName = "NightHunt/Items/Weapon")]
    public class WeaponData : EquipmentDataBase
    {
        [Header("Weapon Stats")] public WeaponType weaponType;
        public float damage = 45f;
        public float fireRate = 600f; // RPM
        public float reloadTime = 2.5f;
        public int magazineSize = 30;

        [Header("Accuracy")] public float baseAccuracy = 75f;
        public float recoil = 50f;

        private void OnValidate()
        {
            category = ItemCategory.Weapon;
            equipmentSlot = EquipmentSlot.PrimaryWeapon; // Default
        }
    }

    public enum WeaponType
    {
        Rifle,
        Pistol,
        Shotgun,
        Sniper,
        SMG,
        LMG
    }
}