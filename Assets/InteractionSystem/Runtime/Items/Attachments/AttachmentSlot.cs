using System.Linq;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public class AttachmentSlot : IAttachmentSlot
    {
        private readonly AttachmentSlotDefinition definition;
        private AttachmentData currentAttachment;
        private GameObject spawnedVisual;
        private Transform attachmentPoint;

        public AttachmentSlotType SlotType => definition.slotType;
        public AttachmentData CurrentAttachment => currentAttachment;
        public bool IsOccupied => currentAttachment != null;

        public AttachmentSlot(AttachmentSlotDefinition def, Transform parent)
        {
            definition = def;

            // Create attachment point
            GameObject pointObj = new GameObject($"AttachmentPoint_{def.slotType}");
            pointObj.transform.SetParent(parent);
            pointObj.transform.localPosition = def.attachmentPointOffset;
            pointObj.transform.localRotation = def.attachmentPointRotation;
            attachmentPoint = pointObj.transform;
        }

        public bool CanAttach(AttachmentData attachment)
        {
            if (attachment == null) return false;
            if (IsOccupied) return false;

            // Check if attachment type is accepted by this slot
            return definition.acceptedTypes.Contains(attachment.attachmentType);
        }

        public void Attach(AttachmentData attachment)
        {
            if (!CanAttach(attachment))
            {
                Debug.LogError($"Cannot attach {attachment.displayName} to slot {SlotType}");
                return;
            }

            currentAttachment = attachment;

            // Spawn visual
            if (attachment.attachmentPrefab != null)
            {
                spawnedVisual = UnityEngine.Object.Instantiate(
                    attachment.attachmentPrefab,
                    attachmentPoint
                );

                spawnedVisual.transform.localPosition = attachment.attachmentOffset;
                spawnedVisual.transform.localRotation = attachment.attachmentRotation;
            }
        }

        public void Detach()
        {
            if (!IsOccupied) return;

            currentAttachment = null;

            if (spawnedVisual != null)
            {
                UnityEngine.Object.Destroy(spawnedVisual);
                spawnedVisual = null;
            }
        }

        public void Cleanup()
        {
            Detach();

            if (attachmentPoint != null)
            {
                UnityEngine.Object.Destroy(attachmentPoint.gameObject);
            }
        }
    }
}