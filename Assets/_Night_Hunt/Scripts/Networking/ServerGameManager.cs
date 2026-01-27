using UnityEngine;
using FishNet.Object;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Team;
using NightHunt.Gameplay.PredatorPrey;
using NightHunt.Gameplay.AntiCamping;
using NightHunt.Gameplay.Loot;
using NightHunt.Gameplay.Objective;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.Vision;
using NightHunt.Gameplay.Core;
using FishNet.Component.Spawning;
using System.Collections;

namespace NightHunt.Networking
{
    /// <summary>
    /// Server Game Manager - Orchestrates game logic
    /// Uses event subscription instead of direct calls for decoupling
    /// </summary>
    public class ServerGameManager : NetworkBehaviour
    {
        [Header("Bootstrap Controller")]
        [SerializeField] private GameplayBootstrap gameplayBootstrap;

        [Header("FishNet Components")]
        [SerializeField] private PlayerSpawner fishnetSpawner;

        // Cached references (from Bootstrap)
        private MatchPhaseManager matchPhaseManager;
        private ScoringSystem scoringSystem;
        private PlayerSpawnSystem spawnSystem;
        private TeamSystem teamSystem;
        private PredatorPreySystem predatorPreySystem;
        private AntiCampingSystem antiCampingSystem;
        private LootSpawner lootSpawner;
        private ObjectiveSystem objectiveSystem;
        private ZoneSystem zoneSystem;
        private VisionSystem visionSystem;

        private bool systemsInitialized = false;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[ServerGameManager] Server starting...");
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            if (IsServerStarted)
            {
                // Auto-find FishNet PlayerSpawner if not assigned
                if (fishnetSpawner == null)
                {
                    fishnetSpawner = FindFirstObjectByType<PlayerSpawner>();
                }

                // Subscribe to FishNet's spawner event
                if (fishnetSpawner != null)
                {
                    fishnetSpawner.OnSpawned += OnPlayerNetworkObjectSpawned;
                    Debug.Log("[ServerGameManager] Subscribed to PlayerSpawner.OnSpawned");
                }

                // Subscribe to Bootstrap initialization
                if (gameplayBootstrap != null)
                {
                    if (gameplayBootstrap.IsInitialized)
                    {
                        OnBootstrapInitialized();
                    }
                    else
                    {
                        gameplayBootstrap.OnSystemsInitialized += OnBootstrapInitialized;
                    }
                }
                else
                {
                    Debug.LogError("[ServerGameManager] GameplayBootstrap reference is null!");
                }
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            // Unsubscribe from events
            if (gameplayBootstrap != null)
            {
                gameplayBootstrap.OnSystemsInitialized -= OnBootstrapInitialized;
            }

            if (fishnetSpawner != null)
            {
                fishnetSpawner.OnSpawned -= OnPlayerNetworkObjectSpawned;
            }

            // ✅ Unsubscribe from MatchPhaseManager events
            if (matchPhaseManager != null)
            {
                matchPhaseManager.OnPhaseStarted -= OnPhaseStarted;
                matchPhaseManager.OnPhaseTransitioned -= OnPhaseTransitioned;
            }

            Debug.Log("[ServerGameManager] Server stopped");
        }

        /// <summary>
        /// Callback when Bootstrap has initialized
        /// </summary>
        private void OnBootstrapInitialized()
        {
            Debug.Log("[ServerGameManager] Initializing server game systems...");

            // Get references from Bootstrap
            GetSystemReferencesFromBootstrap();

            systemsInitialized = true;
            Debug.Log("[ServerGameManager] Server systems initialized successfully");

            // ✅ Subscribe to MatchPhaseManager events (decoupled)
            SubscribeToPhaseEvents();

            // CRITICAL: Spawn core NetworkObjects BEFORE starting match
            SpawnCoreNetworkObjects();

            // Start match
            StartMatch();
        }

