using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Items.Data
{
    /// <summary>
    /// Helmet equipment data with attachment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "HelmetData", menuName = "NightHunt/InteractionSystem/HelmetData")]
    public class HelmetData : EquipmentDataBase
    {
        [Header("Helmet Stats")]
        [SerializeField] private float headshotProtection = 0.5f;
        [SerializeField] private float visionRange = 20f;
        [SerializeField] private float detectionRange = 15f;

        // Properties
        public float HeadshotProtection => headshotProtection;
        public float VisionRange => visionRange;
        public float DetectionRange => detectionRange;

        protected override void OnEnable()
        {
            base.OnEnable();
            equipmentSlot = EquipmentSlot.Head;

            // Default helmet attachment slots
            if (attachmentSlots == null || attachmentSlots.Length == 0)
            {
                attachmentSlots = new AttachmentSlotDefinition[]
                {
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.NightVision, "Night Vision", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Flashlight, "Flashlight", Vector3.zero),
                    new AttachmentSlotDefinition(Core.Interfaces.AttachmentSlotType.Camera, "Camera", Vector3.zero)
                };
            }
        }
    }
}
