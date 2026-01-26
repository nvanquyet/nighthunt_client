using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Items.Data;
using NightHunt.InteractionSystem.Items.Attachments;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Equipment;

namespace NightHunt.InteractionSystem.Items.Attachments
{
    /// <summary>
    /// Universal attachment manager that works for ALL equipment types.
    /// </summary>
    public class AttachmentManager : MonoBehaviour
    {
        [Header("Equipment Reference")]
        [SerializeField] private EquipmentDataBase equipmentData;
        [SerializeField] private GameObject equipmentModel;

        private Dictionary<Core.Interfaces.AttachmentSlotType, AttachmentSlot> attachmentSlots = new Dictionary<Core.Interfaces.AttachmentSlotType, AttachmentSlot>();
        private StatCalculator statCalculator;

        private void Awake()
        {
            statCalculator = GetComponent<StatCalculator>();
            if (statCalculator == null)
            {
                statCalculator = gameObject.AddComponent<StatCalculator>();
            }

            InitializeSlots();
        }

        /// <summary>
        /// Initialize attachment slots from equipment data.
        /// </summary>
        private void InitializeSlots()
        {
            if (equipmentData == null)
                return;

            foreach (var slotDef in equipmentData.AttachmentSlots)
            {
                // Create slot GameObject
                GameObject slotObj = new GameObject($"Slot_{slotDef.slotType}");
                slotObj.transform.SetParent(transform);
                slotObj.transform.localPosition = slotDef.attachmentPoint;

                // Add AttachmentSlot component
                AttachmentSlot slot = slotObj.AddComponent<AttachmentSlot>();
                slot.GetType().GetField("slotType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(slot, slotDef.slotType);
                slot.GetType().GetField("slotDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(slot, slotDef);

                // Create attachment point
                GameObject attachmentPoint = new GameObject("AttachmentPoint");
                attachmentPoint.transform.SetParent(slotObj.transform);
                attachmentPoint.transform.localPosition = Vector3.zero;

                slot.GetType().GetField("attachmentPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(slot, attachmentPoint.transform);

                attachmentSlots[slotDef.slotType] = slot;
            }
        }

        /// <summary>
        /// Attach an attachment to a slot.
        /// </summary>
        public bool AttachAttachment(Core.Interfaces.AttachmentSlotType slotType, AttachmentData attachment)
        {
            if (!attachmentSlots.ContainsKey(slotType))
                return false;

            AttachmentSlot slot = attachmentSlots[slotType];
            if (slot.Attach(attachment))
            {
                // Recalculate stats
                if (statCalculator != null)
                {
                    statCalculator.RecalculateStats(equipmentData, GetAllAttachments());
                }

                // Invoke event
                EquipmentSlot equipmentSlot = GetEquipmentSlotFromContext();
                InventoryEvents.InvokeAttachmentAttached(equipmentSlot, slotType, attachment);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Detach an attachment from a slot.
        /// </summary>
        public bool DetachAttachment(Core.Interfaces.AttachmentSlotType slotType)
        {
            if (!attachmentSlots.ContainsKey(slotType))
                return false;

            AttachmentSlot slot = attachmentSlots[slotType];
            AttachmentData detachedAttachment = slot.GetAttachedAttachment();
            
            if (slot.Detach())
            {
                // Recalculate stats
                if (statCalculator != null)
                {
                    statCalculator.RecalculateStats(equipmentData, GetAllAttachments());
                }

                // Invoke event
                if (detachedAttachment != null)
                {
                    EquipmentSlot equipmentSlot = GetEquipmentSlotFromContext();
                    InventoryEvents.InvokeAttachmentDetached(equipmentSlot, slotType, detachedAttachment);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Get all currently attached attachments.
        /// </summary>
        public Dictionary<Core.Interfaces.AttachmentSlotType, AttachmentData> GetAllAttachments()
        {
            Dictionary<Core.Interfaces.AttachmentSlotType, AttachmentData> attachments = new Dictionary<Core.Interfaces.AttachmentSlotType, AttachmentData>();

            foreach (var kvp in attachmentSlots)
            {
                if (kvp.Value.IsOccupied())
                {
                    attachments[kvp.Key] = kvp.Value.GetAttachedAttachment();
                }
            }

            return attachments;
        }

        /// <summary>
        /// Get attachment at a specific slot.
        /// </summary>
        public AttachmentData GetAttachment(Core.Interfaces.AttachmentSlotType slotType)
        {
            if (attachmentSlots.ContainsKey(slotType))
            {
                return attachmentSlots[slotType].GetAttachedAttachment();
            }
            return null;
        }

        [Header("Equipment Slot Reference")]
        [SerializeField] private EquipmentSlot equipmentSlot = EquipmentSlot.PrimaryWeapon;

        /// <summary>
        /// Set the equipment slot this AttachmentManager belongs to.
        /// </summary>
        public void SetEquipmentSlot(EquipmentSlot slot)
        {
            equipmentSlot = slot;
        }

        /// <summary>
        /// Get equipment slot from context (parent EquipmentManager or EquipmentVisualController).
        /// </summary>
        private EquipmentSlot GetEquipmentSlotFromContext()
        {
            // First check if slot is explicitly set
            if (equipmentSlot != EquipmentSlot.PrimaryWeapon)
                return equipmentSlot;

            // Try to find EquipmentManager in parent hierarchy
            EquipmentManager equipmentManager = GetComponentInParent<EquipmentManager>();
            if (equipmentManager != null)
            {
                // Check which slot this equipment visual belongs to
                EquipmentVisualController visualController = GetComponentInParent<EquipmentVisualController>();
                if (visualController != null)
                {
                    // Check each slot to find which one contains this visual
                    foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                    {
                        GameObject visual = equipmentManager.GetEquipmentVisual(slot);
                        if (visual != null && transform.IsChildOf(visual.transform))
                        {
                            equipmentSlot = slot; // Cache for next time
                            return slot;
                        }
                    }
                }
            }

            return EquipmentSlot.PrimaryWeapon; // Default fallback
        }
    }
}
