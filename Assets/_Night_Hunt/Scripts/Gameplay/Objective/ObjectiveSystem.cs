using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using System.Collections.Generic;
using FishNet;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Manages game objectives: Capture zones, Boss spawns, etc.
    /// Refactored to use IObjective interface
    /// </summary>
    public class ObjectiveSystem : NetworkBehaviour
    {
        [Header("Objectives")]
        [SerializeField] private List<IObjective> activeObjectives = new List<IObjective>();
        [SerializeField] private List<BossObjective> bossObjectives = new List<BossObjective>();
        [SerializeField] private List<CaptureZoneObjective> captureZoneObjectives = new List<CaptureZoneObjective>();
        [SerializeField] private List<RadarStationObjective> radarObjectives = new List<RadarStationObjective>();
        [SerializeField] private List<EMPNodeObjective> empObjectives = new List<EMPNodeObjective>();

        // Synchronized state
        private readonly SyncVar<int> networkActiveObjectiveCount = new SyncVar<int>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeObjectives();
        }

        private void Update()
        {
            if (!IsServer) return;

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
            activeObjectives.AddRange(bossObjectives);
            activeObjectives.AddRange(captureZoneObjectives);
            activeObjectives.AddRange(radarObjectives);
            activeObjectives.AddRange(empObjectives);
        }

        /// <summary>
        /// Server: Activate objectives for phase
        /// </summary>
        [Server]
        public void ActivateObjectivesForPhase(string phaseName)
        {
            switch (phaseName)
            {
                case "Phase2_Hunt":
                case "Phase2_HuntObjectives":
                    ActivatePhase2Objectives();
                    break;
                case "Phase3_Lockdown":
                case "Phase3_FinalLockdown":
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

            // Activate boss objectives
            foreach (var objective in bossObjectives)
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

    /// <summary>
    /// Capture zone objective
    /// </summary>
    public class CaptureZone : NetworkBehaviour
    {
        [Header("Capture Settings")]
        [SerializeField] private float captureRadius = 10f;
        [SerializeField] private float captureTime = 5f;
        [SerializeField] private int scorePerSecond = 20;

        [Header("Visual")]
        [SerializeField] private GameObject zoneIndicator;
        [SerializeField] private Material neutralMaterial;
        [SerializeField] private Material capturingMaterial;
        [SerializeField] private Material capturedMaterial;

        // Synchronized state
        private readonly SyncVar<int> capturingTeamId = new SyncVar<int>(-1);
        private readonly SyncVar<float> captureProgress = new SyncVar<float>();
        private readonly SyncVar<bool> isActive = new SyncVar<bool>();

        private Dictionary<int, int> playersInZone = new Dictionary<int, int>(); // TeamId -> PlayerCount
        private float lastScoreTime;

        public bool IsActive => isActive.Value;
        public int CapturingTeamId => capturingTeamId.Value;
        public float CaptureProgress => captureProgress.Value;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            if (zoneIndicator != null)
            {
                zoneIndicator.transform.localScale = Vector3.one * captureRadius * 2f;
            }
        }

        private void Update()
        {
            if (!IsServer || !isActive.Value) return;

            UpdateCapture();
            UpdateScoring();
        }

        /// <summary>
        /// Server: Initialize capture zone
        /// </summary>
        [Server]
        public void Initialize()
        {
            isActive.Value = false;
            capturingTeamId.Value = -1;
            captureProgress.Value = 0f;
        }

        /// <summary>
        /// Server: Activate capture zone
        /// </summary>
        [Server]
        public void Activate()
        {
            isActive.Value = true;
        }

        /// <summary>
        /// Server: Update capture progress
        /// </summary>
        [Server]
        private void UpdateCapture()
        {
            // Count players per team in zone
            playersInZone.Clear();
            Collider[] colliders = Physics.OverlapSphere(transform.position, captureRadius);
            
            foreach (var collider in colliders)
            {
                var networkPlayer = collider.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    int teamId = networkPlayer.TeamId;
                    if (!playersInZone.ContainsKey(teamId))
                    {
                        playersInZone[teamId] = 0;
                    }
                    playersInZone[teamId]++;
                }
            }

            // Determine capturing team (team with most players)
            int dominantTeam = -1;
            int maxPlayers = 0;

            foreach (var kvp in playersInZone)
            {
                if (kvp.Value > maxPlayers)
                {
                    maxPlayers = kvp.Value;
                    dominantTeam = kvp.Key;
                }
            }

            // Update capture progress
            if (dominantTeam >= 0 && maxPlayers > 0)
            {
                if (capturingTeamId.Value != dominantTeam)
                {
                    capturingTeamId.Value = dominantTeam;
                }

                // Increase capture progress
                float captureRate = maxPlayers * Time.deltaTime / captureTime;
                captureProgress.Value += captureRate;
                captureProgress.Value = Mathf.Clamp01(captureProgress.Value);
            }
            else
            {
                // No one capturing - decay progress
                captureProgress.Value -= Time.deltaTime / captureTime;
                captureProgress.Value = Mathf.Max(0f, captureProgress.Value);

                if (captureProgress.Value <= 0f)
                {
                    capturingTeamId.Value = -1;
                }
            }
        }

        /// <summary>
        /// Server: Update scoring for capturing team
        /// </summary>
        [Server]
        private void UpdateScoring()
        {
            if (capturingTeamId.Value < 0 || captureProgress.Value <= 0f) return;

            if (Time.time - lastScoreTime >= 1f)
            {
                // Award score per second
                // This would integrate with a scoring system
                lastScoreTime = Time.time;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }

    /// <summary>
    /// Boss spawn point
    /// </summary>
    public class BossSpawnPoint : MonoBehaviour
    {
        [Header("Boss Settings")]
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private bool hasSpawned = false;

        /// <summary>
        /// Spawn boss at this point
        /// </summary>
        public void SpawnBoss()
        {
            if (hasSpawned || bossPrefab == null) return;

            GameObject boss = Instantiate(bossPrefab, transform.position, transform.rotation);
            hasSpawned = true;

            // Spawn on network if needed
            var networkObject = boss.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                // Would need NetworkManager reference to spawn
            }
        }
    }
}


