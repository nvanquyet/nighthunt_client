using UnityEngine;
using FishNet.Managing;
using FishNet.Managing.Scened;

namespace NightHunt.Networking
{
    /// <summary>
    /// Bootstrap script for dedicated server
    /// Automatically starts server when running as dedicated server build
    /// </summary>
    public class DedicatedServerBootstrap : MonoBehaviour
    {
        [Header("Server Settings")]
        [SerializeField] private ushort serverPort = 7777;
        [SerializeField] private int maxPlayers = 20;
        [SerializeField] private string gameSceneName = "05_Game";

        [Header("Auto Start")]
        [SerializeField] private bool autoStartOnAwake = true;

        private NetworkManager networkManager;
        private bool serverStarted = false;

        private void Awake()
        {
            // Only run on dedicated server builds
            #if UNITY_SERVER || UNITY_EDITOR
            networkManager = FindObjectOfType<NetworkManager>();
            
            if (networkManager == null)
            {
                Debug.LogError("[DedicatedServerBootstrap] NetworkManager not found!");
                return;
            }

            if (autoStartOnAwake)
            {
                StartDedicatedServer();
            }
            #else
            // Not a server build, disable this component
            enabled = false;
            #endif
        }

        /// <summary>
        /// Start dedicated server
        /// </summary>
        public void StartDedicatedServer()
        {
            if (serverStarted) return;

            #if UNITY_SERVER || UNITY_EDITOR
            Debug.Log($"[DedicatedServerBootstrap] Starting dedicated server on port {serverPort}...");

            // Start server
            if (networkManager.ServerManager.StartConnection())
            {
                serverStarted = true;
                Debug.Log("[DedicatedServerBootstrap] Dedicated server started successfully!");

                // Load game scene
                LoadGameScene();
            }
            else
            {
                Debug.LogError("[DedicatedServerBootstrap] Failed to start dedicated server!");
            }
            #endif
        }

        /// <summary>
        /// Load game scene on server (single scene, not additive)
        /// Unloads all other scenes first, then loads new scene
        /// </summary>
        private void LoadGameScene()
        {
            if (networkManager == null || !networkManager.IsServerStarted) return;

            // Get current active scene
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            // If we're already in the game scene, don't reload
            if (currentSceneName == gameSceneName)
            {
                Debug.Log($"[DedicatedServerBootstrap] Already in game scene: {gameSceneName}");
                return;
            }

            Debug.Log($"[DedicatedServerBootstrap] Loading game scene: {gameSceneName} (unloading all other scenes)");

            // Unload ALL loaded scenes except the game scene
            // This ensures we only have one scene active (single mode)
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.name != gameSceneName && scene.isLoaded)
                {
                    Debug.Log($"[DedicatedServerBootstrap] Unloading scene: {scene.name}");
                    SceneUnloadData sud = new SceneUnloadData(scene.name);
                    networkManager.SceneManager.UnloadConnectionScenes(sud);
                }
            }

            // Wait a frame for unload to complete, then load new scene
            StartCoroutine(LoadGameSceneAfterUnload());
        }

        private System.Collections.IEnumerator LoadGameSceneAfterUnload()
        {
            // Wait for unload to complete
            yield return new WaitForSeconds(0.1f);

            // Load new scene as single (replace current scene), not additive
            SceneLoadData sld = new SceneLoadData(gameSceneName);
            networkManager.SceneManager.LoadConnectionScenes(sld);
        }

        /// <summary>
        /// Stop dedicated server
        /// </summary>
        public void StopDedicatedServer()
        {
            if (!serverStarted) return;

            if (networkManager != null && networkManager.IsServerStarted)
            {
                networkManager.ServerManager.StopConnection(true);
                serverStarted = false;
                Debug.Log("[DedicatedServerBootstrap] Dedicated server stopped");
            }
        }

        private void OnApplicationQuit()
        {
            StopDedicatedServer();
        }
    }
}

