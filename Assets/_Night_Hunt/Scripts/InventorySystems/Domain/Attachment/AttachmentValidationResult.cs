namespace NightHunt.Inventory.Domain.Attachment
{
    /// <summary>
    /// Result of attachment validation.
    /// </summary>
    public struct AttachmentValidationResult
    {
        public bool IsValid;
        public string FailReason;
        
        public static AttachmentValidationResult Success()
        {
            return new AttachmentValidationResult { IsValid = true };
        }
        
        public static AttachmentValidationResult Fail(string reason)
        {
            return new AttachmentValidationResult
            {
                IsValid = false,
                FailReason = reason
            };
        }
    }
}