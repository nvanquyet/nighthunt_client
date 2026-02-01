using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Core;
using NightHunt.Networking;
using NightHunt.InteractionSystem.Utilities;
using FishNet;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Handles character death, downed state, revive, and respawn
    /// </summary>
    public class CharacterDeathSystem : NetworkBehaviour
    {
        [Header("Death Settings")]
        [SerializeField] private float bleedoutTime = 20f;
        [SerializeField] private float reviveTime = 4f;
        [SerializeField] private float reviveRange = 3f;
        [SerializeField] private int revivedHP = 40;

        [Header("Visual")]
        [SerializeField] private GameObject downedIndicator;
        [SerializeField] private GameObject deathEffect;

        // Synchronized state
        private readonly SyncVar<DeathState> currentState = new SyncVar<DeathState>(DeathState.Alive);
        private readonly SyncVar<float> stateStartTime = new SyncVar<float>();

        private CharacterStats characterStats;
        private CharacterPredictedMovement _characterPredictedMovement;
        private CharacterCombat characterCombat;
        private NetworkPlayer networkPlayer;
        private NetworkPlayer revivingPlayer;
        private float reviveProgress = 0f;

        private enum DeathState
        {
            Alive,
            Downed,
            Dead,
            Reviving
        }

        public bool IsAlive => currentState.Value == DeathState.Alive;
        public bool IsDowned => currentState.Value == DeathState.Downed;
        public bool IsDead => currentState.Value == DeathState.Dead;
        public float ReviveProgress => reviveProgress;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Get NetworkPlayer using ComponentFinder
            networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            
            // Use ComponentRegistry instead of GetComponent (event-based, no FindObject)
            if (networkPlayer != null)
            {
                characterStats = ComponentRegistry.GetCharacterStats(networkPlayer);
                var movement = ComponentRegistry.GetMovementController(networkPlayer);
                if (movement is CharacterPredictedMovement predictedMovement)
                {
                    _characterPredictedMovement = predictedMovement;
                }
                characterCombat = ComponentRegistry.GetCharacterCombat(networkPlayer);
            }
            else
            {
                // Fallback to ComponentFinder if NetworkPlayer not found (shouldn't happen normally)
                characterStats = gameObject.FindInHierarchy<CharacterStats>();
                _characterPredictedMovement = gameObject.FindInHierarchy<CharacterPredictedMovement>();
                characterCombat = gameObject.FindInHierarchy<CharacterCombat>();
            }
            
            // Subscribe to state changes
            currentState.OnChange += OnDeathStateChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (currentState != null)
                currentState.OnChange -= OnDeathStateChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            UpdateDeathState();
        }

        /// <summary>
        /// Update death state
        /// </summary>
        private void UpdateDeathState()
        {
            if (characterStats == null) return;

            switch (currentState.Value)
            {
                case DeathState.Alive:
                    // Check if should go downed
                    if (!characterStats.IsAlive())
                    {
                        SetState(DeathState.Downed);
                    }
                    break;

                case DeathState.Downed:
                    // Check bleedout
                    if (Time.time - stateStartTime.Value >= bleedoutTime)
                    {
                        SetState(DeathState.Dead);
                    }
                    break;

                case DeathState.Reviving:
                    UpdateRevive();
                    break;
            }
        }

        /// <summary>
        /// Server: Set death state
        /// </summary>
        [Server]
        private void SetState(DeathState newState)
        {
            currentState.Value = newState;
            stateStartTime.Value = Time.time;

            switch (newState)
            {
                case DeathState.Downed:
                    OnDowned();
                    break;
                case DeathState.Dead:
                    OnDead();
                    break;
                case DeathState.Alive:
                    OnRevived();
                    break;
            }
        }

        /// <summary>
        /// Server: Handle downed state
        /// </summary>
        [Server]
        private void OnDowned()
        {
            // Disable movement and combat
            if (_characterPredictedMovement != null)
            {
                _characterPredictedMovement.SetMoveInput(Vector2.zero);
            }

            if (characterCombat != null)
            {
                characterCombat.SetAttacking(false);
            }

            // Visual effect
            RpcOnDowned();
        }

        /// <summary>
        /// Server: Handle death
        /// </summary>
        [Server]
        private void OnDead()
        {
            // Award kill to killer (would need killer tracking)
            // Handle respawn
            HandleRespawn();
        }

        /// <summary>
        /// Server: Handle revive
        /// </summary>
        [Server]
        private void OnRevived()
        {
            // Restore HP
            if (characterStats != null)
            {
                characterStats.SetHP(revivedHP);
            }

            // Re-enable movement and combat
            revivingPlayer = null;
            reviveProgress = 0f;

            // Visual effect
            RpcOnRevived();
        }

        /// <summary>
        /// Server: Start revive
        /// </summary>
        [Server]
        public void StartRevive(NetworkPlayer reviver)
        {
            if (currentState.Value != DeathState.Downed) return;
            if (revivingPlayer != null) return; // Already being revived

            revivingPlayer = reviver;
            currentState.Value = DeathState.Reviving;
            stateStartTime.Value = Time.time;
        }

        /// <summary>
        /// Server: Cancel revive
        /// </summary>
        [Server]
        public void CancelRevive()
        {
            if (currentState.Value == DeathState.Reviving)
            {
                currentState.Value = DeathState.Downed;
                revivingPlayer = null;
                reviveProgress = 0f;
            }
        }

        /// <summary>
        /// Server: Update revive progress
        /// </summary>
        [Server]
        private void UpdateRevive()
        {
            if (revivingPlayer == null || !revivingPlayer.IsSpawned)
            {
                CancelRevive();
                return;
            }

            // Check distance
            float distance = Vector3.Distance(transform.position, revivingPlayer.transform.position);
            if (distance > reviveRange)
            {
                CancelRevive();
                return;
            }

            // Update progress
            reviveProgress = (Time.time - stateStartTime.Value) / reviveTime;

            if (reviveProgress >= 1f)
            {
                // Revive complete
                SetState(DeathState.Alive);
            }
        }

        /// <summary>
        /// Server: Handle respawn
        /// </summary>
        [Server]
        private void HandleRespawn()
        {
            // Check if can respawn (phase, beacon, etc.)
            var phaseManager = FindObjectOfType<Match.MatchPhaseManager>();
            if (phaseManager != null && !phaseManager.IsRespawnEnabled())
            {
                // Can't respawn in this phase
                return;
            }

            // Find respawn location
            var spawnSystem = FindObjectOfType<Spawn.PlayerSpawnSystem>();
            if (spawnSystem != null)
            {
                Vector3 spawnPos = spawnSystem.SpawnPlayer(networkPlayer, networkPlayer.TeamId);
            }
            else
            {
                // Default respawn
                RespawnAtPosition(Vector3.zero);
            }
        }

        /// <summary>
        /// Server: Respawn at position
        /// </summary>
        [Server]
        private void RespawnAtPosition(Vector3 position)
        {
            transform.position = position;
            
            // Reset stats
            if (characterStats != null)
            {
                characterStats.SetHP(characterStats.GetMaxHP());
                characterStats.SetStamina(characterStats.GetMaxStamina());
            }

            SetState(DeathState.Alive);
        }

        /// <summary>
        /// Client: On downed
        /// </summary>
        [ObserversRpc]
        private void RpcOnDowned()
        {
            if (downedIndicator != null)
            {
                downedIndicator.SetActive(true);
            }
        }

        /// <summary>
        /// Client: On revived
        /// </summary>
        [ObserversRpc]
        private void RpcOnRevived()
        {
            if (downedIndicator != null)
            {
                downedIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// Client: On death state changed
        /// </summary>
        private void OnDeathStateChanged(DeathState oldState, DeathState newState, bool asServer)
        {
            if (newState == DeathState.Dead)
            {
                if (deathEffect != null)
                {
                    Instantiate(deathEffect, transform.position, Quaternion.identity);
                }
            }
        }
    }
}

