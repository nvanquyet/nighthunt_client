using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.ClientEffects;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.PredatorPrey;
using NightHunt.Gameplay.Zone;
using NightHunt.InteractionSystem.Loot.Spawn;
using NightHunt.Gameplay.AntiCamping;
using NightHunt.Gameplay.Vision;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Team;
using NightHunt.Gameplay.Objective;

namespace NightHunt.Gameplay.Core
{
    /// <summary>
    /// Bootstrap script - CONTROLLER chính cho tất cả systems
    /// Nhận callback từ ServerGameManager và thực hiện spawn/despawn/activate
    /// </summary>
    public class GameplayBootstrap : MonoBehaviour
    {
        [Header("Shared Systems (Client + Server)")]
        [SerializeField] private MatchPhaseManager matchPhaseManager;
        [SerializeField] private ScoringSystem scoringSystem;
        [SerializeField] private PredatorPreySystem predatorPreySystem;
        [SerializeField] private ZoneSystem zoneSystem;
        [SerializeField] private LootSpawnManager lootSpawnManager;
        [SerializeField] private LootContainerManager lootContainerManager;
        [SerializeField] private AntiCampingSystem antiCampingSystem;
        [SerializeField] private VisionSystem visionSystem;

        [Header("Server-Only Systems")]
        [SerializeField] private PlayerSpawnSystem spawnSystem;
        [SerializeField] private TeamSystem teamSystem;
        [SerializeField] private ObjectiveSystem objectiveSystem;

        [Header("Managers")]
        [SerializeField] private InputLayerManager inputLayerManager;
        [SerializeField] private GameplayEventBus eventBus;
        [SerializeField] private ClientEffectManager clientEffectManager;

        [Header("Settings")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool initializeOnStart = true;

        private bool isInitialized = false;
        private NetworkManager networkManager;

        // Events
        public event System.Action OnSystemsInitialized;

        private void Awake()
        {
            if (initializeOnStart)
            {
                Initialize();
            }
        }

        private void Start()
        {
            networkManager = InstanceFinder.NetworkManager;
        }

        /// <summary>
        /// Initialize tất cả systems
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                Debug.LogWarning("[GameplayBootstrap] Already initialized!");
                return;
            }

            Debug.Log("[GameplayBootstrap] Initializing gameplay systems...");

            InitializeCoreSystems();
            InitializeManagers();
            InitializeGameplaySystems();

            isInitialized = true;
            Debug.Log("[GameplayBootstrap] All systems initialized successfully!");

            // Notify listeners
            OnSystemsInitialized?.Invoke();
        }

        private void InitializeCoreSystems()
        {
            if (eventBus == null)
                eventBus = GameplayEventBus.Instance;

            if (clientEffectManager == null)
                clientEffectManager = ClientEffectManager.Instance;
        }

        private void InitializeManagers()
        {
            if (inputLayerManager == null)
            {
                inputLayerManager = InputLayerManager.Instance;
                if (inputLayerManager == null)
                {
                    Debug.LogWarning("[GameplayBootstrap] InputLayerManager not found! Creating one...");
                    GameObject inputManagerGO = new GameObject("InputLayerManager");
                    inputLayerManager = inputManagerGO.AddComponent<InputLayerManager>();
                }
            }
        }

        private void InitializeGameplaySystems()
        {
            // Find systems nếu chưa assign
            if (matchPhaseManager == null)
                matchPhaseManager = FindFirstObjectByType<MatchPhaseManager>();

            if (scoringSystem == null)
                scoringSystem = FindFirstObjectByType<ScoringSystem>();

            if (predatorPreySystem == null)
                predatorPreySystem = FindFirstObjectByType<PredatorPreySystem>();

            if (zoneSystem == null)
                zoneSystem = ZoneSystem.Instance;

            if (lootSpawnManager == null)
                lootSpawnManager = FindFirstObjectByType<LootSpawnManager>();
            
            if (lootContainerManager == null)
                lootContainerManager = FindFirstObjectByType<LootContainerManager>();

            if (antiCampingSystem == null)
                antiCampingSystem = FindFirstObjectByType<AntiCampingSystem>();

            if (visionSystem == null)
                visionSystem = FindFirstObjectByType<VisionSystem>();

            if (spawnSystem == null)
                spawnSystem = FindFirstObjectByType<PlayerSpawnSystem>();

            if (teamSystem == null)
                teamSystem = FindFirstObjectByType<TeamSystem>();

            if (objectiveSystem == null)
                objectiveSystem = FindFirstObjectByType<ObjectiveSystem>();
        }

        /// <summary>
        /// Activate systems cho Phase 1: Preparation
        /// </summary>
        public void ActivatePhase1Systems()
        {
            Debug.Log("[GameplayBootstrap] Activating Phase 1 systems...");

            // Spawn/Activate LootSpawnManager (package)
            SpawnOrActivateSystem(lootSpawnManager, "LootSpawnManager");
            
            // Spawn/Activate LootContainerManager (package)
            SpawnOrActivateSystem(lootContainerManager, "LootContainerManager");
            
            //Spawn/Activate TeamSystem
            SpawnOrActivateSystem(teamSystem, "TeamSystem");
            
            //Spawn/Activate SpawnSystem
            SpawnOrActivateSystem(spawnSystem, "PlayerSpawnSystem");

            // Activate VisionSystem
            SpawnOrActivateSystem(visionSystem, "VisionSystem");

            // AntiCampingSystem luôn active
            SpawnOrActivateSystem(antiCampingSystem, "AntiCampingSystem");

            Debug.Log("[GameplayBootstrap] Phase 1 systems activated!");
        }

