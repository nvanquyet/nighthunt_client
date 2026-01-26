using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Items.Data
{
    /// <summary>
    /// Attachment item data (scopes, grips, plates, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "AttachmentData", menuName = "NightHunt/InteractionSystem/AttachmentData")]
    public class AttachmentData : ItemDataBase
    {
        [Header("Attachment Settings")]
        [SerializeField] private string attachmentType = "Tactical";
        [SerializeField] private Core.Interfaces.AttachmentSlotType[] compatibleSlots = new Core.Interfaces.AttachmentSlotType[0];
        [SerializeField] private StatModifier[] statModifiers = new StatModifier[0];
        [SerializeField] private GameObject attachmentPrefab;

        // Properties
        public string AttachmentType => attachmentType;
        public Core.Interfaces.AttachmentSlotType[] CompatibleSlots => compatibleSlots;
        public StatModifier[] StatModifiers => statModifiers;
        public GameObject AttachmentPrefab => attachmentPrefab;

        protected override void OnEnable()
        {
            base.OnEnable();
            category = ItemCategory.Attachment;
        }

        /// <summary>
        /// Check if this attachment is compatible with a slot type.
        /// </summary>
        public bool IsCompatibleWith(Core.Interfaces.AttachmentSlotType slotType)
        {
            if (compatibleSlots == null || compatibleSlots.Length == 0)
                return true; // No restrictions

            foreach (var slot in compatibleSlots)
            {
                if (slot == slotType)
                    return true;
            }

            return false;
        }
    }
}
