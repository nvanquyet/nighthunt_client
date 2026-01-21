namespace NightHunt.Networking.Prediction.Production.Logging
{
    public static class PredictionLogFormatter
    {
        public static string Format(
            string tag,
            int netId,
            uint tick,
            string phase,
            bool asServer,
            bool replaying,
            string message)
        {
            return $"[{tag}][{netId}][tick={tick}][phase={phase}][asServer={asServer}][replaying={replaying}] {message}";
        }
    }
}


