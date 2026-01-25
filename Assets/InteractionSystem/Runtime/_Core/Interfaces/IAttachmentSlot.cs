namespace NightHunt.InteractionSystem.Core
{
    public interface IAttachmentSlot
    {
        AttachmentSlotType SlotType { get; }
        AttachmentData CurrentAttachment { get; }
        bool IsOccupied { get; }
        bool CanAttach(AttachmentData attachment);
        void Attach(AttachmentData attachment);
        void Detach();
    }
}