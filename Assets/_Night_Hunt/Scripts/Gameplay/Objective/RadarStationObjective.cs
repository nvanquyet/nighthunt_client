using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Radar station objective - Server Authoritative.
    /// A player holds the interact button to activate it over time.
    /// On completion: awards objective score to the activating team.
    /// </summary>
    public class RadarStationObjective : NetworkBehaviour, IObjective
    {
        [Header("Radar Station Settings")]
        [SerializeField] private string objectiveId   = "RADAR_STATION";
        [SerializeField] private string objectiveName = "Activate Radar Station";
        [SerializeField] private float  activationTime = 5f;

        [Header("Score")]
        [Tooltip("Score awarded to the activating team via MatchEndManager.AddObjectiveScore.")]
        [SerializeField] private float completionScore = 300f;

        private readonly SyncVar<bool>  _syncIsActivated = new SyncVar<bool>();
        private readonly SyncVar<float> _syncProgress    = new SyncVar<float>();

        public string ObjectiveId   => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool   IsCompleted   => _syncIsActivated.Value;
        public float  Progress      => _syncProgress.Value;

        private bool isInteracting      = false;
        private int  _interactingTeamId = -1;

        public void OnStart()
        {
            if (!IsServerStarted) return;
            _syncIsActivated.Value = false;
            _syncProgress.Value    = 0f;
            isInteracting          = false;
            _interactingTeamId     = -1;
        }

        public void OnUpdate()
        {
            if (!IsServerStarted || _syncIsActivated.Value) return;

            if (isInteracting)
            {
                _syncProgress.Value = Mathf.Clamp01(_syncProgress.Value + Time.deltaTime / activationTime);

                if (_syncProgress.Value >= 1f)
                    OnComplete();
            }
            else if (_syncProgress.Value > 0f)
            {
                // Decay if player releases early
                _syncProgress.Value = Mathf.Max(0f, _syncProgress.Value - Time.deltaTime / activationTime);
            }
        }

        [Server]
        public void OnComplete()
        {
            if (_syncIsActivated.Value) return;

            _syncIsActivated.Value = true;
            _syncProgress.Value    = 1f;
            isInteracting          = false;

            Debug.Log($"[RadarStationObjective] '{objectiveName}' activated by team {_interactingTeamId}.");

            if (_interactingTeamId >= 0)
            {
                var mem = FindFirstObjectByType<MatchEndManager>();
                if (mem != null)
                    mem.AddObjectiveScore(_interactingTeamId, completionScore);

                var scoring = FindFirstObjectByType<ScoringSystem>();
                if (scoring != null)
                    scoring.AwardObjectiveCapture(_interactingTeamId, activationTime);
            }
        }

        public void OnFail() { /* Radar station doesn't fail */ }

        /// <summary>
        /// Begin activation — caller must supply the interacting player's team.
        /// </summary>
        /// <param name="teamId">Team ID of the player holding the activation button.</param>
        [Server]
        public void StartInteraction(int teamId = -1)
        {
            isInteracting      = true;
            _interactingTeamId = teamId;
        }

        /// <summary>Player released the interaction — activation decays.</summary>
        [Server]
        public void StopInteraction()
        {
            isInteracting = false;
        }
    }
}
