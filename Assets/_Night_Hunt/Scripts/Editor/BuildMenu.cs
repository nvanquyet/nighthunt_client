using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System;
using NightHunt.Config;

namespace NightHunt.Editor
{
    /// <summary>
    /// Build menu for Unity Editor - Build Client only
    /// Headless server build functionality has been disabled.
    /// </summary>
    public class BuildMenu
    {
        // Paths
        private const string BUILD_CONFIG_PATH = "Assets/_Night_Hunt/Config/BuildConfig.asset";
        private const string CLIENT_BUILD_DIR = "Builds/Client";
        
        // Scenes (sử dụng SceneConfig hoặc default paths với prefix)
        private const string FIRST_LOADING_SCENE = "Assets/_Night_Hunt/Scenes/01_FirstLoading.unity";
        private const string LOGIN_SCENE = "Assets/_Night_Hunt/Scenes/02_Login.unity";
        private const string HOME_SCENE = "Assets/_Night_Hunt/Scenes/03_Home.unity";
        private const string WAITING_SCENE = "Assets/_Night_Hunt/Scenes/04_Waiting.unity";
        private const string GAME_SCENE = "Assets/_Night_Hunt/Scenes/05_Game.unity";

        #region Build Client

        [MenuItem("Build/Client/Build Client (Windows)", false, 1)]
        public static void BuildClientWindows()
        {
            BuildClient(BuildTarget.StandaloneWindows64, "Windows");
        }

        [MenuItem("Build/Client/Build Client (Mac)", false, 2)]
        public static void BuildClientMac()
        {
            BuildClient(BuildTarget.StandaloneOSX, "Mac");
        }

        [MenuItem("Build/Client/Build Client (Linux)", false, 3)]
        public static void BuildClientLinux()
        {
            BuildClient(BuildTarget.StandaloneLinux64, "Linux");
        }

        private static void BuildClient(BuildTarget target, string platformName)
        {
            Debug.Log($"Building Client for {platformName}...");

            // Get build config
            var config = GetBuildConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Build Error", 
                    "Build config not found! Please create one via:\nBuild > Setup > Create Build Config", 
                    "OK");
                return;
            }

            // Validate scenes
            string[] scenes = GetClientScenes();
            if (!ValidateScenes(scenes))
            {
                return;
            }

            // Build path
            string buildName = GetBuildName(target, config.buildName);
            string buildPath = Path.Combine(CLIENT_BUILD_DIR, platformName, buildName);
            string buildDirectory = Path.GetDirectoryName(buildPath);

            if (!Directory.Exists(buildDirectory))
            {
                Directory.CreateDirectory(buildDirectory);
            }