        /// <summary>
        /// ✅ Subscribe to MatchPhaseManager events
        /// This decouples ServerGameManager from MatchPhaseManager
        /// </summary>
        private void SubscribeToPhaseEvents()
        {
            if (matchPhaseManager != null)
            {
                matchPhaseManager.OnPhaseStarted += OnPhaseStarted;
                matchPhaseManager.OnPhaseTransitioned += OnPhaseTransitioned;
                Debug.Log("[ServerGameManager] Subscribed to MatchPhaseManager events");
            }
            else
            {
                Debug.LogWarning("[ServerGameManager] MatchPhaseManager not found, cannot subscribe to events");
            }
        }

        /// <summary>
        /// ✅ Event handler: Phase started
        /// Called when a new phase starts
        /// </summary>
        [Server]
        private void OnPhaseStarted(MatchPhaseState newPhase, string phaseName)
        {
            Debug.Log($"[ServerGameManager] Phase started: {phaseName} ({newPhase})");

            // Delegate to GameplayBootstrap to activate phase-specific systems
            if (gameplayBootstrap == null) return;

            switch (newPhase)
            {
                case MatchPhaseState.Preparation:
                    gameplayBootstrap.ActivatePhase1Systems();
                    break;
                
                case MatchPhaseState.Hunt:
                    gameplayBootstrap.ActivatePhase2Systems();
                    break;
                
                case MatchPhaseState.Lockdown:
                    gameplayBootstrap.ActivatePhase3Systems();
                    break;
            }
        }

        /// <summary>
        /// ✅ Event handler: Phase transitioned
        /// Called when transitioning from one phase to another
        /// </summary>
        [Server]
        private void OnPhaseTransitioned(MatchPhaseState oldPhase, MatchPhaseState newPhase)
        {
            Debug.Log($"[ServerGameManager] Phase transition: {oldPhase} → {newPhase}");

            // Handle phase transition logic
            // e.g., cleanup old phase, prepare new phase
        }

        /// <summary>
        /// Spawn core NetworkObjects BEFORE starting match
        /// </summary>
        [Server]
        private void SpawnCoreNetworkObjects()
        {
            Debug.Log("[ServerGameManager] Spawning core network objects...");

            if (gameplayBootstrap == null) return;

            // Spawn MatchPhaseManager (CRITICAL - must spawn first!)
            gameplayBootstrap.SpawnOrActivateSystem(matchPhaseManager, "MatchPhaseManager");

            // Spawn ScoringSystem
            gameplayBootstrap.SpawnOrActivateSystem(scoringSystem, "ScoringSystem");

            // Spawn server-only systems
            gameplayBootstrap.SpawnOrActivateSystem(spawnSystem, "PlayerSpawnSystem");
            gameplayBootstrap.SpawnOrActivateSystem(teamSystem, "TeamSystem");

            Debug.Log("[ServerGameManager] Core network objects spawned!");
        }

        /// <summary>
        /// Get system references from GameplayBootstrap
        /// </summary>
        private void GetSystemReferencesFromBootstrap()
        {
            if (gameplayBootstrap == null) return;

            gameplayBootstrap.GetSystemReferences(
                out matchPhaseManager,
                out scoringSystem,
                out predatorPreySystem,
                out zoneSystem,
                out lootSpawner,
                out antiCampingSystem,
                out visionSystem
            );

            spawnSystem = gameplayBootstrap.SpawnSystem;
            teamSystem = gameplayBootstrap.TeamSystem;
            objectiveSystem = gameplayBootstrap.ObjectiveSystem;

            // Validate critical systems
            if (matchPhaseManager == null)
                Debug.LogError("[ServerGameManager] MatchPhaseManager not found!");
            if (spawnSystem == null)
                Debug.LogError("[ServerGameManager] PlayerSpawnSystem not found!");
            if (teamSystem == null)
                Debug.LogError("[ServerGameManager] TeamSystem not found!");
        }

