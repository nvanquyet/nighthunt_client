using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using System.Collections.Generic;
using FishNet;
using NightHunt.Networking;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Zone;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Central authority for all in-match objectives and phase-scoped zone effects.
    ///
    /// Managed categories:
    ///   Phase 2 (Hunt)    — CaptureZoneObjectives, ZoneBuffs (passive speed/effect zones)
    ///   Phase 3 (Lockdown) — RadarStationObjectives, EMPNodeObjectives
    ///
    /// ZoneBuff is NOT an IObjective (no scoring/progress) but is phase-managed here
    /// so everything activates and deactivates from a single place — no stray logic.
    /// </summary>
    public class ObjectiveSystem : NetworkBehaviour
    {
        [Header("Phase 2 — Hunt Objectives")]
        [SerializeField] private List<CaptureZoneObjective> captureZoneObjectives = new List<CaptureZoneObjective>();

        [Header("Phase 2 — Passive Zone Effects")]
        [Tooltip("ZoneBuff triggers that grant stat modifiers (e.g. speed +20%). " +
                 "Activated at Phase 2 start. Assign GameObjects that start INACTIVE in the scene.")]
        [SerializeField] private List<ZoneBuff> zoneBuffObjects = new List<ZoneBuff>();

        [Header("Phase 3 — Lockdown Objectives")]
        [SerializeField] private List<RadarStationObjective> radarObjectives = new List<RadarStationObjective>();
        [SerializeField] private List<EMPNodeObjective> empObjectives = new List<EMPNodeObjective>();

        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        private List<IObjective> activeObjectives = new List<IObjective>();

        // Synchronized state
        private readonly SyncVar<int> networkActiveObjectiveCount = new SyncVar<int>();

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_phaseManager == null)
            {
                _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
                if (_phaseManager == null)
                    Debug.LogWarning("[ObjectiveSystem] MatchPhaseManager not found — assign it in the Inspector.");
            }

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
            // Enum comparison is type-safe — no string parsing needed.
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
        /// Server: Activate Phase 2 (Hunt) objectives and passive zone effects.
        /// </summary>
        [Server]
        private void ActivatePhase2Objectives()
        {
            // Activate scored capture zones
            foreach (var objective in captureZoneObjectives)
            {
                if (objective != null)
                    objective.OnStart();
            }

            // Enable passive speed/effect buff zones — start inactive in scene,
            // activated here so they are phase-scoped instead of always-on.
            foreach (var buff in zoneBuffObjects)
            {
                if (buff != null)
                    buff.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Server: Activate Phase 3 (Lockdown) objectives.
        /// CaptureZones are NOT re-activated here — they are phase-gated to Hunt and
        /// calling OnStart() again would wastefully re-run FindFirstObjectByType lookups.
        /// ZoneBuff areas remain active from Phase 2.
        /// </summary>
        [Server]
        private void ActivatePhase3Objectives()
        {
            // Activate radar stations
            foreach (var objective in radarObjectives)
            {
                if (objective != null)
                    objective.OnStart();
            }

            // Activate EMP nodes
            foreach (var objective in empObjectives)
            {
                if (objective != null)
                    objective.OnStart();
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