            // Build options
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = target,
                options = GetBuildOptions(config, false)
            };

            // Build
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            HandleBuildResult(report, buildPath, $"Client ({platformName})");
        }

        #endregion

        #region Setup & Config

        [MenuItem("Build/Setup/Create Build Config", false, 20)]
        public static void CreateBuildConfig()
        {
            string directory = Path.GetDirectoryName(BUILD_CONFIG_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = ScriptableObject.CreateInstance<BuildConfig>();
            AssetDatabase.CreateAsset(config, BUILD_CONFIG_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            
            EditorUtility.DisplayDialog("Build Config Created", 
                $"Build config created at:\n{BUILD_CONFIG_PATH}\n\nSelect it in Project window to configure.", 
                "OK");
        }

        [MenuItem("Build/Setup/Open Build Config", false, 21)]
        public static void OpenBuildConfig()
        {
            var config = GetBuildConfig();
            if (config != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = config;
            }
            else
            {
                if (EditorUtility.DisplayDialog("Build Config Not Found", 
                    "Build config not found. Create one now?", 
                    "Yes", "No"))
                {
                    CreateBuildConfig();
                }
            }
        }

        [MenuItem("Build/Setup/Open Client Setup Guide", false, 22)]
        public static void OpenClientSetupGuide()
        {
            string path = "Assets/_Night_Hunt/Documentation/CLIENT_SETUP.md";
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start(path);
            }
            else
            {
                EditorUtility.DisplayDialog("File Not Found", 
                    $"Setup guide not found at:\n{path}", 
                    "OK");
            }
        }

        #endregion

        #region Helper Methods

        private static BuildConfig GetBuildConfig()
        {
            return AssetDatabase.LoadAssetAtPath<BuildConfig>(BUILD_CONFIG_PATH);
        }

        private static string[] GetClientScenes()
        {
            // Try to get scenes from SceneConfig, fallback to default paths
            string[] scenes = GetScenesFromConfig();
            if (scenes != null && scenes.Length > 0)
            {
                return scenes;
            }
            
            // Fallback to default scene paths
            return new[]
            {
                FIRST_LOADING_SCENE,
                LOGIN_SCENE,
                HOME_SCENE,
                WAITING_SCENE,
                GAME_SCENE
            };
        }
        
        /// <summary>
        /// Get scene paths from SceneConfig
        /// </summary>
        private static string[] GetScenesFromConfig()
        {
            #if UNITY_EDITOR
            // Try to find SceneConfig
            string[] guids = AssetDatabase.FindAssets("t:SceneConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var config = AssetDatabase.LoadAssetAtPath<SceneConfig>(path);
                if (config != null)
                {
                    // Build scene paths from SceneConfig
                    string basePath = "Assets/_Night_Hunt/Scenes/";
                    return new[]
                    {
                        basePath + config.firstLoadingScene + ".unity",
                        basePath + config.loginScene + ".unity",
                        basePath + config.homeScene + ".unity",
                        basePath + config.waitingScene + ".unity",
                        basePath + config.gameScene + ".unity"
                    };
                }
            }
            #endif
            return null;
        }

        private static bool ValidateScenes(string[] scenes)
        {
            foreach (var scene in scenes)
            {
                if (!File.Exists(scene))
                {
                    EditorUtility.DisplayDialog("Build Error", 
                        $"Scene not found:\n{scene}\n\nPlease create the scene first.", 
                        "OK");
                    return false;
                }
            }
            return true;
        }

        private static string GetBuildName(BuildTarget target, string baseName)
        {
            string extension = target switch
            {
                BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.StandaloneOSX => ".app",
                BuildTarget.StandaloneLinux64 => ".x86_64",
                _ => ""
            };
            
            return string.IsNullOrEmpty(baseName) ? $"NightHuntClient{extension}" : $"{baseName}{extension}";
        }

        private static BuildOptions GetBuildOptions(BuildConfig config, bool isHeadless)
        {
            BuildOptions options = BuildOptions.None;

            if (config.developmentBuild)
            {
                options |= BuildOptions.Development;
            }

            if (config.allowDebugging)
            {
                options |= BuildOptions.AllowDebugging;
            }

            if (config.scriptDebugging)
            {
                options |= BuildOptions.Development | BuildOptions.AllowDebugging;
            }

            return options;
        }

        private static void HandleBuildResult(BuildReport report, string buildPath, string buildType)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"{buildType} build succeeded!");
                Debug.Log($"Build path: {buildPath}");
                Debug.Log($"Size: {report.summary.totalSize / 1024 / 1024} MB");

                EditorUtility.DisplayDialog("Build Success", 
                    $"{buildType} built successfully!\n\nPath: {buildPath}\nSize: {report.summary.totalSize / 1024 / 1024} MB", 
                    "OK");
            }
            else if (report.summary.result == BuildResult.Failed)
            {
                Debug.LogError($"{buildType} build failed!");
                EditorUtility.DisplayDialog("Build Failed", 
                    $"{buildType} build failed.\n\nCheck Console for details.", 
                    "OK");
            }
        }

        #endregion
    }

    /// <summary>
    /// Build configuration ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfig", menuName = "NightHunt/Build Config", order = 1)]
    public class BuildConfig : ScriptableObject
    {
        [Header("Build Settings")]
        [Tooltip("Base name for build output")]
        public string buildName = "NightHuntClient";

        [Tooltip("Enable development build")]
        public bool developmentBuild = false;

        [Tooltip("Allow debugging")]
        public bool allowDebugging = false;

        [Tooltip("Enable script debugging")]
        public bool scriptDebugging = false;

        [Header("Scene Settings")]
        [Tooltip("First Loading Scene (chứa GameManager và PersistentUICanvas)")]
        public string firstLoadingScene = "Assets/_Night_Hunt/Scenes/01_FirstLoading.unity";

        [Tooltip("Login Scene")]
        public string loginScene = "Assets/_Night_Hunt/Scenes/02_Login.unity";

        [Tooltip("Home Scene")]
        public string homeScene = "Assets/_Night_Hunt/Scenes/03_Home.unity";

        [Tooltip("Waiting Scene (Lobby)")]
        public string waitingScene = "Assets/_Night_Hunt/Scenes/04_Waiting.unity";

        [Tooltip("Game Scene")]
        public string gameScene = "Assets/_Night_Hunt/Scenes/05_Game.unity";
    }
}