        /// <summary>
        /// Server: Start a new match
        /// </summary>
        [Server]
        private void StartMatch()
        {
            Debug.Log("[ServerGameManager] Starting new match...");

            // Start Phase 1
            if (matchPhaseManager != null && matchPhaseManager.IsSpawned)
            {
                matchPhaseManager.StartPhase(MatchPhaseState.Preparation);
            }
            else
            {
                Debug.LogError("[ServerGameManager] Cannot start match - MatchPhaseManager not ready!");
            }
        }

        /// <summary>
        /// Called when FishNet spawns a player NetworkObject
        /// </summary>
        private void OnPlayerNetworkObjectSpawned(NetworkObject networkObject)
        {
            Debug.Log($"[ServerGameManager] OnPlayerNetworkObjectSpawned START - ObjectId: {networkObject?.ObjectId}, IsSpawned: {networkObject?.IsSpawned}");
            
            // Debug: Log GameObject info để tìm trong Hierarchy
            if (networkObject != null && networkObject.gameObject != null)
            {
                GameObject go = networkObject.gameObject;
                string sceneName = go.scene.IsValid() ? go.scene.name : "Invalid Scene";
                string parentName = go.transform.parent != null ? go.transform.parent.name : "None (Root)";
                string hierarchyPath = GetHierarchyPath(go.transform);
                
                Debug.Log($"[ServerGameManager] Player GameObject Info:\n" +
                         $"  - Name: {go.name}\n" +
                         $"  - Scene: {sceneName}\n" +
                         $"  - Parent: {parentName}\n" +
                         $"  - Hierarchy Path: {hierarchyPath}\n" +
                         $"  - Position: {go.transform.position}\n" +
                         $"  - Active: {go.activeSelf}\n" +
                         $"  - IsSpawned: {networkObject.IsSpawned}");
            }

            // Get NetworkPlayer component
            NetworkPlayer player = networkObject?.GetComponent<NetworkPlayer>();
            if (player == null)
            {
                Debug.LogError("[ServerGameManager] NetworkObject doesn't have NetworkPlayer component!");
                return;
            }

            // Initialize player (team assignment, spawn positioning)
            Debug.Log($"[ServerGameManager] About to call InitializePlayer. Player IsSpawned: {player.IsSpawned}, ObjectId: {player.ObjectId}");
            
            try
            {
                InitializePlayer(player);
                Debug.Log($"[ServerGameManager] InitializePlayer returned successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ServerGameManager] EXCEPTION in InitializePlayer: {ex.Message}\n{ex.StackTrace}");
                return;
            }
            
            // Debug: Check if player still exists after initialization
            if (player == null || !player.IsSpawned || player.gameObject == null)
            {
                Debug.LogError($"[ServerGameManager] Player was destroyed/despawned during InitializePlayer! " +
                             $"player==null: {player == null}, IsSpawned: {player?.IsSpawned}, gameObject==null: {player?.gameObject == null}");
                return;
            }
            
            Debug.Log($"[ServerGameManager] Player still valid after InitializePlayer: {player.gameObject.name}, IsSpawned: {player.IsSpawned}, ObjectId: {player.ObjectId}");
            
            // Final check - is object still alive?
            if (networkObject != null && networkObject.gameObject != null && networkObject.IsSpawned)
            {
                Debug.Log($"[ServerGameManager] OnPlayerNetworkObjectSpawned END - NetworkObject still exists: {networkObject.gameObject.name}, IsSpawned: {networkObject.IsSpawned}, Scene: {networkObject.gameObject.scene.name}");
                
                // Check again after a frame to see if it gets destroyed
                StartCoroutine(CheckPlayerStillExistsAfterFrame(networkObject));
            }
            else
            {
                Debug.LogError($"[ServerGameManager] OnPlayerNetworkObjectSpawned END - NetworkObject was destroyed! " +
                             $"networkObject==null: {networkObject == null}, gameObject==null: {networkObject?.gameObject == null}, IsSpawned: {networkObject?.IsSpawned}");
            }
        }

