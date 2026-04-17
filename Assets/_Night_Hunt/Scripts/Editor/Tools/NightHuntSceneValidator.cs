using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// NightHuntSceneValidator — editor + CI scene validation tool.
    ///
    /// Menu: NightHunt / Validate / All Scenes
    ///       NightHunt / Validate / Active Scene
    ///
    /// Also runs automatically before every build via IPreprocessBuildWithReport.
    /// Fails the build if any required component is missing from its expected scene.
    ///
    /// Scene rules:
    ///   00_DS_Boot  — requires NetworkManager, ServerBootstrap
    ///              — must NOT have AudioListener or Camera (headless)
    ///   01_Home     — requires GameManager, MatchFlowCoordinator, MatchLoadingOverlay
    ///   02_Map_01   — requires GameBootstrap, ServerGameManager, ScoringSystem,
    ///                  MatchPhaseManager, MatchEndManager, SpawnSystem, RespawnSystem
    /// </summary>
    public static class NightHuntSceneValidator
    {
        // ── Scene paths ───────────────────────────────────────────────────────

        private const string SceneBoot    = "Assets/_Night_Hunt/Scenes/00_DS_Boot.unity";
        private const string SceneHome    = "Assets/_Night_Hunt/Scenes/01_Home.unity";
        private const string SceneMap01   = "Assets/_Night_Hunt/Scenes/02_Map_01.unity";
        private const string DuplicateMap = "Assets/_Night_Hunt/Scenes/02_Map_01 1.unity";

        // ── Scene rules ───────────────────────────────────────────────────────

        private static readonly SceneRule[] Rules = new[]
        {
            new SceneRule(SceneBoot,
                required: new[]
                {
                    "FishNet.Managing.NetworkManager",
                    "NightHunt.Server.ServerBootstrap",   // namespace is NightHunt.Server (not Networking)
                },
                forbidden: new[]
                {
                    "UnityEngine.AudioListener",
                    "UnityEngine.Camera",
                }),

            new SceneRule(SceneHome,
                required: new[]
                {
                    "NightHunt.Core.GameManager",
                    "NightHunt.UI.MatchFlowCoordinator",
                    "NightHunt.UI.MatchLoadingOverlay",
                },
                forbidden: null),

            new SceneRule(SceneMap01,
                required: new[]
                {
                    "NightHunt.Gameplay.Core.GameBootstrap",
                    "NightHunt.Networking.ServerGameManager",       // was NightHunt.Gameplay.Match
                    "NightHunt.Gameplay.Scoring.ScoringSystem",
                    "NightHunt.Gameplay.Match.MatchPhaseManager",
                    "NightHunt.Gameplay.Match.MatchEndManager",
                    "NightHunt.Gameplay.Spawn.SpawnSystem",         // was NightHunt.Gameplay.Spawning
                    "NightHunt.Gameplay.Respawn.RespawnSystem",     // was NightHunt.Gameplay.Spawning
                },
                forbidden: null),
        };

        // ── Menu items ────────────────────────────────────────────────────────

        [MenuItem("NightHunt/Validate/All Scenes")]
        public static void ValidateAllScenes()
        {
            bool passed = RunAllValidations(logToConsole: true);
            if (passed)
                Debug.Log("[NightHuntSceneValidator] All scenes passed validation.");
            else
                Debug.LogError("[NightHuntSceneValidator] Validation FAILED — see errors above.");
        }

        [MenuItem("NightHunt/Validate/Active Scene")]
        public static void ValidateActiveScene()
        {
            string activeScenePath = SceneManager.GetActiveScene().path;
            foreach (var rule in Rules)
            {
                if (rule.ScenePath == activeScenePath)
                {
                    bool ok = ValidateOpenScene(rule, logToConsole: true);
                    if (ok) Debug.Log("[NightHuntSceneValidator] Active scene passed validation.");
                    return;
                }
            }
            Debug.Log($"[NightHuntSceneValidator] Active scene '{activeScenePath}' has no validation rules.");
        }

        // ── Core validation ───────────────────────────────────────────────────

        private static bool RunAllValidations(bool logToConsole)
        {
            bool allPassed = true;

            // Check for duplicate scene file.
            if (System.IO.File.Exists(DuplicateMap))
            {
                if (logToConsole)
                    Debug.LogError($"[NightHuntSceneValidator] Duplicate scene found: '{DuplicateMap}' — delete it via Project window.");
                allPassed = false;
            }

            // Save any pending changes before opening scenes.
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                bool saved = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (!saved) return false;
            }

            string originalScene = EditorSceneManager.GetActiveScene().path;

            foreach (var rule in Rules)
            {
                bool ok = ValidateSceneByPath(rule, logToConsole);
                if (!ok) allPassed = false;
            }

            // Re-open original scene if we moved away.
            if (!string.IsNullOrEmpty(originalScene)
                && EditorSceneManager.GetActiveScene().path != originalScene)
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }

            return allPassed;
        }

        /// <summary>Validate a scene by opening it (if not already open).</summary>
        private static bool ValidateSceneByPath(SceneRule rule, bool logToConsole)
        {
            bool alreadyOpen = EditorSceneManager.GetActiveScene().path == rule.ScenePath;
            Scene scene;

            if (alreadyOpen)
            {
                scene = EditorSceneManager.GetActiveScene();
            }
            else
            {
                if (!System.IO.File.Exists(rule.ScenePath))
                {
                    if (logToConsole)
                        Debug.LogError($"[NightHuntSceneValidator] Scene not found: '{rule.ScenePath}'");
                    return false;
                }
                scene = EditorSceneManager.OpenScene(rule.ScenePath, OpenSceneMode.Additive);
            }

            bool ok = ValidateOpenScene(rule, logToConsole, scene);

            if (!alreadyOpen)
                EditorSceneManager.CloseScene(scene, removeScene: true);

            return ok;
        }

        private static bool ValidateOpenScene(SceneRule rule, bool logToConsole, Scene? explicitScene = null)
        {
            Scene scene = explicitScene ?? SceneManager.GetActiveScene();
            string sceneName = System.IO.Path.GetFileName(rule.ScenePath);
            bool ok = true;

            // Collect all components in scene.
            var allComponents = new HashSet<string>();
            foreach (var go in scene.GetRootGameObjects())
                CollectComponentTypes(go, allComponents);

            // Required check.
            if (rule.Required != null)
            {
                foreach (var type in rule.Required)
                {
                    if (!allComponents.Contains(type))
                    {
                        if (logToConsole)
                            Debug.LogError($"[NightHuntSceneValidator] [{sceneName}] Missing required component: {type}");
                        ok = false;
                    }
                }
            }

            // Forbidden check.
            if (rule.Forbidden != null)
            {
                foreach (var type in rule.Forbidden)
                {
                    if (allComponents.Contains(type))
                    {
                        if (logToConsole)
                            Debug.LogError($"[NightHuntSceneValidator] [{sceneName}] Forbidden component present: {type}");
                        ok = false;
                    }
                }
            }

            if (ok && logToConsole)
                Debug.Log($"[NightHuntSceneValidator] [{sceneName}] OK");

            return ok;
        }

        private static void CollectComponentTypes(GameObject go, HashSet<string> types)
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null)
                    types.Add(comp.GetType().FullName);
            }
            for (int i = 0; i < go.transform.childCount; i++)
                CollectComponentTypes(go.transform.GetChild(i).gameObject, types);
        }

        // ── Build preprocessor ────────────────────────────────────────────────

        private class BuildPreprocessor : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                bool passed = RunAllValidations(logToConsole: true);
                if (!passed)
                    throw new BuildFailedException("[NightHuntSceneValidator] Build aborted: scene validation failed. Fix errors above before building.");
            }
        }

        // ── Types ─────────────────────────────────────────────────────────────

        private readonly struct SceneRule
        {
            public readonly string   ScenePath;
            public readonly string[] Required;
            public readonly string[] Forbidden;

            public SceneRule(string scenePath, string[] required, string[] forbidden)
            {
                ScenePath = scenePath;
                Required  = required;
                Forbidden = forbidden;
            }
        }
    }
}
