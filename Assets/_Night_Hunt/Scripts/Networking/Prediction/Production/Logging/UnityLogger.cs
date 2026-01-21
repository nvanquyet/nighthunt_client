using UnityEngine;

namespace NightHunt.Networking.Prediction.Production.Logging
{
    public sealed class UnityLogger : ILogger
    {
        public void Info(string tag, string message)
        {
            Debug.Log(Format(tag, message));
        }

        public void Warn(string tag, string message)
        {
            Debug.LogWarning(Format(tag, message));
        }

        public void Error(string tag, string message)
        {
            Debug.LogError(Format(tag, message));
        }

        private static string Format(string tag, string message)
        {
            return $"[{tag}] {message}";
        }
    }
}


