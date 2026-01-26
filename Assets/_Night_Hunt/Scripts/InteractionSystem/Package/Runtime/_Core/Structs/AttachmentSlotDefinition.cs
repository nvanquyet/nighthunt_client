using System;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Core.Structs
{
    /// <summary>
    /// Definition of an attachment slot on equipment.
    /// </summary>
    [Serializable]
    public struct AttachmentSlotDefinition
    {
        /// <summary>
        /// Type of attachment slot.
        /// </summary>
        public AttachmentSlotType slotType;

        /// <summary>
        /// Display name for the slot.
        /// </summary>
        public string displayName;

        /// <summary>
        /// Attachment point in world space (relative to equipment).
        /// </summary>
        public Vector3 attachmentPoint;

        /// <summary>
        /// Rotation offset for attached items.
        /// </summary>
        public Vector3 rotationOffset;

        /// <summary>
        /// Scale multiplier for attached items.
        /// </summary>
        public float scaleMultiplier;

        /// <summary>
        /// Whether this slot is required (must have attachment).
        /// </summary>
        public bool isRequired;

        /// <summary>
        /// Allowed attachment types for this slot (empty = all types allowed).
        /// </summary>
        public string[] allowedAttachmentTypes;

        /// <summary>
        /// Create a new attachment slot definition.
        /// </summary>
        public AttachmentSlotDefinition(
            AttachmentSlotType slotType,
            string displayName,
            Vector3 attachmentPoint,
            Vector3 rotationOffset = default,
            float scaleMultiplier = 1f,
            bool isRequired = false,
            string[] allowedAttachmentTypes = null)
        {
            this.slotType = slotType;
            this.displayName = displayName;
            this.attachmentPoint = attachmentPoint;
            this.rotationOffset = rotationOffset;
            this.scaleMultiplier = scaleMultiplier;
            this.isRequired = isRequired;
            this.allowedAttachmentTypes = allowedAttachmentTypes ?? new string[0];
        }

        /// <summary>
        /// Check if an attachment type is allowed in this slot.
        /// </summary>
        public bool IsAttachmentTypeAllowed(string attachmentType)
        {
            if (allowedAttachmentTypes == null || allowedAttachmentTypes.Length == 0)
                return true;

            foreach (var allowed in allowedAttachmentTypes)
            {
                if (allowed == attachmentType)
                    return true;
            }

            return false;
        }
    }
}
