using System.Collections.Generic;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Utilities;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public class AttachmentManager : MonoBehaviour
    {
        [Header("Equipment Reference")] [SerializeField]
        private EquipmentDataBase equipmentData;

        private Dictionary<AttachmentSlotType, AttachmentSlot> slots =
            new Dictionary<AttachmentSlotType, AttachmentSlot>();

        private StatCalculator statCalculator;

        public IReadOnlyDictionary<AttachmentSlotType, AttachmentSlot> Slots => slots;

        private void Awake()
        {
            statCalculator = new StatCalculator();
        }

        public void Initialize(EquipmentDataBase equipment, Transform parent)
        {
            equipmentData = equipment;

            // Clear old slots
            ClearAllSlots();

            // Create slots from equipment definition
            if (equipment.attachmentSlots != null)
            {
                foreach (var slotDef in equipment.attachmentSlots)
                {
                    AttachmentSlot slot = new AttachmentSlot(slotDef, parent);
                    slots[slotDef.slotType] = slot;
                }
            }
        }

        public bool TryAttach(AttachmentSlotType slotType, AttachmentData attachment)
        {
            if (!slots.TryGetValue(slotType, out AttachmentSlot slot))
            {
                Debug.LogError($"Slot {slotType} does not exist on {equipmentData.displayName}");
                return false;
            }

            // Validate compatibility
            if (!attachment.IsCompatibleWith(equipmentData, slotType))
            {
                Debug.LogError(
                    $"{attachment.displayName} is not compatible with {slotType} on {equipmentData.displayName}");
                return false;
            }

            if (!slot.CanAttach(attachment))
            {
                return false;
            }

            slot.Attach(attachment);

            // Recalculate stats
            RecalculateStats();

            return true;
        }

        public bool TryDetach(AttachmentSlotType slotType)
        {
            if (!slots.TryGetValue(slotType, out AttachmentSlot slot))
            {
                return false;
            }

            if (!slot.IsOccupied)
            {
                return false;
            }

            slot.Detach();

            // Recalculate stats
            RecalculateStats();

            return true;
        }

        public AttachmentData GetAttachment(AttachmentSlotType slotType)
        {
            if (slots.TryGetValue(slotType, out AttachmentSlot slot))
            {
                return slot.CurrentAttachment;
            }

            return null;
        }

        public StatModifier[] GetFinalStats()
        {
            return statCalculator.CalculateFinalStats(equipmentData, GetAllAttachments());
        }

        private AttachmentData[] GetAllAttachments()
        {
            List<AttachmentData> attachments = new List<AttachmentData>();

            foreach (var slot in slots.Values)
            {
                if (slot.IsOccupied)
                {
                    attachments.Add(slot.CurrentAttachment);
                }
            }

            return attachments.ToArray();
        }

        private void RecalculateStats()
        {
            StatModifier[] finalStats = GetFinalStats();

            // Notify parent equipment
            SendMessage("OnStatsChanged", finalStats, SendMessageOptions.DontRequireReceiver);
        }

        private void ClearAllSlots()
        {
            foreach (var slot in slots.Values)
            {
                slot.Cleanup();
            }

            slots.Clear();
        }

        private void OnDestroy()
        {
            ClearAllSlots();
        }
    }
}