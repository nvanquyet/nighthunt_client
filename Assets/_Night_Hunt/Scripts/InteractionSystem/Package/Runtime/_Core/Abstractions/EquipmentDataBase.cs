using UnityEngine;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.InteractionSystem.Core.Abstractions
{
    /// <summary>
    /// Base class for all equipment data (weapons, armor, helmets, backpacks).
    /// Supports universal attachment system.
    /// </summary>
    public abstract class EquipmentDataBase : ItemDataBase
    {
        [Header("Equipment Properties")]
        [SerializeField] protected EquipmentSlot equipmentSlot;
        [SerializeField] protected GameObject equipmentPrefab;

        [Header("Attachment Slots")]
        [SerializeField] protected AttachmentSlotDefinition[] attachmentSlots = new AttachmentSlotDefinition[0];

        [Header("Base Stats")]
        [SerializeField] protected StatModifier[] baseStatModifiers = new StatModifier[0];

        // Properties
        public EquipmentSlot EquipmentSlot => equipmentSlot;
        public GameObject EquipmentPrefab => equipmentPrefab;
        public AttachmentSlotDefinition[] AttachmentSlots => attachmentSlots;
        public StatModifier[] BaseStatModifiers => baseStatModifiers;

        /// <summary>
        /// Get attachment slot by type.
        /// </summary>
        public AttachmentSlotDefinition? GetAttachmentSlot(Interfaces.AttachmentSlotType slotType)
        {
            foreach (var slot in attachmentSlots)
            {
                if (slot.slotType == slotType)
                    return slot;
            }
            return null;
        }

        /// <summary>
        /// Check if this equipment has a specific attachment slot.
        /// </summary>
        public bool HasAttachmentSlot(Interfaces.AttachmentSlotType slotType)
        {
            return GetAttachmentSlot(slotType).HasValue;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Equipment-specific initialization can be done here or in derived classes
        }
    }

    /// <summary>
    /// Equipment slots on the player.
    /// </summary>
    public enum EquipmentSlot
    {
        PrimaryWeapon,
        SecondaryWeapon,
        MeleeWeapon,
        Head,
        Body,
        Backpack,
        Utility
    }
}
