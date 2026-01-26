using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Items.Data
{
    /// <summary>
    /// Weapon equipment data with attachment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "NightHunt/InteractionSystem/WeaponData")]
    public class WeaponData : EquipmentDataBase
    {
        [Header("Weapon Stats")]
        [SerializeField] private float damage = 50f;
        [SerializeField] private float fireRate = 600f;
        [SerializeField] private float accuracy = 75f;
        [SerializeField] private float recoil = 50f;
        [SerializeField] private float range = 100f;
        [SerializeField] private float reloadSpeed = 2f;

        // Properties
        public float Damage => damage;
        public float FireRate => fireRate;
        public float Accuracy => accuracy;
        public float Recoil => recoil;
        public float Range => range;
        public float ReloadSpeed => reloadSpeed;

        protected override void OnEnable()
        {
            base.OnEnable();
            equipmentSlot = EquipmentSlot.PrimaryWeapon;

            // Default weapon attachment slots
            if (attachmentSlots == null || attachmentSlots.Length == 0)
            {
                attachmentSlots = new AttachmentSlotDefinition[]
                {
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Scope, "Scope", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Barrel, "Barrel", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Magazine, "Magazine", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Grip, "Grip", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Stock, "Stock", Vector3.zero)
                };
            }
        }
    }
}
