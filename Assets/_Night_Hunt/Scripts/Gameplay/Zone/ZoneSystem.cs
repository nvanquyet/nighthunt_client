using UnityEngine;
using NightHunt.Data;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Manages game zones (Safe, Toxic, Heal, Speed, Scanner, etc.)
    /// Applies zone effects to players within radius
    /// </summary>
    public class ZoneSystem : MonoBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField] private List<ZoneArea> activeZones = new List<ZoneArea>();

        private static ZoneSystem _instance;
        public static ZoneSystem Instance => _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Register a zone area
        /// </summary>
        public void RegisterZone(ZoneArea zone)
        {
            if (!activeZones.Contains(zone))
            {
                activeZones.Add(zone);
            }
        }

        /// <summary>
        /// Unregister a zone area
        /// </summary>
        public void UnregisterZone(ZoneArea zone)
        {
            activeZones.Remove(zone);
        }

        /// <summary>
        /// Get all zones affecting a position
        /// </summary>
        public List<ZoneArea> GetZonesAtPosition(Vector3 position)
        {
            List<ZoneArea> affectingZones = new List<ZoneArea>();

            foreach (var zone in activeZones)
            {
                if (zone != null && zone.IsPositionInside(position))
                {
                    affectingZones.Add(zone);
                }
            }

            return affectingZones;
        }

        /// <summary>
        /// Get primary zone effect at position (highest priority)
        /// </summary>
        public ZoneArea GetPrimaryZoneAtPosition(Vector3 position)
        {
            var zones = GetZonesAtPosition(position);
            if (zones.Count == 0) return null;

            // Return first zone (can add priority system later)
            return zones[0];
        }
    }

    /// <summary>
    /// Individual zone area that applies effects
    /// </summary>
    public class ZoneArea : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [SerializeField] private string zoneId;
        [SerializeField] private float radius = 20f;
        [SerializeField] private float duration = 60f;
        [SerializeField] private bool isActive = true;

        [Header("Visual")]
        [SerializeField] private GameObject zoneIndicator;
        [SerializeField] private ParticleSystem zoneEffect;

        private ZoneConfigData config;
        private float spawnTime;
        private List<Gameplay.Character.CharacterStats> playersInZone = new List<Gameplay.Character.CharacterStats>();

        private void Start()
        {
            spawnTime = Time.time;
            
            // Load config
            config = GameConfigLoader.Instance?.GetZoneConfig(zoneId);
            if (config != null)
            {
                radius = config.Radius;
                duration = config.Duration;
            }

            // Register with zone system
            if (ZoneSystem.Instance != null)
            {
                ZoneSystem.Instance.RegisterZone(this);
            }

            // Setup visual
            if (zoneIndicator != null)
            {
                zoneIndicator.transform.localScale = Vector3.one * radius * 2f;
            }
        }

        private void Update()
        {
            // Check duration
            if (duration > 0f && Time.time - spawnTime >= duration)
            {
                DestroyZone();
                return;
            }

            // Update players in zone
            UpdatePlayersInZone();
        }

        private void UpdatePlayersInZone()
        {
            // Find all players in radius
            Collider[] colliders = Physics.OverlapSphere(transform.position, radius);
            
            List<Gameplay.Character.CharacterStats> currentPlayers = new List<Gameplay.Character.CharacterStats>();

            foreach (var collider in colliders)
            {
                var characterStats = collider.GetComponent<Gameplay.Character.CharacterStats>();
                if (characterStats != null && !currentPlayers.Contains(characterStats))
                {
                    currentPlayers.Add(characterStats);
                    
                    // Apply zone effects
                    if (!playersInZone.Contains(characterStats))
                    {
                        OnPlayerEnter(characterStats);
                    }
                }
            }

            // Remove players who left
            foreach (var player in playersInZone)
            {
                if (!currentPlayers.Contains(player))
                {
                    OnPlayerExit(player);
                }
            }

            playersInZone = currentPlayers;
        }

        private void OnPlayerEnter(Gameplay.Character.CharacterStats character)
        {
            if (config == null) return;

            // Apply zone effects
            ApplyZoneEffects(character, true);
        }

        private void OnPlayerExit(Gameplay.Character.CharacterStats character)
        {
            if (config == null) return;

            // Remove zone effects
            ApplyZoneEffects(character, false);
        }

        private void ApplyZoneEffects(Gameplay.Character.CharacterStats character, bool entering)
        {
            if (config == null) return;

            // Apply damage over time (Toxic zone)
            if (config.ZoneType == "Toxic" && entering)
            {
                // Damage will be applied in Update
            }

            // Apply vision modifier
            if (config.VisionMultiplier != 1f)
            {
                // Would need to integrate with VisionSystem
            }

            // Apply speed modifier
            if (config.SpeedMultiplier != 1f)
            {
                // Would need to integrate with CharacterMovement
            }

            // Apply stamina regen
            if (config.StaminaRegen != 1f)
            {
                // Would need to integrate with CharacterMovement
            }

            // Ping reveal (Scanner zone)
            if (config.PingReveal && entering)
            {
                // Reveal players in zone
            }
        }

        /// <summary>
        /// Check if position is inside zone
        /// </summary>
        public bool IsPositionInside(Vector3 position)
        {
            if (!isActive) return false;
            float distance = Vector3.Distance(transform.position, position);
            return distance <= radius;
        }

        /// <summary>
        /// Destroy zone
        /// </summary>
        public void DestroyZone()
        {
            // Remove all players
            foreach (var player in playersInZone)
            {
                OnPlayerExit(player);
            }

            // Unregister
            if (ZoneSystem.Instance != null)
            {
                ZoneSystem.Instance.UnregisterZone(this);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Get zone config
        /// </summary>
        public ZoneConfigData GetConfig() => config;

        /// <summary>
        /// Get zone radius
        /// </summary>
        public float GetRadius() => radius;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

