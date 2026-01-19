using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;
using System.Collections.Generic;
using FishNet;

namespace NightHunt.Gameplay.PredatorPrey
{
    /// <summary>
    /// Manages Predator/Prey dynamic role system
    /// Teams with higher scores become "Predators" (revealed, slower stamina regen)
    /// Teams with lower scores become "Prey" (radar ping, faster stamina regen)
    /// </summary>
    public class PredatorPreySystem : NetworkBehaviour
    {
        [Header("System Settings")]
        [SerializeField] private float updateInterval = 5f;

        // Synchronized state
        private readonly SyncVar<int> leadingTeamId = new SyncVar<int>(-1);
        // Note: Dictionary cannot be directly synced, will use manual sync
        private Dictionary<int, bool> teamRoles = new Dictionary<int, bool>(); // TeamId -> IsPredator

        private ScoringSystem scoringSystem;
        private ScoreTracker scoreTracker;
        private RevealSystem revealSystem;
        private RadarPingSystem radarPingSystem;
        private float lastUpdateTime;

        public override void OnStartServer()
        {
            base.OnStartServer();
            scoringSystem = FindObjectOfType<ScoringSystem>();
            if (scoringSystem != null)
            {
                scoreTracker = new ScoreTracker(scoringSystem);
            }

            // Initialize reveal and radar systems
            revealSystem = GetComponent<RevealSystem>();
            if (revealSystem == null)
            {
                revealSystem = gameObject.AddComponent<RevealSystem>();
            }

            radarPingSystem = GetComponent<RadarPingSystem>();
            if (radarPingSystem == null)
            {
                radarPingSystem = gameObject.AddComponent<RadarPingSystem>();
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            // Update roles periodically
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateRoles();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Server: Update predator/prey roles based on scores
        /// </summary>
        [Server]
        public void UpdateRoles()
        {
            if (scoringSystem == null) return;

            // Get leading team using ScoreTracker
            int newLeadingTeam = -1;
            if (scoreTracker != null)
            {
                newLeadingTeam = scoreTracker.GetLeadingTeam();
            }
            else if (scoringSystem != null)
            {
                newLeadingTeam = scoringSystem.GetLeadingTeam();
            }
            
            if (newLeadingTeam != leadingTeamId.Value)
            {
                leadingTeamId.Value = newLeadingTeam;
                ApplyRoleChanges();
            }
        }

        /// <summary>
        /// Server: Apply role changes to all players
        /// </summary>
        [Server]
        private void ApplyRoleChanges()
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            
            foreach (var player in players)
            {
                int teamId = player.TeamId;
                bool isPredator = (teamId == leadingTeamId.Value);

                ApplyRoleToPlayer(player, isPredator);
            }
        }

        /// <summary>
        /// Server: Apply role to specific player
        /// </summary>
        [Server]
        private void ApplyRoleToPlayer(NetworkPlayer player, bool isPredator)
        {
            var characterStats = player.GetComponent<CharacterStats>();
            var characterMovement = player.GetComponent<CharacterMovement>();
            var visionSystem = player.GetComponent<Vision.VisionSystem>();

            if (characterStats == null) return;

            // Remove old role effects
            RemoveRoleEffects(characterStats, characterMovement, visionSystem);

            // Apply new role effects using RoleBuffSystem
            if (isPredator)
            {
                RoleBuffSystem.ApplyPredatorBuffs(characterStats);
                ApplyPredatorEffects(characterStats, characterMovement, visionSystem);
            }
            else
            {
                RoleBuffSystem.ApplyPreyBuffs(characterStats);
                ApplyPreyEffects(characterStats, characterMovement, visionSystem);
            }
        }

        /// <summary>
        /// Apply Predator effects (team leading)
        /// - Reveal direction to enemies
        /// - Slower stamina regen
        /// - Louder footsteps
        /// </summary>
        private void ApplyPredatorEffects(CharacterStats stats, CharacterMovement movement, Vision.VisionSystem vision)
        {
            // Slower stamina regen
            if (movement != null)
            {
                movement.SetStaminaDrainMultiplier(1.2f); // 20% slower regen
            }

            // Louder footsteps (increase noise)
            if (stats != null)
            {
                // Apply noise multiplier
                // Would need to integrate with noise system
            }

            // Reveal direction (handled by vision system)
            if (vision != null)
            {
                // Enable direction reveal
            }
        }

        /// <summary>
        /// Apply Prey effects (team losing)
        /// - Radar ping
        /// - Faster stamina regen
        /// - Quieter movement
        /// </summary>
        private void ApplyPreyEffects(CharacterStats stats, CharacterMovement movement, Vision.VisionSystem vision)
        {
            // Faster stamina regen
            if (movement != null)
            {
                movement.SetStaminaDrainMultiplier(0.8f); // 20% faster regen
            }

            // Quieter movement (reduce noise)
            if (stats != null)
            {
                // Apply noise reduction
                stats.ApplyStatusEffect("STATUS_SILENT", 999f); // Long duration
            }

            // Radar ping (handled by vision system)
            if (vision != null)
            {
                // Enable radar ping
            }
        }

        /// <summary>
        /// Remove role effects
        /// </summary>
        private void RemoveRoleEffects(CharacterStats stats, CharacterMovement movement, Vision.VisionSystem vision)
        {
            if (stats != null)
            {
                RoleBuffSystem.RemoveRoleBuffs(stats);
            }

            if (movement != null)
            {
                movement.SetStaminaDrainMultiplier(1f); // Reset
            }

            if (stats != null)
            {
                stats.RemoveStatusEffect("STATUS_SILENT");
            }
        }

        /// <summary>
        /// Check if team is predator
        /// </summary>
        public bool IsTeamPredator(int teamId)
        {
            return teamId == leadingTeamId.Value;
        }

        /// <summary>
        /// Get leading team ID
        /// </summary>
        public int GetLeadingTeamId() => leadingTeamId.Value;
    }
}

