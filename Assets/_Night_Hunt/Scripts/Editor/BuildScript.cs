#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace NightHunt.Build
{
    /// <summary>
    /// BuildScript — Unity command-line build automation.
    ///
    /// Dùng với -executeMethod khi build qua CLI hoặc CI/CD:
    ///
    ///   Dedicated Server (Linux):
    ///     Unity.exe -quit -batchmode -nographics
    ///       -projectPath "path/to/NightHuntClient"
    ///       -buildTarget LinuxHeadless
    ///       -executeMethod NightHunt.Build.BuildScript.BuildDedicatedServer
    ///       -logFile build_ds.log
    ///
    ///   Client (Windows):
    ///     Unity.exe -quit -batchmode
    ///       -projectPath "path/to/NightHuntClient"
    ///       -buildTarget Win64
    ///       -executeMethod NightHunt.Build.BuildScript.BuildClient
    ///       -logFile build_client.log
    ///
    ///   Client (Android):
    ///     Unity.exe -quit -batchmode
    ///       -projectPath "path/to/NightHuntClient"
    ///       -buildTarget Android
    ///       -executeMethod NightHunt.Build.BuildScript.BuildClientAndroid
    ///       -logFile build_android.log
    /// </summary>
    public static class BuildScript
    {
        // ── Scene lists ───────────────────────────────────────────────────────

        /// <summary>Scenes cần thiết cho Dedicated Server (headless, không có UI).</summary>
        private static readonly string[] DsScenes =
        {
            "Assets/Scenes/01_Bootstrap.unity",
            "Assets/Scenes/05_Gameplay.unity",
            "Assets/Scenes/Maps/GameMap_01.unity",
            "Assets/Scenes/Maps/GameMap_02.unity",
            // Thêm maps mới ở đây khi có
        };

        /// <summary>Scenes đầy đủ cho Client build.</summary>
        private static readonly string[] ClientScenes =
        {
            "Assets/Scenes/01_Bootstrap.unity",
            "Assets/Scenes/02_Home.unity",
            "Assets/Scenes/03_CharacterSelect.unity",
            "Assets/Scenes/04_Lobby.unity",
            "Assets/Scenes/06_MatchLoading.unity",
            "Assets/Scenes/05_Gameplay.unity",
            "Assets/Scenes/Maps/GameMap_01.unity",
            "Assets/Scenes/Maps/GameMap_02.unity",
            // Thêm maps mới ở đây khi có
        };

        // ── Build targets ─────────────────────────────────────────────────────

        /// <summary>Build Dedicated Server cho Linux x86_64 (Docker container).</summary>
        public static void BuildDedicatedServer()
        {
            string outputPath = GetArgOrDefault("-buildOutput", "Build/Server/NightHuntDS");

            var options = new BuildPlayerOptions
            {
                scenes           = DsScenes,
                locationPathName = outputPath,
                target           = BuildTarget.StandaloneLinux64,
                subtarget        = (int)StandaloneBuildSubtarget.Server,
                options          = BuildOptions.None,
            };

            ExecuteBuild(options, "Dedicated Server Linux");
        }

        /// <summary>Build Client cho Windows x64.</summary>
        public static void BuildClient()
        {
            string outputPath = GetArgOrDefault("-buildOutput", "Build/Client/NightHuntClient.exe");

            var options = new BuildPlayerOptions
            {
                scenes           = ClientScenes,
                locationPathName = outputPath,
                target           = BuildTarget.StandaloneWindows64,
                options          = BuildOptions.None,
            };

            ExecuteBuild(options, "Client Windows64");
        }

        /// <summary>Build Client cho macOS (Universal).</summary>
        public static void BuildClientMac()
        {
            string outputPath = GetArgOrDefault("-buildOutput", "Build/Client/NightHuntClient.app");

            var options = new BuildPlayerOptions
            {
                scenes           = ClientScenes,
                locationPathName = outputPath,
                target           = BuildTarget.StandaloneOSX,
                options          = BuildOptions.None,
            };

            ExecuteBuild(options, "Client macOS");
        }

        /// <summary>Build Client cho Android (.apk).</summary>
        public static void BuildClientAndroid()
        {
            string outputPath = GetArgOrDefault("-buildOutput", "Build/Client/NightHuntClient.apk");

            // Android keystore config (từ env vars hoặc arg)
            string keystorePath = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH") ?? "";
            string keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS") ?? "";
            string keyAlias     = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS")     ?? "";
            string keyPass      = Environment.GetEnvironmentVariable("ANDROID_KEY_PASS")      ?? "";

            if (!string.IsNullOrEmpty(keystorePath))
            {
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = keyAlias;
                PlayerSettings.Android.keyaliasPass = keyPass;
            }

            var options = new BuildPlayerOptions
            {
                scenes           = ClientScenes,
                locationPathName = outputPath,
                target           = BuildTarget.Android,
                options          = BuildOptions.None,
            };

            ExecuteBuild(options, "Client Android");
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private static void ExecuteBuild(BuildPlayerOptions options, string buildName)
        {
            UnityEngine.Debug.Log($"[BuildScript] Starting {buildName} build → {options.locationPathName}");

            BuildReport  report  = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log($"[BuildScript] ✅ {buildName} build succeeded " +
                                      $"({summary.totalSize / 1024 / 1024} MB, {summary.totalTime.TotalSeconds:F1}s)");
            }
            else
            {
                string msg = $"[BuildScript] ❌ {buildName} build FAILED: {summary.result} " +
                             $"(errors: {summary.totalErrors}, warnings: {summary.totalWarnings})";
                UnityEngine.Debug.LogError(msg);
                throw new Exception(msg);
            }
        }

        /// <summary>Đọc custom arg từ command line: -argName value</summary>
        private static string GetArgOrDefault(string argName, string defaultValue)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return defaultValue;
        }
    }
}
#endif
