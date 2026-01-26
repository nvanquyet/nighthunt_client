using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Items.Data
{
    /// <summary>
    /// Armor equipment data with attachment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "ArmorData", menuName = "NightHunt/InteractionSystem/ArmorData")]
    public class ArmorData : EquipmentDataBase
    {
        [Header("Armor Stats")]
        [SerializeField] private float armorValue = 50f;
        [SerializeField] private float damageReduction = 0.3f;
        [SerializeField] private float movementSpeedModifier = 1f;

        // Properties
        public float ArmorValue => armorValue;
        public float DamageReduction => damageReduction;
        public float MovementSpeedModifier => movementSpeedModifier;

        protected override void OnEnable()
        {
            base.OnEnable();
            equipmentSlot = EquipmentSlot.Body;

            // Default armor attachment slots
            if (attachmentSlots == null || attachmentSlots.Length == 0)
            {
                attachmentSlots = new AttachmentSlotDefinition[]
                {
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Plate, "Plate", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Pouch1, "Pouch 1", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Pouch2, "Pouch 2", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Pouch3, "Pouch 3", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Hydration, "Hydration", Vector3.zero)
                };
            }
        }
    }
}
