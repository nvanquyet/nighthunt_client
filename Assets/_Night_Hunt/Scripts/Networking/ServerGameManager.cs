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

namespace NightHunt.Networking
{
    /// <summary>
    /// Server-side game manager
    /// Handles all server-authoritative game logic
    /// Works with both host and dedicated server
    /// </summary>
    public class ServerGameManager : NetworkBehaviour
    {
        [Header("Server Systems")]
        [SerializeField] private MatchPhaseManager matchPhaseManager;
        [SerializeField] private ScoringSystem scoringSystem;
        [SerializeField] private PlayerSpawnSystem spawnSystem;
        [SerializeField] private TeamSystem teamSystem;
        [SerializeField] private PredatorPreySystem predatorPreySystem;
        [SerializeField] private AntiCampingSystem antiCampingSystem;
        [SerializeField] private LootSpawner lootSpawner;
        [SerializeField] private ObjectiveSystem objectiveSystem;

        private bool systemsInitialized = false;

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            Debug.Log("[ServerGameManager] Initializing server game systems...");
            InitializeServerSystems();
        }

        /// <summary>
        /// Initialize all server systems
        /// </summary>
        [Server]
        private void InitializeServerSystems()
        {
            if (systemsInitialized) return;

            // Find or create server systems
            if (matchPhaseManager == null)
                matchPhaseManager = FindObjectOfType<MatchPhaseManager>();

            if (scoringSystem == null)
                scoringSystem = FindObjectOfType<ScoringSystem>();

            if (spawnSystem == null)
                spawnSystem = FindObjectOfType<PlayerSpawnSystem>();

            if (teamSystem == null)
                teamSystem = FindObjectOfType<TeamSystem>();

            if (predatorPreySystem == null)
                predatorPreySystem = FindObjectOfType<PredatorPreySystem>();

            if (antiCampingSystem == null)
                antiCampingSystem = FindObjectOfType<AntiCampingSystem>();

            if (lootSpawner == null)
                lootSpawner = FindObjectOfType<LootSpawner>();

            if (objectiveSystem == null)
                objectiveSystem = FindObjectOfType<ObjectiveSystem>();

            // Start match
            StartMatch();

            systemsInitialized = true;
            Debug.Log("[ServerGameManager] Server systems initialized successfully");
        }

        /// <summary>
        /// Server: Start a new match
        /// </summary>
        [Server]
        private void StartMatch()
        {
            Debug.Log("[ServerGameManager] Starting new match...");

            // Start Phase 1
            if (matchPhaseManager != null)
            {
                matchPhaseManager.StartPhase("Phase1_Preparation");
            }

            // Initialize objectives
            if (objectiveSystem != null)
            {
                objectiveSystem.ActivateObjectivesForPhase("Phase1_Preparation");
            }
        }

        /// <summary>
        /// Server: Handle player connected
        /// </summary>
        [Server]
        public void OnPlayerConnected(NetworkPlayer player)
        {
            // Assign team
            if (teamSystem != null)
            {
                int teamId = teamSystem.AssignPlayerToTeam(player);
                Debug.Log($"[ServerGameManager] Player {player.PlayerName} assigned to team {teamId}");
            }

            // Spawn player
            if (spawnSystem != null)
            {
                int teamId = player.TeamId;
                spawnSystem.SpawnPlayer(player, teamId);
            }
        }

        /// <summary>
        /// Server: Handle player disconnected
        /// </summary>
        [Server]
        public void OnPlayerDisconnected(NetworkPlayer player)
        {
            // Cleanup player data
            if (antiCampingSystem != null)
            {
                antiCampingSystem.ResetPlayerCamping((uint)player.ObjectId);
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

            // Award kill score
            if (killer != null)
            {
                scoringSystem.AwardKill((uint)killer.ObjectId, (uint)victim.ObjectId);
            }

            // Update predator/prey system
            if (predatorPreySystem != null)
            {
                predatorPreySystem.UpdateRoles();
            }
        }

        /// <summary>
        /// Server: Handle phase transition
        /// </summary>
        [Server]
        public void OnPhaseTransition(string newPhase)
        {
            Debug.Log($"[ServerGameManager] Phase transition to: {newPhase}");

            // Activate objectives for new phase
            if (objectiveSystem != null)
            {
                objectiveSystem.ActivateObjectivesForPhase(newPhase);
            }

            // Spawn boss in Phase 2
            if (newPhase == "Phase2_HuntObjectives")
            {
                // Boss spawning handled by ObjectiveSystem
            }
        }

        // Public getters for server systems
        public MatchPhaseManager MatchPhaseManager => matchPhaseManager;
        public ScoringSystem ScoringSystem => scoringSystem;
        public PlayerSpawnSystem SpawnSystem => spawnSystem;
        public TeamSystem TeamSystem => teamSystem;
        public PredatorPreySystem PredatorPreySystem => predatorPreySystem;
        public AntiCampingSystem AntiCampingSystem => antiCampingSystem;
        public LootSpawner LootSpawner => lootSpawner;
        public ObjectiveSystem ObjectiveSystem => objectiveSystem;
    }
}

