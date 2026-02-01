using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using NightHunt.Gameplay.Core;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.PredatorPrey;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.AI;
using NightHunt.Gameplay.AntiCamping;
using NightHunt.Gameplay.Vision;
using NightHunt.Gameplay.ClientEffects;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Editor.Gameplay
{
    /// <summary>
    /// Editor tool để tự động setup gameplay scene
    /// </summary>
    public class GameplaySceneSetupTool : EditorWindow
    {
        [MenuItem("Night Hunt/Setup/Create Gameplay Test Scene")]
        public static void CreateGameplayTestScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // Remove default camera (we'll add Cinemachine camera)
            var defaultCamera = Camera.main;
            if (defaultCamera != null)
            {
                DestroyImmediate(defaultCamera.gameObject);
            }

            // Create all required GameObjects
            CreateNetworkManager();
            CreateGameplayBootstrap();
            CreateMatchPhaseManager();
            CreateScoringSystem();
            CreatePredatorPreySystem();
            CreateZoneSystem();
            CreateAntiCampingSystem();
            CreateVisionSystem();
            CreateInputLayerManager();
            CreateGameplayEventBus();
            CreateClientEffectManager();
            CreateMainCamera();
            CreateSpawnPoints();

            // Save scene
            string scenePath = "Assets/Scenes/TestGameplayScene.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[GameplaySceneSetupTool] Test scene created at {scenePath}");
            EditorUtility.DisplayDialog("Success", $"Test scene created at {scenePath}", "OK");
        }

        private static void CreateNetworkManager()
        {
            GameObject go = new GameObject("NetworkManager");
            // Note: NetworkManager component sẽ được add manually vì cần FishNet setup
            Debug.Log("[GameplaySceneSetupTool] Created NetworkManager GameObject");
        }

        private static void CreateGameplayBootstrap()
        {
            GameObject go = new GameObject("GameplayBootstrap");
            go.AddComponent<GameplayBootstrap>();
            Debug.Log("[GameplaySceneSetupTool] Created GameplayBootstrap");
        }

        private static void CreateMatchPhaseManager()
        {
            GameObject go = new GameObject("MatchPhaseManager");
            go.AddComponent<MatchPhaseManager>();
            Debug.Log("[GameplaySceneSetupTool] Created MatchPhaseManager");
        }

        private static void CreateScoringSystem()
        {
            GameObject go = new GameObject("ScoringSystem");
            go.AddComponent<ScoringSystem>();
            Debug.Log("[GameplaySceneSetupTool] Created ScoringSystem");
        }

        private static void CreatePredatorPreySystem()
        {
            GameObject go = new GameObject("PredatorPreySystem");
            go.AddComponent<PredatorPreySystem>();
            go.AddComponent<RevealSystem>();
            go.AddComponent<RadarPingSystem>();
            Debug.Log("[GameplaySceneSetupTool] Created PredatorPreySystem");
        }

        private static void CreateZoneSystem()
        {
            GameObject go = new GameObject("ZoneSystem");
            go.AddComponent<ZoneSystem>();
            Debug.Log("[GameplaySceneSetupTool] Created ZoneSystem");
        }


        private static void CreateAntiCampingSystem()
        {
            GameObject go = new GameObject("AntiCampingSystem");
            go.AddComponent<AntiCampingSystem>();
            Debug.Log("[GameplaySceneSetupTool] Created AntiCampingSystem");
        }

        private static void CreateVisionSystem()
        {
            GameObject go = new GameObject("VisionSystem");
            go.AddComponent<VisionSystem>();
            Debug.Log("[GameplaySceneSetupTool] Created VisionSystem");
        }

        private static void CreateInputLayerManager()
        {
            GameObject go = new GameObject("InputLayerManager");
            go.AddComponent<InputLayerManager>();
            Debug.Log("[GameplaySceneSetupTool] Created InputLayerManager");
        }

        private static void CreateGameplayEventBus()
        {
            GameObject go = new GameObject("GameplayEventBus");
            go.AddComponent<GameplayEventBus>();
            Debug.Log("[GameplaySceneSetupTool] Created GameplayEventBus");
        }

        private static void CreateClientEffectManager()
        {
            GameObject go = new GameObject("ClientEffectManager");
            go.AddComponent<ClientEffectManager>();
            Debug.Log("[GameplaySceneSetupTool] Created ClientEffectManager");
        }

        private static void CreateMainCamera()
        {
            GameObject go = new GameObject("MainCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            // Note: CinemachineBrain sẽ được add manually sau khi install Cinemachine
            Debug.Log("[GameplaySceneSetupTool] Created MainCamera");
        }

        private static void CreateSpawnPoints()
        {
            for (int i = 1; i <= 4; i++)
            {
                GameObject spawnPoint = new GameObject($"SpawnPoint_{i}");
                spawnPoint.transform.position = new Vector3(i * 5f, 0f, 0f);
                spawnPoint.tag = "SpawnPoint";
                Debug.Log($"[GameplaySceneSetupTool] Created SpawnPoint_{i}");
            }
        }
    }
}

