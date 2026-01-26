using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Items.Data;
using AttachmentSlotType = NightHunt.InteractionSystem.Core.Interfaces.AttachmentSlotType;

namespace NightHunt.InteractionSystem.Items.Attachments
{
    /// <summary>
    /// Individual attachment slot on equipment.
    /// </summary>
    public class AttachmentSlot : MonoBehaviour, IAttachmentSlot
    {
        [Header("Slot Settings")]
        [SerializeField] private AttachmentSlotType slotType;
        [SerializeField] private AttachmentSlotDefinition slotDefinition;
        [SerializeField] private Transform attachmentPoint;

        private AttachmentData attachedAttachment;
        private GameObject attachedVisual;

        // Properties
        public AttachmentSlotType GetSlotType() => slotType;
        public bool IsOccupied() => attachedAttachment != null;
        public AttachmentData GetAttachedAttachment() => attachedAttachment;

        /// <summary>
        /// Check if an attachment can be attached to this slot.
        /// </summary>
        public bool CanAttach(AttachmentData attachment)
        {
            if (attachment == null)
                return false;

            if (IsOccupied())
                return false;

            // Check compatibility
            if (!attachment.IsCompatibleWith(slotType))
                return false;

            // Check slot definition restrictions
            if (slotDefinition.allowedAttachmentTypes != null && slotDefinition.allowedAttachmentTypes.Length > 0)
            {
                if (!slotDefinition.IsAttachmentTypeAllowed(attachment.AttachmentType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Attach an attachment to this slot.
        /// </summary>
        public bool Attach(AttachmentData attachment)
        {
            if (!CanAttach(attachment))
                return false;

            attachedAttachment = attachment;

            // Spawn visual if prefab exists
            if (attachment.AttachmentPrefab != null && attachmentPoint != null)
            {
                attachedVisual = Instantiate(attachment.AttachmentPrefab, attachmentPoint);
                attachedVisual.transform.localPosition = slotDefinition.attachmentPoint;
                attachedVisual.transform.localRotation = Quaternion.Euler(slotDefinition.rotationOffset);
                attachedVisual.transform.localScale = Vector3.one * slotDefinition.scaleMultiplier;
            }

            return true;
        }

        /// <summary>
        /// Remove the attachment from this slot.
        /// </summary>
        public bool Detach()
        {
            if (!IsOccupied())
                return false;

            // Destroy visual
            if (attachedVisual != null)
            {
                Destroy(attachedVisual);
                attachedVisual = null;
            }

            attachedAttachment = null;
            return true;
        }
    }
}
