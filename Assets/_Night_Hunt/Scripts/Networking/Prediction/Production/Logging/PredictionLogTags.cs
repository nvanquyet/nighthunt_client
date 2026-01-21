namespace NightHunt.Networking.Prediction.Production.Logging
{
    public static class PredictionLogTags
    {
        public const string PredMove = "PRED_MOVE";

        public static class Phase
        {
            public const string Input = "Input";
            public const string Replicate = "Replicate";
            public const string Simulate = "Simulate";
            public const string Reconcile = "Reconcile";
            public const string SmoothApply = "SmoothApply";
        }
    }

    public static class MovementErrorCodes
    {
        public const string MissingCharacterController = "MV001";
        public const string InvalidInputOrState = "MV002";
        public const string ReconcileDeltaExceedsSnapThreshold = "MV003";
        public const string SmoothingFailed = "MV004";
    }
}


