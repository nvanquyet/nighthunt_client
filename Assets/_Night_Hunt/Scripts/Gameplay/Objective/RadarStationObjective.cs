using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Radar station objective - Server Authoritative
    /// </summary>
    public class RadarStationObjective : NetworkBehaviour, IObjective
    {
        [Header("Radar Station Settings")]
        [SerializeField] private string objectiveId = "RADAR_STATION";
        [SerializeField] private string objectiveName = "Activate Radar Station";
        [SerializeField] private float activationTime = 5f;

        private readonly SyncVar<bool> _syncIsActivated = new SyncVar<bool>();
        private readonly SyncVar<float> _syncProgress = new SyncVar<float>();

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted => _syncIsActivated.Value;
        public float Progress => _syncProgress.Value;

        private bool isInteracting = false;

        public void OnStart()
        {
            if (!IsServerStarted) return;
            _syncIsActivated.Value = false;
            _syncProgress.Value = 0f;
            isInteracting = false;
        }

        public void OnUpdate()
        {
            if (!IsServerStarted || _syncIsActivated.Value) return;

            if (isInteracting)
            {
                _syncProgress.Value += Time.deltaTime / activationTime;
                _syncProgress.Value = Mathf.Clamp01(_syncProgress.Value);

                if (_syncProgress.Value >= 1f)
                {
                    OnComplete();
                }
            }
            else if (_syncProgress.Value > 0f)
            {
                // Decay progress if not interacting
                _syncProgress.Value = Mathf.Max(0f, _syncProgress.Value - Time.deltaTime / activationTime);
            }
        }

        [Server]
        public void OnComplete()
        {
            if (_syncIsActivated.Value) return;
            
            _syncIsActivated.Value = true;
            _syncProgress.Value = 1f;
            Debug.Log($"[RadarStationObjective] Radar station activated: {objectiveName}");
        }

        public void OnFail()
        {
            // Radar station doesn't fail
        }

        /// <summary>
        /// Start interaction
        /// </summary>
        [Server]
        public void StartInteraction()
        {
            isInteracting = true;
        }

        /// <summary>
        /// Stop interaction
        /// </summary>
        [Server]
        public void StopInteraction()
        {
            isInteracting = false;
        }
    }
}

