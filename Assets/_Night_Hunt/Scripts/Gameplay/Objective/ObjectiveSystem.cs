using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using System.Collections.Generic;
using FishNet;
using NightHunt.Networking;
using NightHunt.Gameplay.Match;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Manages game objectives: Capture zones, Boss spawns, etc.
    /// Refactored to use IObjective interface
    /// </summary>
    public class ObjectiveSystem : NetworkBehaviour
    {
        [Header("Objectives")]
        private List<IObjective> activeObjectives = new List<IObjective>();
        [SerializeField] private List<CaptureZoneObjective> captureZoneObjectives = new List<CaptureZoneObjective>();
        [SerializeField] private List<RadarStationObjective> radarObjectives = new List<RadarStationObjective>();
        [SerializeField] private List<EMPNodeObjective> empObjectives = new List<EMPNodeObjective>();

        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        // Synchronized state
        private readonly SyncVar<int> networkActiveObjectiveCount = new SyncVar<int>();

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_phaseManager == null)
                _phaseManager = FindFirstObjectByType<MatchPhaseManager>();

            if (_phaseManager != null)
                _phaseManager.OnPhaseStarted += OnPhaseStarted;

            InitializeObjectives();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_phaseManager != null)
                _phaseManager.OnPhaseStarted -= OnPhaseStarted;
        }

        private void OnPhaseStarted(MatchPhaseState phase, string displayName)
        {
            // So sánh bằng enum — type-safe
            ActivateObjectivesForPhase(phase);
        }

        private void Update()
        {
            if (!IsServerInitialized) return;

            // Update all active objectives
            foreach (var objective in activeObjectives)
            {
                if (objective != null && !objective.IsCompleted)
                {
                    objective.OnUpdate();
                }
            }

            // Update network sync
            int completedCount = 0;
            foreach (var objective in activeObjectives)
            {
                if (objective != null && objective.IsCompleted)
                {
                    completedCount++;
                }
            }
            networkActiveObjectiveCount.Value = activeObjectives.Count - completedCount;
        }

        /// <summary>
        /// Server: Initialize objectives based on phase
        /// </summary>
        [Server]
        private void InitializeObjectives()
        {
            activeObjectives.Clear();
            
            // Collect all objectives
            activeObjectives.AddRange(captureZoneObjectives);
            activeObjectives.AddRange(radarObjectives);
            activeObjectives.AddRange(empObjectives);
        }

        /// <summary>
        /// Server: Activate objectives for phase
        /// </summary>
        [Server]
        public void ActivateObjectivesForPhase(MatchPhaseState phase)
        {
            switch (phase)
            {
                case MatchPhaseState.Hunt:
                    ActivatePhase2Objectives();
                    break;
                case MatchPhaseState.Lockdown:
                    ActivatePhase3Objectives();
                    break;
            }
        }

        /// <summary>
        /// Server: Activate Phase 2 objectives
        /// </summary>
        [Server]
        private void ActivatePhase2Objectives()
        {
            // Activate capture zones
            foreach (var objective in captureZoneObjectives)
            {
                if (objective != null)
                {
                    objective.OnStart();
                }
            }
        }

        /// <summary>
        /// Server: Activate Phase 3 objectives
        /// </summary>
        [Server]
        private void ActivatePhase3Objectives()
        {
            // All objectives active in Phase 3
            ActivatePhase2Objectives();

            // Activate radar stations
            foreach (var objective in radarObjectives)
            {
                if (objective != null)
                {
                    objective.OnStart();
                }
            }

            // Activate EMP nodes
            foreach (var objective in empObjectives)
            {
                if (objective != null)
                {
                    objective.OnStart();
                }
            }
        }

        /// <summary>
        /// Get active objective count
        /// </summary>
        public int GetActiveObjectiveCount() => networkActiveObjectiveCount.Value;

        /// <summary>
        /// Get all active objectives
        /// </summary>
        public List<IObjective> GetActiveObjectives() => new List<IObjective>(activeObjectives);
    }
}
