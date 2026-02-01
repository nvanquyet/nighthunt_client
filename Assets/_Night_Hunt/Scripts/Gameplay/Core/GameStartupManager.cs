using UnityEngine;
using System.Collections;
using System;
using FishNet;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.ClientEffects;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Team;
using NightHunt.InteractionSystem.Loot.Spawn;

namespace NightHunt.Gameplay.Core
{
    /// <summary>
    /// Centralized manager for game startup flow, ensuring proper initialization order and readiness states.
    /// Server-only execution with support for late join and reconnection.
    /// </summary>
    public class GameStartupManager : NetworkBehaviour
    {
        public enum StartupPhase
        {
            NotStarted,
            CoreSystems,
            LootSystems,
            GameplaySystems,
            PlayerSystems,
            Ready
        }

        [Header("System References")]
        [SerializeField] private GameplayEventBus eventBus;
        [SerializeField] private InputLayerManager inputLayerManager;
        [SerializeField] private ClientEffectManager clientEffectManager;
        [SerializeField] private LootSpawnManager lootSpawnManager;
        [SerializeField] private LootContainerManager lootContainerManager;
        [SerializeField] private MatchPhaseManager matchPhaseManager;
        [SerializeField] private ScoringSystem scoringSystem;
        [SerializeField] private ZoneSystem zoneSystem;
        [SerializeField] private TeamSystem teamSystem;
        [SerializeField] private PlayerSpawnSystem spawnSystem;

