using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using System.Collections.Generic;
using FishNet;
using NightHunt.Networking;
using NightHunt.Gameplay.Zone;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Central authority for all in-match objectives and zone-scoped effects.
    ///
    /// Activates CaptureZoneObjectives and ZoneBuff triggers when zone phase 0 starts.
    /// Driven by SafeZoneManager.OnZonePhaseStarted(int zoneIndex).
    /// </summary>
    public class ObjectiveSystem : NetworkBehaviour
    {
        [Header("Capture Objectives")]
        [SerializeField] private List<CaptureZoneObjective> captureZoneObjectives = new List<CaptureZoneObjective>();

        [Header("Zone Buffs")]
        [Tooltip("ZoneBuff triggers that grant stat modifiers (e.g. speed +20%). " +
                 "Activated at zone phase 0 start. Assign GameObjects that start INACTIVE in the scene.")]
        [SerializeField] private List<ZoneBuff> zoneBuffObjects = new List<ZoneBuff>();

        private List<IObjective> activeObjectives = new List<IObjective>();

        // Synchronized state
        private readonly SyncVar<int> networkActiveObjectiveCount = new SyncVar<int>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (SafeZoneManager.Instance != null)
                SafeZoneManager.Instance.OnZonePhaseStarted += OnZonePhaseStarted;
            InitializeObjectives();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (SafeZoneManager.Instance != null)
                SafeZoneManager.Instance.OnZonePhaseStarted -= OnZonePhaseStarted;
        }

        private void OnZonePhaseStarted(int zoneIndex)
        {
            if (!IsServerInitialized) return;
            if (zoneIndex == 0)
                ActivateCaptureObjectives();
        }

        private void Update()
        {
            if (!IsServerInitialized) return;

            int completedCount = 0;
            foreach (var objective in activeObjectives)
            {
                if (objective == null) continue;
                if (!objective.IsCompleted) objective.OnUpdate();
                else completedCount++;
            }
            networkActiveObjectiveCount.Value = activeObjectives.Count - completedCount;
        }

        [Server]
        private void InitializeObjectives()
        {
            activeObjectives.Clear();
            activeObjectives.AddRange(captureZoneObjectives);
        }

        /// <summary>
        /// Server: Activate capture objectives and passive zone buffs (called at phase 0).
        /// </summary>
        [Server]
        private void ActivateCaptureObjectives()
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
        /// Get active objective count
        /// </summary>
        public int GetActiveObjectiveCount() => networkActiveObjectiveCount.Value;

        /// <summary>
        /// Get all active objectives
        /// </summary>
        public List<IObjective> GetActiveObjectives() => new List<IObjective>(activeObjectives);
    }
}
