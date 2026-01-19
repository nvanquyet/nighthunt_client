using UnityEngine;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Radar station objective
    /// </summary>
    public class RadarStationObjective : MonoBehaviour, IObjective
    {
        [Header("Radar Station Settings")]
        [SerializeField] private string objectiveId = "RADAR_STATION";
        [SerializeField] private string objectiveName = "Activate Radar Station";
        [SerializeField] private float activationTime = 5f;
        [SerializeField] private bool isActivated = false;

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted => isActivated;
        public float Progress { get; private set; }

        private float activationProgress = 0f;
        private bool isInteracting = false;

        public void OnStart()
        {
            isActivated = false;
            Progress = 0f;
            activationProgress = 0f;
        }

        public void OnUpdate()
        {
            if (isInteracting && !isActivated)
            {
                activationProgress += Time.deltaTime / activationTime;
                activationProgress = Mathf.Clamp01(activationProgress);
                Progress = activationProgress;

                if (activationProgress >= 1f)
                {
                    OnComplete();
                }
            }
            else if (!isInteracting && !isActivated)
            {
                // Decay progress if not interacting
                activationProgress = Mathf.Max(0f, activationProgress - Time.deltaTime / activationTime);
                Progress = activationProgress;
            }
        }

        public void OnComplete()
        {
            isActivated = true;
            Progress = 1f;
            Debug.Log($"[RadarStationObjective] Radar station activated: {objectiveName}");
        }

        public void OnFail()
        {
            // Radar station doesn't fail
        }

        /// <summary>
        /// Start interaction
        /// </summary>
        public void StartInteraction()
        {
            isInteracting = true;
        }

        /// <summary>
        /// Stop interaction
        /// </summary>
        public void StopInteraction()
        {
            isInteracting = false;
        }
    }
}

