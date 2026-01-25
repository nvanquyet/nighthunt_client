using System.Linq;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
    [CreateAssetMenu(fileName = "Attachment", menuName = "NightHunt/Items/Attachment")]
    public class AttachmentData : ItemDataBase
    {
        [Header("Attachment Properties")]
        public AttachmentType attachmentType;
        public AttachmentSlotType[] compatibleSlots;
    
        [Header("Visuals")]
        public GameObject attachmentPrefab;
        public Vector3 attachmentOffset;
        public Quaternion attachmentRotation;
    
        [Header("Stat Modifiers")]
        public StatModifier[] modifiers;
    
        [Header("Requirements")]
        public string[] compatibleEquipmentIds; // Empty = all compatible
    
        private void OnValidate()
        {
            category = ItemCategory.Attachment;
            maxStack = 1; // Attachments never stack
        }
    
        public bool IsCompatibleWith(EquipmentDataBase equipment, AttachmentSlotType slotType)
        {
            // Check if this attachment type fits the slot
            if (!compatibleSlots.Contains(slotType)) return false;
        
            // Check equipment whitelist
            if (compatibleEquipmentIds.Length > 0)
            {
                return compatibleEquipmentIds.Contains(equipment.itemId);
            }
        
            return true;
        }
    }
}