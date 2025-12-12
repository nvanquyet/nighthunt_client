using System.Threading.Tasks;
using _Night_Hunt.Scripts.NightHuntInput;
using FishNet.Managing;
using NightHunt.Core.Config;
using NightHunt.Core.Utilities;
using NightHunt.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Night_Hunt.Core.Bootstrap
{
    /// <summary>
    /// Application entry point - initializes all core systems
    /// Ensures proper initialization order and dependency injection
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
        
        private static bool isInitialized = false;

        private void Awake()
        {
            if (isInitialized)
            {
                Destroy(gameObject);
                return;
            }

            isInitialized = true;
            DontDestroyOnLoad(gameObject);
            
            InitializeSystems();
        }

        private async void InitializeSystems()
        {
            Debug.Log("[Bootstrap] Starting Night Hunt initialization...");

            // 1. Initialize Service Locator first
            ServiceLocator.Initialize();
            
            // 2. Load game configuration
            await LoadConfigurationAsync();
            
            // 3. Initialize core systems in order
            InitializeInputSystem();
            InitializeNetworkSystem();
            InitializeAudioSystem();
            InitializePoolSystem();
            
            // 4. Load main menu
            await LoadMainMenuAsync();
            
            Debug.Log("[Bootstrap] Initialization complete!");
        }

        private async Task LoadConfigurationAsync()
        {
            var configLoader = new ConfigLoader();
            GameData gameData = await configLoader.LoadAsync("NightHunt_Full_GameDesign_Config_v3");
            
            ServiceLocator.Register(gameData);
            ServiceLocator.Register(gameConfig);
            
            Debug.Log($"[Bootstrap] Loaded {gameData.WeaponConfig.Count} weapons, {gameData.ItemConfig.Count} items");
        }

        private void InitializeInputSystem()
        {
            var inputManager = new GameObject("InputManager").AddComponent<InputManager>();
            inputManager.transform.SetParent(transform);
            ServiceLocator.Register(inputManager);
            
            Debug.Log("[Bootstrap] Input System initialized");
        }

        private void InitializeNetworkSystem() 
        {
            var networkManager = new GameObject("NetworkManager").AddComponent<NetworkManager>();
            networkManager.transform.SetParent(transform);
            ServiceLocator.Register(networkManager);
            
            Debug.Log("[Bootstrap] Network System initialized");
        }

        private void InitializeAudioSystem()
        {
            // var audioManager = new GameObject("AudioManager").AddComponent<AudioManager>();
            // audioManager.transform.SetParent(transform);
            // ServiceLocator.Register(audioManager);
            //
            // Debug.Log("[Bootstrap] Audio System initialized");
        }

        private void InitializePoolSystem()
        {
            // var poolManager = new GameObject("PoolManager").AddComponent<PoolManager>();
            // poolManager.transform.SetParent(transform);
            // ServiceLocator.Register(poolManager);
            //
            // Debug.Log("[Bootstrap] Pool System initialized");
        }

        private async Task LoadMainMenuAsync()
        {
            var loadOp = SceneManager.LoadSceneAsync("MainMenu");
            while (!loadOp.isDone)
            {
                await Task.Yield();
            }
        }
    }

    
   
}