using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Capture zone objective
    /// </summary>
    public class CaptureZone : NetworkBehaviour
    {
        [Header("Capture Settings")] [SerializeField]
        private float captureRadius = 10f;

        [SerializeField] private float captureTime = 5f;
        [SerializeField] private int scorePerSecond = 20;

        [Header("Visual")] [SerializeField] private GameObject zoneIndicator;
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
                var networkPlayer = ComponentResolver.Find<NetworkPlayer>(collider)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] NetworkPlayer not found")
                    .Resolve();
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
                lastScoreTime = Time.time;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }
}