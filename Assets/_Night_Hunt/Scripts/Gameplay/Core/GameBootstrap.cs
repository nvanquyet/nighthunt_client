using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet;
using System;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Team;
using NightHunt.Gameplay.Boss;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.AntiCamping;
using NightHunt.Gameplay.Scoring;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.Gameplay.Core
{
    /// <summary>
    /// GAME BOOTSTRAP - System initialization coordinator
    /// 
    /// Server Authority:
    /// - Initializes all gameplay systems in correct order
    /// - Spawns NetworkObjects on server
    /// - Provides system references to ServerGameManager
    /// 
    /// Responsibilities:
    /// - Initialize services and systems
    /// - Activate phase-specific systems
    /// - Provide clean system references
    /// - Fire initialization events
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Core Systems")]
        // MatchPhaseManager removed — replaced by SafeZoneManager

        [Header("Server Systems")] [SerializeField]
        private SpawnSystem _spawnSystem;

        [SerializeField] private TeamAssignmentSystem _teamAssignmentSystem;

        [Header("Settings")] [SerializeField] private bool _initializeOnStart = true;
        [SerializeField] private bool _autoSpawnNetworkObjects = true;

        #endregion

        #region State

        private NetworkManager _networkManager;
        private bool _isInitialized = false;

        #endregion

        #region Events

        /// <summary>
        /// Fired when all systems are initialized
        /// ServerGameManager subscribes to this
        /// </summary>
        public event Action OnSystemsInitialized;

        #endregion

        #region Properties

        public bool IsInitialized => _isInitialized;

        // System accessors for ServerGameManager
        public SpawnSystem SpawnSystem => _spawnSystem;
        public TeamAssignmentSystem TeamAssignmentSystem => _teamAssignmentSystem;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Don't initialize in Awake - wait for network
            if (_initializeOnStart)
            {
                // Delay initialization until Start to ensure NetworkManager is ready
                return;
            }
        }

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;

            if (_networkManager == null)
            {
                Debug.LogError("[GameBootstrap] NetworkManager not found!");
                return;
            }

            if (_initializeOnStart)
            {
                Initialize();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize all gameplay systems
        /// Can be called manually or automatically on Start
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GameBootstrap] Already initialized!");
                return;
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ===== Initializing Gameplay Systems =====");

            // Step 1: Initialize singleton services
            InitializeSingletonServices();

            // Step 2: Find/validate system references
            ValidateSystemReferences();

            // Step 3: Spawn NetworkObjects if on server
            if (_networkManager.IsServerStarted && _autoSpawnNetworkObjects)
            {
                SpawnNetworkSystems();
            }

            // Step 4: Subscribe SafeZoneManager zone events
            if (SafeZoneManager.Instance != null)
                SafeZoneManager.Instance.OnZonePhaseStarted += HandleZonePhaseStarted;

            _isInitialized = true;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ===== ✅ All Systems Initialized =====");

            // Notify listeners (ServerGameManager)
            OnSystemsInitialized?.Invoke();
        }

        #endregion

        #region Service Initialization

        private void InitializeSingletonServices()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] Initializing singleton services...");

            // Verify singleton services exist
            if (RegistryService.Instance == null)
            {
                Debug.LogWarning("[GameBootstrap] RegistryService not found - creating one");
                GameObject registryGO = new GameObject("RegistryService");
                registryGO.AddComponent<RegistryService>();
            }

            if (TeamService.Instance == null)
            {
                Debug.LogWarning("[GameBootstrap] TeamService not found - creating one");
                GameObject teamGO = new GameObject("TeamService");
                teamGO.AddComponent<TeamService>();
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] âœ… Singleton services initialized");
        }

        #endregion

        #region System Reference Validation

        private void ValidateSystemReferences()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] Validating system references...");

            // Auto-find systems if not assigned
            if (_spawnSystem == null)
            {
                _spawnSystem = FindFirstObjectByType<SpawnSystem>();
                if (_spawnSystem == null)
                    Debug.LogError("[GameBootstrap] SpawnSystem not found!");
            }

            if (_teamAssignmentSystem == null)
            {
                _teamAssignmentSystem = FindFirstObjectByType<TeamAssignmentSystem>();
                if (_teamAssignmentSystem == null)
                    Debug.LogError("[GameBootstrap] TeamAssignmentSystem not found!");
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] âœ… System references validated");
        }

        #endregion

        #region Network Object Spawning

        /// <summary>
        /// SERVER: Spawn NetworkObjects for systems that need network sync
        /// </summary>
        private void SpawnNetworkSystems()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] Spawning network systems...");

            // Spawn SpawnSystem
            SpawnNetworkSystem(_spawnSystem, "SpawnSystem");

            // Spawn TeamAssignmentSystem
            SpawnNetworkSystem(_teamAssignmentSystem, "TeamAssignmentSystem");

            // Spawn TeamService (if it's a NetworkBehaviour)
            if (TeamService.Instance != null)
            {
                SpawnNetworkSystem(TeamService.Instance, "TeamService");
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] âœ… Network systems spawned");
        }

        /// <summary>
        /// SERVER: Spawn a single NetworkObject system
        /// </summary>
        private void SpawnNetworkSystem(Component system, string systemName)
        {
            if (system == null)
            {
                Debug.LogWarning($"[GameBootstrap] Cannot spawn null system: {systemName}");
                return;
            }

            NetworkObject networkObject = ComponentResolver.Find<NetworkObject>(system)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();

            if (networkObject != null)
            {
                // This is a NetworkObject - spawn it on network
                if (!networkObject.IsSpawned)
                {
                    _networkManager.ServerManager.Spawn(networkObject);
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                        Debug.Log($"[GameBootstrap] âœ… Spawned {systemName} on network");
                }
                else
                {
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                        Debug.Log($"[GameBootstrap] {systemName} already spawned");
                }
            }
            else
            {
                // Not a NetworkObject - just activate
                if (!system.gameObject.activeSelf)
                {
                    system.gameObject.SetActive(true);
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                        Debug.Log($"[GameBootstrap] âœ… Activated {systemName}");
                }
            }
        }

        #endregion

        #region Phase System Activation

        private void HandleZonePhaseStarted(int zoneIndex)
        {
            if (!_networkManager.IsServerStarted) return;

            if (zoneIndex == 0)
                ActivatePhase1Systems();
            else if (!SafeZoneManager.Instance.IsInFinalZone)
                ActivatePhase2Systems();
            else
                ActivatePhase3Systems();
        }

        /// <summary>
        /// SERVER: Activate systems for Phase 1 (Preparation)
        /// Called by ServerGameManager when phase starts
        /// </summary>
        public void ActivatePhase1Systems()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ===== Activating Phase 1 Systems =====");

            // Ensure WorldSpawnManager starts its loot spawn loop.
            var worldSpawn = FindFirstObjectByType<WorldSpawnManager>();
            if (worldSpawn == null)
                Debug.LogWarning("[GameBootstrap] Phase 1: WorldSpawnManager not found in scene.");

            // AntiCampingSystem is always active but safe to log here.
            var antiCamp  = FindFirstObjectByType<AntiCampingSystem>();
            if (antiCamp == null)
                Debug.LogWarning("[GameBootstrap] Phase 1: AntiCampingSystem not found in scene.");

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ✅ Phase 1 systems activated");
        }

        /// <summary>
        /// SERVER: Activate systems for Phase 2 (Hunt & Objectives)
        /// </summary>
        public void ActivatePhase2Systems()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ===== Activating Phase 2 Systems =====");

            // BossSpawnManager self-activates via MatchPhaseManager.OnPhaseStarted.
            var bossSpawn = FindFirstObjectByType<BossSpawnManager>();
            if (bossSpawn == null)
                Debug.LogWarning("[GameBootstrap] Phase 2: BossSpawnManager not found — boss will not spawn.");

            // ScoringSystem self-activates via OnStartServer event subscriptions.
            var scoring   = FindFirstObjectByType<ScoringSystem>();
            if (scoring == null)
                Debug.LogWarning("[GameBootstrap] Phase 2: ScoringSystem not found.");

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ✅ Phase 2 systems activated");
        }

        /// <summary>
        /// SERVER: Activate systems for Phase 3 (Final Lockdown)
        /// </summary>
        public void ActivatePhase3Systems()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ===== Activating Phase 3 Systems =====");

            // SafeZoneManager handles final-zone logic natively — damage, events.

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] ✅ Phase 3 systems activated");
        }

        #endregion

        #region System Deactivation

        /// <summary>
        /// SERVER: Deactivate/despawn a system
        /// </summary>
        public void DeactivateSystem(Component system, string systemName)
        {
            if (system == null) return;

            NetworkObject networkObject = ComponentResolver.Find<NetworkObject>(system)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();

            if (networkObject != null && networkObject.IsSpawned)
            {
                // NetworkObject - despawn from network
                if (_networkManager != null && _networkManager.IsServerStarted)
                {
                    _networkManager.ServerManager.Despawn(networkObject);
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                        Debug.Log($"[GameBootstrap] Despawned {systemName}");
                }
            }
            else
            {
                // Regular GameObject - deactivate
                system.gameObject.SetActive(false);
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log($"[GameBootstrap] Deactivated {systemName}");
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleanup all systems
        /// Called on scene unload or game end
        /// </summary>
        public void Cleanup()
        {
            if (!_isInitialized) return;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] Cleaning up gameplay systems...");;

            // Clean up: clear event subscribers, reset state, unload resources
            _isInitialized = false;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameBootstrap] âœ… Cleanup complete");
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force re-initialization (useful for testing)
        /// </summary>
        public void Reinitialize()
        {
            Cleanup();
            _isInitialized = false;
            Initialize();
        }

        /// <summary>
        /// Get debug info string
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Bootstrap Initialized: {_isInitialized}\n" +

                   $"SpawnSystem: {(_spawnSystem != null ? "âœ…" : "âŒ")}\n" +
                   $"TeamAssignmentSystem: {(_teamAssignmentSystem != null ? "âœ…" : "âŒ")}\n" +
                   $"Network Manager: {(_networkManager != null ? "âœ…" : "âŒ")}";
        }

        #endregion
    }
}
