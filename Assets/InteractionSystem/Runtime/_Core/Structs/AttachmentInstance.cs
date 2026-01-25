using System;

namespace NightHunt.InteractionSystem.Core
{
    [Serializable]
    public struct AttachmentInstance
    {
        public string attachmentDataId;
        public AttachmentSlotType slotType;
        public float durability;
    }
}