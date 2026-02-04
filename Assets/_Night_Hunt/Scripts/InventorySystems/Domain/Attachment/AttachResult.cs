namespace NightHunt.Inventory.Domain.Attachment
{
    /// <summary>
    /// Result of an attach operation.
    /// </summary>
    public struct AttachResult
    {
        public bool IsSuccess;
        public string FailReason;

        public static AttachResult Success()
        {
            return new AttachResult { IsSuccess = true };
        }

        public static AttachResult Fail(string reason)
        {
            return new AttachResult
            {
                IsSuccess = false,
                FailReason = reason
            };
        }
    }
}