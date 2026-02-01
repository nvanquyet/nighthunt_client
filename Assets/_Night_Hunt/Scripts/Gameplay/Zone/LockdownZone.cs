using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Phase 3 lockdown zone
    /// Closes over time, damages players outside
    /// </summary>
    public class LockdownZone : NetworkBehaviour
    {
        [Header("Lockdown Settings")]
        [SerializeField] private float initialRadius = 100f;
        [SerializeField] private float finalRadius = 20f;
        [SerializeField] private float closeTime = 120f; // 2 minutes
        [SerializeField] private float damagePerSecond = 10f;
        [SerializeField] private float teleportDelay = 3f;

        // Synchronized state
        private readonly SyncVar<float> networkCurrentRadius = new SyncVar<float>();
        private readonly SyncVar<float> networkCloseProgress = new SyncVar<float>();

        private MatchPhaseManager phaseManager;
        private float currentRadius;
        private float closeProgress = 0f;
        private List<NetworkPlayer> playersOutsideZone = new List<NetworkPlayer>();

        public float CurrentRadius => networkCurrentRadius.Value;
        public float CloseProgress => networkCloseProgress.Value;

        private void Awake()
        {
            phaseManager = FindFirstObjectByType<MatchPhaseManager>();
            currentRadius = initialRadius;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkCurrentRadius.OnChange += OnRadiusChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkCurrentRadius != null)
                networkCurrentRadius.OnChange -= OnRadiusChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            // Only active in Phase 3
            if (phaseManager == null || phaseManager.CurrentPhase != MatchPhaseState.Lockdown)
            {
                return;
            }

            UpdateZoneClosing();
            CheckPlayersOutsideZone();
        }

        /// <summary>
        /// Server: Update zone closing
        /// </summary>
        [Server]
        private void UpdateZoneClosing()
        {
            if (phaseManager == null) return;

            float phaseElapsed = phaseManager.PhaseElapsedTime;
            float phaseDuration = phaseManager.PhaseRemainingTime + phaseElapsed;

            // Calculate close progress (0-1)
            closeProgress = Mathf.Clamp01(phaseElapsed / closeTime);

            // Calculate current radius
            currentRadius = Mathf.Lerp(initialRadius, finalRadius, closeProgress);

            // Sync to network
            networkCurrentRadius.Value = currentRadius;
            networkCloseProgress.Value = closeProgress;
        }

        /// <summary>
        /// Server: Check players outside zone
        /// </summary>
        [Server]
        private void CheckPlayersOutsideZone()
        {
            playersOutsideZone.Clear();
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                float distance = Vector3.Distance(Vector3.zero, player.transform.position); // Assuming zone center is at origin
                if (distance > currentRadius)
                {
                    playersOutsideZone.Add(player);
                    DamagePlayer(player);
                }
            }
        }

        /// <summary>
        /// Server: Damage player outside zone
        /// </summary>
        [Server]
        private void DamagePlayer(NetworkPlayer player)
        {
            // Use ComponentFinder to search in hierarchy (including children)
            var stats = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(player.gameObject, includeInactive: false);
            if (stats != null)
            {
                stats.TakeDamage(damagePerSecond * Time.deltaTime);
            }

            // Teleport to zone edge after delay
            StartCoroutine(TeleportToZone(player));
        }

        /// <summary>
        /// Teleport player to zone edge
        /// </summary>
        private System.Collections.IEnumerator TeleportToZone(NetworkPlayer player)
        {
            yield return new WaitForSeconds(teleportDelay);

            if (player != null)
            {
                Vector3 direction = player.transform.position.normalized;
                Vector3 teleportPosition = direction * currentRadius;
                player.transform.position = teleportPosition;
            }
        }

        private void OnRadiusChanged(float oldRadius, float newRadius, bool asServer)
        {
            // Update visual representation
            transform.localScale = Vector3.one * newRadius * 2f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Vector3.zero, currentRadius);
        }
    }
}