        /// <summary>
        /// Check if player still exists after a frame (to catch immediate destruction)
        /// </summary>
        private IEnumerator CheckPlayerStillExistsAfterFrame(NetworkObject networkObject)
        {
            yield return null; // Wait one frame
            
            try
            {
                if (networkObject != null && networkObject.gameObject != null && networkObject.IsSpawned)
                {
                    Debug.Log($"[ServerGameManager] Player still exists after 1 frame: {networkObject.gameObject.name}, IsSpawned: {networkObject.IsSpawned}");
                }
                else
                {
                    Debug.LogError($"[ServerGameManager] Player was destroyed within 1 frame! " +
                                 $"networkObject==null: {networkObject == null}, IsSpawned: {networkObject?.IsSpawned}");
                }
            }
            catch (UnityEngine.MissingReferenceException ex)
            {
                Debug.LogError($"[ServerGameManager] Player was destroyed within 1 frame (MissingReferenceException): {ex.Message}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ServerGameManager] Exception checking player after 1 frame: {ex.Message}");
            }
        }

        /// <summary>
        /// Get full hierarchy path for debugging
        /// </summary>
        private string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return "null";
            
            string path = transform.name;
            Transform current = transform.parent;
            
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        /// <summary>
        /// Initialize player with game-specific logic
        /// </summary>
        [Server]
        private void InitializePlayer(NetworkPlayer player)
        {
            try
            {
                Debug.Log($"[ServerGameManager] InitializePlayer START - Player: {player?.gameObject?.name}, IsSpawned: {player?.IsSpawned}");
                
                if (!systemsInitialized)
                {
                    Debug.LogWarning("[ServerGameManager] Systems not initialized yet, delaying player initialization");
                    StartCoroutine(InitializePlayerDelayed(player));
                    return;
                }

                if (player == null || !player.IsSpawned)
                {
                    Debug.LogError($"[ServerGameManager] Cannot initialize invalid player! player==null: {player == null}, IsSpawned: {player?.IsSpawned}");
                    return;
                }

                Debug.Log($"[ServerGameManager] Initializing player: {player.PlayerName}");

                // Set player name if empty
                if (string.IsNullOrEmpty(player.PlayerName))
                {
                    Debug.Log($"[ServerGameManager] Setting player name to Player_{player.Owner.ClientId}");
                    player.SetPlayerName($"Player_{player.Owner.ClientId}");
                }

                // Assign to team
                Debug.Log($"[ServerGameManager] Assigning player to team...");
                int teamId = AssignPlayerTeam(player);
                Debug.Log($"[ServerGameManager] Player assigned to team {teamId}");

                // Position at spawn point
                Debug.Log($"[ServerGameManager] Positioning player at spawn...");
                PositionPlayerAtSpawn(player, teamId);
                Debug.Log($"[ServerGameManager] Player positioned");

                Debug.Log($"[ServerGameManager] Player initialized: {player.PlayerName} on Team {teamId}");
                Debug.Log($"[ServerGameManager] InitializePlayer END - Player still spawned: {player.IsSpawned}, ObjectId: {player.ObjectId}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ServerGameManager] EXCEPTION in InitializePlayer: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Delay initialization if systems not ready
        /// </summary>
        private IEnumerator InitializePlayerDelayed(NetworkPlayer player)
        {
            // Wait until systems are initialized
            while (!systemsInitialized)
            {
                yield return null;
            }

            // Now initialize
            InitializePlayer(player);
        }

        /// <summary>
        /// Assign player to team
        /// </summary>
        [Server]
        private int AssignPlayerTeam(NetworkPlayer player)
        {
            if (teamSystem != null && teamSystem.IsSpawned)
            {
                int teamId = teamSystem.AssignPlayerToTeam(player);
                Debug.Log($"[ServerGameManager] Player assigned to team {teamId}");
                return teamId;
            }
            else
            {
                Debug.LogWarning("[ServerGameManager] TeamSystem not available, using default team 0");
                player.SetTeamId(0);
                return 0;
            }
        }

        /// <summary>
        /// Position player at spawn point
        /// </summary>
        [Server]
        private void PositionPlayerAtSpawn(NetworkPlayer player, int teamId)
        {
            if (spawnSystem != null && spawnSystem.IsSpawned)
            {
                Vector3 spawnPosition = spawnSystem.SpawnPlayer(player, teamId);
                Debug.Log($"[ServerGameManager] Player positioned at {spawnPosition}");
            }
            else
            {
                Debug.LogWarning("[ServerGameManager] PlayerSpawnSystem not available, player remains at origin");
            }
        }

        /// <summary>
        /// Server: Handle player disconnected
        /// </summary>
        [Server]
        public void OnPlayerDisconnected(NetworkPlayer player)
        {
            if (antiCampingSystem != null)
            {
                antiCampingSystem.ResetPlayerCamping((uint)player.ObjectId);
            }

            if (teamSystem != null)
            {
                teamSystem.RemovePlayerFromTeam(player);
            }

            Debug.Log($"[ServerGameManager] Player {player.PlayerName} disconnected");
        }

        /// <summary>
        /// Server: Handle player death
        /// </summary>
        [Server]
        public void OnPlayerDeath(NetworkPlayer victim, NetworkPlayer killer = null)
        {
            if (scoringSystem == null) return;

            if (killer != null)
            {
                scoringSystem.AwardKill((uint)killer.ObjectId, (uint)victim.ObjectId);
            }

            if (predatorPreySystem != null)
            {
                predatorPreySystem.UpdateRoles();
            }

            Debug.Log($"[ServerGameManager] Player {victim.PlayerName} died" + 
                     (killer != null ? $" (killed by {killer.PlayerName})" : ""));
        }

        /// <summary>
        /// Server: Respawn player after death
        /// </summary>
        [Server]
        public void RespawnPlayer(NetworkPlayer player, float delay = 3f)
        {
            if (player == null || !player.IsSpawned)
            {
                Debug.LogError("[ServerGameManager] Cannot respawn invalid player!");
                return;
            }

            StartCoroutine(RespawnPlayerDelayed(player, delay));
        }

        private IEnumerator RespawnPlayerDelayed(NetworkPlayer player, float delay)
        {
            Debug.Log($"[ServerGameManager] Player {player.PlayerName} will respawn in {delay} seconds");
            
            yield return new WaitForSeconds(delay);
            
            if (player != null && player.IsSpawned)
            {
                // Keep current team
                int teamId = player.TeamId;

                // Reposition at spawn point
                if (spawnSystem != null && spawnSystem.IsSpawned)
                {
                    Vector3 spawnPosition = spawnSystem.SpawnPlayer(player, teamId);
                    Debug.Log($"[ServerGameManager] Player {player.PlayerName} respawned at {spawnPosition}");
                }
            }
        }

        // Public getters
        public MatchPhaseManager MatchPhaseManager => matchPhaseManager;
        public ScoringSystem ScoringSystem => scoringSystem;
        public PlayerSpawnSystem SpawnSystem => spawnSystem;
        public TeamSystem TeamSystem => teamSystem;
        public PredatorPreySystem PredatorPreySystem => predatorPreySystem;
        public AntiCampingSystem AntiCampingSystem => antiCampingSystem;
        public LootSpawner LootSpawner => lootSpawner;
        public ObjectiveSystem ObjectiveSystem => objectiveSystem;
        public ZoneSystem ZoneSystem => zoneSystem;
        public VisionSystem VisionSystem => visionSystem;
        public GameplayBootstrap Bootstrap => gameplayBootstrap;
    }
}