        /// <summary>
        /// Activate systems cho Phase 2: Hunt & Objectives
        /// </summary>
        public void ActivatePhase2Systems()
        {
            Debug.Log("[GameplayBootstrap] Activating Phase 2 systems...");

            // Spawn ObjectiveSystem
            SpawnOrActivateSystem(objectiveSystem, "ObjectiveSystem");
            
            // Activate objectives
            if (objectiveSystem != null && objectiveSystem.IsServerStarted)
            {
                objectiveSystem.ActivateObjectivesForPhase("Phase2_HuntObjectives");
            }

            // Spawn PredatorPreySystem
            SpawnOrActivateSystem(predatorPreySystem, "PredatorPreySystem");
            
            // Update roles
            if (predatorPreySystem != null && predatorPreySystem.IsServerStarted)
            {
                predatorPreySystem.UpdateRoles();
            }

            Debug.Log("[GameplayBootstrap] Phase 2 systems activated!");
        }

        /// <summary>
        /// Activate systems cho Phase 3: Final Lockdown
        /// </summary>
        public void ActivatePhase3Systems()
        {
            Debug.Log("[GameplayBootstrap] Activating Phase 3 systems...");

            // Spawn ZoneSystem
            SpawnOrActivateSystem(zoneSystem, "ZoneSystem");

            // Phase 3 objectives
            if (objectiveSystem != null && objectiveSystem.IsServerStarted)
            {
                objectiveSystem.ActivateObjectivesForPhase("Phase3_FinalLockdown");
            }

            // Enhanced predator/prey
            if (predatorPreySystem != null && predatorPreySystem.IsServerStarted)
            {
                predatorPreySystem.UpdateRoles();
            }

            Debug.Log("[GameplayBootstrap] Phase 3 systems activated!");
        }

        /// <summary>
        /// Spawn NetworkObject hoặc SetActive - PUBLIC để ServerGameManager có thể dùng
        /// </summary>
        public void SpawnOrActivateSystem(Component system, string systemName)
        {
            if (system == null)
            {
                Debug.LogWarning($"[GameplayBootstrap] {systemName} is null!");
                return;
            }

            GameObject systemObject = system.gameObject;
            NetworkObject networkObject = systemObject.GetComponent<NetworkObject>();

            if (networkObject != null)
            {
                // NetworkObject - cần spawn trên server
                if (networkManager != null && networkManager.IsServerStarted)
                {
                    if (!networkObject.IsSpawned)
                    {
                        networkManager.ServerManager.Spawn(networkObject);
                        Debug.Log($"[GameplayBootstrap] Spawned {systemName} on network");
                    }
                    else
                    {
                        Debug.Log($"[GameplayBootstrap] {systemName} already spawned");
                    }
                }
            }
            else
            {
                // Non-NetworkObject - chỉ SetActive
                if (!systemObject.activeSelf)
                {
                    systemObject.SetActive(true);
                    Debug.Log($"[GameplayBootstrap] Activated {systemName}");
                }
            }
        }

        /// <summary>
        /// Deactivate/Despawn system
        /// </summary>
        public void DeactivateSystem(Component system, string systemName)
        {
            if (system == null) return;

            GameObject systemObject = system.gameObject;
            NetworkObject networkObject = systemObject.GetComponent<NetworkObject>();

            if (networkObject != null && networkObject.IsSpawned)
            {
                if (networkManager != null && networkManager.IsServerStarted)
                {
                    networkManager.ServerManager.Despawn(networkObject);
                    Debug.Log($"[GameplayBootstrap] Despawned {systemName}");
                }
            }
            else
            {
                systemObject.SetActive(false);
                Debug.Log($"[GameplayBootstrap] Deactivated {systemName}");
            }
        }

        /// <summary>
        /// Cleanup systems
        /// </summary>
        public void Cleanup()
        {
            if (!isInitialized) return;

            Debug.Log("[GameplayBootstrap] Cleaning up gameplay systems...");

            if (eventBus != null)
            {
                eventBus.ClearAllSubscribers();
            }

            isInitialized = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        public bool IsInitialized => isInitialized;

        public void GetSystemReferences(
            out MatchPhaseManager matchPhase,
            out ScoringSystem scoring,
            out PredatorPreySystem predatorPrey,
            out ZoneSystem zone,
            out AntiCampingSystem antiCamping,
            out VisionSystem vision)
        {
            matchPhase = matchPhaseManager;
            scoring = scoringSystem;
            predatorPrey = predatorPreySystem;
            zone = zoneSystem;
            antiCamping = antiCampingSystem;
            vision = visionSystem;
        }

        // Server-only systems
        public PlayerSpawnSystem SpawnSystem => spawnSystem;
        public TeamSystem TeamSystem => teamSystem;
        public ObjectiveSystem ObjectiveSystem => objectiveSystem;
    }
}
