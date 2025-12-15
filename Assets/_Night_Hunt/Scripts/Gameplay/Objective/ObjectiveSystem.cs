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
    /// </summary>
    public class ObjectiveSystem : NetworkBehaviour
    {
        [Header("Objectives")]
        [SerializeField] private List<CaptureZone> captureZones = new List<CaptureZone>();
        [SerializeField] private List<BossSpawnPoint> bossSpawnPoints = new List<BossSpawnPoint>();

        // Synchronized state
        private readonly SyncVar<int> activeObjectiveCount = new SyncVar<int>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeObjectives();
        }

        /// <summary>
        /// Server: Initialize objectives based on phase
        /// </summary>
        [Server]
        private void InitializeObjectives()
        {
            // Capture zones activate in Phase 2
            foreach (var zone in captureZones)
            {
                if (zone != null)
                {
                    zone.Initialize();
                }
            }
        }
        /// <summary>
        /// Server: Activate objectives for phase
        /// </summary>
        [Server]
        public void ActivateObjectivesForPhase(string phaseName)
        {
            switch (phaseName)
            {
                case "Phase2_HuntObjectives":
                    ActivateCaptureZones();
                    SpawnBoss();
                    break;
                case "Phase3_FinalLockdown":
                    // All objectives active
                    break;
            }
        }

        /// <summary>
        /// Server: Activate capture zones
        /// </summary>
        [Server]
        private void ActivateCaptureZones()
        {
            foreach (var zone in captureZones)
            {
                if (zone != null)
                {
                    zone.Activate();
                }
            }
        }

        /// <summary>
        /// Server: Spawn boss
        /// </summary>
        [Server]
        private void SpawnBoss()
        {
            if (bossSpawnPoints.Count == 0) return;

            BossSpawnPoint spawnPoint = bossSpawnPoints[Random.Range(0, bossSpawnPoints.Count)];
            spawnPoint.SpawnBoss();
        }

        /// <summary>
        /// Get active objective count
        /// </summary>
        public int GetActiveObjectiveCount() => activeObjectiveCount.Value;
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


