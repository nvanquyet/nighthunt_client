using UnityEngine;

namespace NightHunt.Networking.Prediction.Utils
{
    /// <summary>
    /// Scriptable config for prediction settings.
    /// </summary>
    [CreateAssetMenu(fileName = "PredictionConfig", menuName = "NightHunt/Gameplay/Prediction Config")]
    public class PredictionConfig : ScriptableObject
    {
        [Header("Prediction")]
        public bool enablePrediction = true;
        [Range(30, 120)] public int tickRate = 60;
        [Range(16, 128)] public int stateHistorySize = 32;
        [Range(5, 20)] public int maxPredictionSteps = 10;

        [Header("Reconciliation")]
        public bool enableReconciliation = true;
        public ReconciliationStrategy defaultStrategy = ReconciliationStrategy.Hybrid;
        [Range(0.01f, 1f)] public float reconciliationThreshold = 0.1f;
        [Range(0.1f, 5f)] public float snapThreshold = 1f;
        [Range(0.01f, 0.5f)] public float smoothTime = 0.1f;
        public AnimationCurve smoothCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Interpolation")]
        [Range(0f, 0.5f)] public float interpolationDelay = 0.1f;
        public bool useExtrapolation = true;
        [Range(0.1f, 1f)] public float extrapolationLimit = 0.5f;

        [Header("Performance")]
        public bool useLOD = true;
        public float lodDistance1 = 50f;
        public float lodDistance2 = 100f;
        public float lodDistance3 = 200f;
        public bool enableObjectPooling = false; // ← THÊM DÒNG NÀY

        [Header("Debug")]
        public bool showDebugGizmos = false;
        public bool logReconciliation = false;
        public bool showNetworkStats = false;

        private void OnValidate()
        {
            tickRate = Mathf.Clamp(tickRate, 30, 120);
            stateHistorySize = Mathf.Clamp(stateHistorySize, 16, 128);
            maxPredictionSteps = Mathf.Clamp(maxPredictionSteps, 5, 20);
            reconciliationThreshold = Mathf.Clamp(reconciliationThreshold, 0.01f, 1f);
            snapThreshold = Mathf.Clamp(snapThreshold, 0.1f, 5f);
            smoothTime = Mathf.Clamp(smoothTime, 0.01f, 0.5f);
            interpolationDelay = Mathf.Clamp(interpolationDelay, 0f, 0.5f);
            extrapolationLimit = Mathf.Clamp(extrapolationLimit, 0.1f, 1f);

            if (lodDistance1 >= lodDistance2) lodDistance2 = lodDistance1 + 10f;
            if (lodDistance2 >= lodDistance3) lodDistance3 = lodDistance2 + 10f;
        }
    }

    public enum ReconciliationStrategy
    {
        Snap,
        Smooth,
        Hybrid
    }
}