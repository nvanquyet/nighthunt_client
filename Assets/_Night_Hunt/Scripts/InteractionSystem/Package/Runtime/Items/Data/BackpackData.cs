using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Items.Data
{
    /// <summary>
    /// Backpack equipment data with attachment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "BackpackData", menuName = "NightHunt/InteractionSystem/BackpackData")]
    public class BackpackData : EquipmentDataBase
    {
        [Header("Backpack Stats")]
        [SerializeField] private int additionalSlots = 12;
        [SerializeField] private float additionalWeightCapacity = 10f;

        // Properties
        public int AdditionalSlots => additionalSlots;
        public float AdditionalWeightCapacity => additionalWeightCapacity;

        protected override void OnEnable()
        {
            base.OnEnable();
            equipmentSlot = EquipmentSlot.Backpack;

            // Default backpack attachment slots
            if (attachmentSlots == null || attachmentSlots.Length == 0)
            {
                attachmentSlots = new AttachmentSlotDefinition[]
                {
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.BackpackPouch1, "Pouch 1", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.BackpackPouch2, "Pouch 2", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.BackpackHydration, "Hydration", Vector3.zero)
                };
            }
        }
    }
}
