namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Result of an inventory operation.
    /// </summary>
    public struct OperationResult
    {
        public bool IsSuccess;
        public string FailReason;
        
        public static OperationResult Success() => new OperationResult { IsSuccess = true };
        public static OperationResult Fail(string reason) => new OperationResult { IsSuccess = false, FailReason = reason };
    }
}