        [Header("Settings")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private float phaseDelay = 0.1f;

        private StartupPhase currentPhase = StartupPhase.NotStarted;
        private bool isInitializing = false;
        private bool isGameReady = false;

        // Events
        public event Action<StartupPhase> OnPhaseCompleted;
        public event Action OnGameReady;

        public StartupPhase CurrentPhase => currentPhase;
        public bool IsGameReady => isGameReady;

        private void Start()
        {
            if (autoInitialize && InstanceFinder.IsServer)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Initialize the startup sequence (server only).
        /// </summary>
        [Server]
        public void Initialize()
        {
            if (!InstanceFinder.IsServer)
            {
                Debug.LogWarning("[GameStartupManager] Initialize can only be called on server!");
                return;
            }

            if (isInitializing || isGameReady)
            {
                Debug.LogWarning("[GameStartupManager] Already initialized or initializing!");
                return;
            }

            Debug.Log("[GameStartupManager] Starting game startup sequence...");
            StartCoroutine(StartupSequence());
        }

        /// <summary>
        /// Main startup sequence coroutine.
        /// </summary>
        private IEnumerator StartupSequence()
        {
            isInitializing = true;

            yield return InitializeCoreSystems();
            yield return new WaitForSeconds(phaseDelay);
            CompletePhase(StartupPhase.CoreSystems);

            yield return InitializeLootSystems();
            yield return new WaitForSeconds(phaseDelay);
            CompletePhase(StartupPhase.LootSystems);

            yield return InitializeGameplaySystems();
            yield return new WaitForSeconds(phaseDelay);
            CompletePhase(StartupPhase.GameplaySystems);

            yield return InitializePlayerSystems();
            yield return new WaitForSeconds(phaseDelay);
            CompletePhase(StartupPhase.PlayerSystems);

            isInitializing = false;
            isGameReady = true;
            currentPhase = StartupPhase.Ready;
            OnGameReady?.Invoke();
            Debug.Log("[GameStartupManager] Game startup complete! Game is ready.");
        }

        /// <summary>
        /// Phase 1: Initialize core systems (EventBus, InputLayerManager, ClientEffectManager).
        /// </summary>
        private IEnumerator InitializeCoreSystems()
        {
            Debug.Log("[GameStartupManager] Phase 1: Initializing core systems...");

            // Find or create EventBus
            if (eventBus == null)
            {
                eventBus = GameplayEventBus.Instance;
                if (eventBus == null)
                {
                    GameObject go = new GameObject("GameplayEventBus");
                    eventBus = go.AddComponent<GameplayEventBus>();
                    Debug.Log("[GameStartupManager] Created GameplayEventBus");
                }
            }

            // Find or create InputLayerManager
            if (inputLayerManager == null)
            {
                inputLayerManager = InputLayerManager.Instance;
                if (inputLayerManager == null)
                {
                    GameObject go = new GameObject("InputLayerManager");
                    inputLayerManager = go.AddComponent<InputLayerManager>();
                    Debug.Log("[GameStartupManager] Created InputLayerManager");
                }
            }

            // Find or create ClientEffectManager
            if (clientEffectManager == null)
            {
                clientEffectManager = ClientEffectManager.Instance;
                if (clientEffectManager == null)
                {
                    GameObject go = new GameObject("ClientEffectManager");
                    clientEffectManager = go.AddComponent<ClientEffectManager>();
                    Debug.Log("[GameStartupManager] Created ClientEffectManager");
                }
            }

            Debug.Log("[GameStartupManager] Core systems initialized");
            yield return null;
        }

        /// <summary>
        /// Phase 2: Initialize loot systems (LootSpawnManager, LootContainerManager).
        /// </summary>
        private IEnumerator InitializeLootSystems()
        {
            Debug.Log("[GameStartupManager] Phase 2: Initializing loot systems...");

            // Find LootSpawnManager
            if (lootSpawnManager == null)
            {
                lootSpawnManager = FindFirstObjectByType<LootSpawnManager>();
                if (lootSpawnManager == null)
                {
                    Debug.LogWarning("[GameStartupManager] LootSpawnManager not found! Loot spawn points will not work.");
                }
            }

            // Find LootContainerManager
            if (lootContainerManager == null)
            {
                lootContainerManager = FindFirstObjectByType<LootContainerManager>();
                if (lootContainerManager == null)
                {
                    Debug.LogWarning("[GameStartupManager] LootContainerManager not found! Containers will not be pre-generated.");
                }
            }

            // Note: Each LootSpawnPoint handles its own spawning based on its config
            // LootSpawnManager only manages spawn points, not automatic spawning
            // Note: Each NetworkLootContainer handles its own pre-generation based on its config
            // LootContainerManager only manages spawn points, not container pre-generation

            Debug.Log("[GameStartupManager] Loot systems initialized");
            yield return null;
        }

        /// <summary>
        /// Phase 3: Initialize gameplay systems (MatchPhaseManager, ScoringSystem, ZoneSystem, etc.).
        /// </summary>
        private IEnumerator InitializeGameplaySystems()
        {
            Debug.Log("[GameStartupManager] Phase 3: Initializing gameplay systems...");

            // Find MatchPhaseManager
            if (matchPhaseManager == null)
            {
                matchPhaseManager = FindFirstObjectByType<MatchPhaseManager>();
            }

            // Find ScoringSystem
            if (scoringSystem == null)
            {
                scoringSystem = FindFirstObjectByType<ScoringSystem>();
            }

            // Find ZoneSystem
            if (zoneSystem == null)
            {
                zoneSystem = ZoneSystem.Instance;
            }

            Debug.Log("[GameStartupManager] Gameplay systems initialized");
            yield return null;
        }

        /// <summary>
        /// Phase 4: Initialize player systems (TeamSystem, PlayerSpawnSystem).
        /// Note: Actual player spawning is temporarily skipped for local testing.
        /// </summary>
        private IEnumerator InitializePlayerSystems()
        {
            Debug.Log("[GameStartupManager] Phase 4: Initializing player systems...");

            // Find TeamSystem
            if (teamSystem == null)
            {
                teamSystem = FindFirstObjectByType<TeamSystem>();
            }

            // Find PlayerSpawnSystem
            if (spawnSystem == null)
            {
                spawnSystem = FindFirstObjectByType<PlayerSpawnSystem>();
            }

            // TODO: Spawn players (temporarily skipped for local testing)
            // if (spawnSystem != null)
            // {
            //     spawnSystem.SpawnAllPlayers();
            // }

            Debug.Log("[GameStartupManager] Player systems initialized (player spawning skipped for local testing)");
            yield return null;
        }

        /// <summary>
        /// Complete a phase and notify listeners.
        /// </summary>
        private void CompletePhase(StartupPhase phase)
        {
            currentPhase = phase;
            OnPhaseCompleted?.Invoke(phase);
            Debug.Log($"[GameStartupManager] Phase completed: {phase}");
        }

        /// <summary>
        /// Sync game state to a late-joining client (server only).
        /// </summary>
        [Server]
        public void SyncStateToClient(NetworkConnection conn)
        {
            if (!InstanceFinder.IsServer)
            {
                Debug.LogWarning("[GameStartupManager] SyncStateToClient can only be called on server!");
                return;
            }

            if (conn == null || !conn.IsValid)
            {
                Debug.LogWarning("[GameStartupManager] Invalid connection for state sync");
                return;
            }

            Debug.Log($"[GameStartupManager] Syncing game state to client {conn.ClientId}");

            // Create state snapshot
            GameStateSnapshot snapshot = new GameStateSnapshot
            {
                currentPhase = currentPhase,
                isGameReady = isGameReady,
                // TODO: Add more state data (spawned items, container states, etc.)
            };

            // Send to client
            TargetSyncGameState(conn, snapshot);
        }

        /// <summary>
        /// Client receives game state snapshot and applies it.
        /// </summary>
        [TargetRpc]
        private void TargetSyncGameState(NetworkConnection conn, GameStateSnapshot snapshot)
        {
            Debug.Log($"[GameStartupManager] Received game state snapshot: Phase={snapshot.currentPhase}, Ready={snapshot.isGameReady}");

            // Apply state
            currentPhase = snapshot.currentPhase;
            isGameReady = snapshot.isGameReady;

            // TODO: Apply additional state (spawn items, containers, etc.)
        }

        /// <summary>
        /// Snapshot of game state for late join sync.
        /// </summary>
        [Serializable]
        private struct GameStateSnapshot
        {
            public StartupPhase currentPhase;
            public bool isGameReady;
            // TODO: Add more fields as needed (spawned items, container states, etc.)
        }
    }
}
