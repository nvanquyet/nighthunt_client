using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Match;
using System.Collections;
using FishNet;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Respawn Beacon that players can place
    /// Allows team members to respawn at this location
    /// Can be destroyed by enemies
    /// </summary>
    public class RespawnBeacon : NetworkBehaviour
    {
        [Header("Beacon Settings")] [SerializeField]
        private int maxHP = 500;

        [SerializeField] private float placeTime = 5f;
        [SerializeField] private float respawnDelay = 15f;
        [SerializeField] private float minDistanceFromOtherBeacons = 30f;

        [Header("Visual")] [SerializeField] private GameObject beaconModel;
        [SerializeField] private GameObject placementIndicator;
        [SerializeField] private ParticleSystem placementEffect;

        // Synchronized state
        private readonly SyncVar<int> currentHP = new SyncVar<int>();
        private readonly SyncVar<int> ownerTeamId = new SyncVar<int>();
        private readonly SyncVar<bool> isPlaced = new SyncVar<bool>();
        private readonly SyncVar<bool> isActive = new SyncVar<bool>();

        private RespawnConfigData config;
        private MatchPhaseManager phaseManager;

        public int CurrentHP => currentHP.Value;
        public int OwnerTeamId => ownerTeamId.Value;
        public bool IsPlaced => isPlaced.Value;
        public bool IsActive => isActive.Value;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Load config
            var configData = GameConfigLoader.Instance?.ConfigData;
            if (configData != null && configData.RespawnConfig != null && configData.RespawnConfig.Count > 0)
            {
                config = configData.RespawnConfig[0];
            }

            if (config != null)
            {
                maxHP = config.BeaconHP;
                placeTime = config.PlaceTime;
                respawnDelay = config.RespawnDelay;
                minDistanceFromOtherBeacons = config.MinDistance;
            }

            currentHP.Value = maxHP;

            // Find phase manager
            phaseManager = FindObjectOfType<MatchPhaseManager>();

            // Subscribe to HP changes
            currentHP.OnChange += OnHPChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            currentHP.OnChange -= OnHPChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Check if beacons are enabled in current phase
            if (phaseManager != null && !phaseManager.AreBeaconsEnabled())
            {
                // Beacons disabled in this phase
                DespawnBeacon();
            }
        }

        /// <summary>
        /// Server: Initialize beacon with owner team
        /// </summary>
        [Server]
        public void Initialize(int teamId)
        {
            ownerTeamId.Value = teamId;
            currentHP.Value = maxHP;
            isPlaced.Value = false;
            isActive.Value = true;
        }

        /// <summary>
        /// Server: Start placing beacon
        /// </summary>
        [Server]
        public void StartPlacement()
        {
            if (isPlaced.Value) return;

            // Check distance from other beacons
            if (!IsValidPlacementLocation())
            {
                Debug.LogWarning("[RespawnBeacon] Invalid placement location - too close to another beacon");
                return;
            }

            // Start placement timer
            StartCoroutine(PlacementCoroutine());
        }

        private IEnumerator PlacementCoroutine()
        {
            yield return new WaitForSeconds(placeTime);

            if (!isPlaced.Value)
            {
                isPlaced.Value = true;
                OnBeaconPlaced();
            }
        }

        /// <summary>
        /// Check if placement location is valid
        /// </summary>
        private bool IsValidPlacementLocation()
        {
            RespawnBeacon[] allBeacons = FindObjectsOfType<RespawnBeacon>();

            foreach (var beacon in allBeacons)
            {
                if (beacon == this || !beacon.isPlaced.Value) continue;

                float distance = Vector3.Distance(transform.position, beacon.transform.position);
                if (distance < minDistanceFromOtherBeacons)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Server: Take damage
        /// </summary>
        [Server]
        public void TakeDamage(int damage)
        {
            if (!isActive.Value || !isPlaced.Value) return;

            currentHP.Value -= damage;
            currentHP.Value = Mathf.Max(0, currentHP.Value);

            if (currentHP.Value <= 0)
            {
                OnBeaconDestroyed();
            }
        }

        /// <summary>
        /// Server: Destroy beacon
        /// </summary>
        [Server]
        private void OnBeaconDestroyed()
        {
            isActive.Value = false;
            Debug.Log($"[RespawnBeacon] Beacon destroyed (Team {ownerTeamId.Value})");

            // Award score to destroyer
            // This would be handled by a scoring system

            // Despawn after a delay
            Invoke(nameof(DespawnBeacon), 2f);
        }

        /// <summary>
        /// Server: Beacon placed successfully
        /// </summary>
        [Server]
        private void OnBeaconPlaced()
        {
            Debug.Log($"[RespawnBeacon] Beacon placed (Team {ownerTeamId.Value})");
            // Visual effects, etc.
        }

        /// <summary>
        /// Server: Despawn beacon
        /// </summary>
        [Server]
        private void DespawnBeacon()
        {
            if (IsSpawned)
            {
                Despawn();
            }
        }

        /// <summary>
        /// Server: Check if player can respawn here
        /// </summary>
        [Server]
        public bool CanRespawnHere(int teamId)
        {
            if (!isActive.Value || !isPlaced.Value) return false;
            if (teamId != ownerTeamId.Value) return false;
            if (phaseManager != null && !phaseManager.AreBeaconsEnabled()) return false;
            return true;
        }

        /// <summary>
        /// Get respawn delay
        /// </summary>
        public float GetRespawnDelay() => respawnDelay;

        private void OnHPChanged(int oldHP, int newHP, bool asServer)
        {
            // Update visual representation
            if (beaconModel != null)
            {
                // Update health bar, etc.
            }
        }

        /// <summary>
        /// Client: Show placement indicator
        /// </summary>
        public void ShowPlacementIndicator(bool show)
        {
            if (placementIndicator != null)
            {
                placementIndicator.SetActive(show);
            }
        }
    }
}