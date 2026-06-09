using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Automated build script dành cho CI/CD pipeline (GitHub Actions).
///
/// DS build:
///   Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildDedicatedServer -quit
///
/// Client build:
///   Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildClient -quit
/// </summary>
public static class BuildScript
{
    // ── Dedicated Server ──────────────────────────────────────────────────────
    private const string DS_BUILD_PATH = "Builds/DedicatedServer";

    // 00_DS_Boot = dedicated DS boot scene (NetworkManager DDOL + ServerBootstrap only).
    // DS boots into 00_DS_Boot → ServerBootstrap parses --mapId → LoadGlobalScenes(map).
    // NetworkManager DontDestroyOnLoad=1 survives into map scene.
    // Map scene has its own NM → FishNet destroys duplicate (DestroyNewest) → boot NM used.
    private static readonly string[] SERVER_SCENES =
    {
        "Assets/_Night_Hunt/Scenes/00_DS_Boot.unity",  // index 0 — DS boot only
        "Assets/_Night_Hunt/Scenes/02_Map_01.unity",   // index 1 — Map 01
        "Assets/_Night_Hunt/Scenes/02_Map_02.unity",   // index 2 — Map 02
        // Thêm map mới: thêm dòng ở đây + entry trong ServerBootstrap.LoadGameScene()
    };

    // ── Client ────────────────────────────────────────────────────────────────
    private const string CLIENT_BUILD_PATH = "Builds/Client";

    // Client không có DS boot scene. Chỉ cần:
    // 1. Các scene phía trước gameplay (login, lobby)
    // 2. Các map gameplay để FishNet SceneManager có thể load khi DS notify
    private static readonly string[] CLIENT_SCENES =
    {
        "Assets/_Night_Hunt/Scenes/01_Home.unity",          // index 0 — boot, login, lobby
        "Assets/_Night_Hunt/Scenes/02_Map_01.unity",        // index 1 — Map 01 (FishNet load khi join match)
        "Assets/_Night_Hunt/Scenes/02_Map_02.unity",        // index 2 — Map 02
        // Thêm map mới: thêm ở cả SERVER_SCENES lẫn CLIENT_SCENES
    };

    // ── Build: Dedicated Server ───────────────────────────────────────────────

    /// <summary>
    /// CI/CD entry point — Linux x64 Dedicated Server (headless, no GPU/audio).
    /// Build subtarget = Server → UNITY_SERVER define được set tự động.
    /// ClientOnlyGameObject + RenderDisabler sẽ disable toàn bộ client-only objects.
    /// </summary>
    public static void BuildDedicatedServer()
    {
        Debug.Log("╔══════════════════════════════════════════╗");
        Debug.Log("║  NightHunt — Dedicated Server Build      ║");
        Debug.Log("╚══════════════════════════════════════════╝");
        RunBuild(SERVER_SCENES, DS_BUILD_PATH, "NightHuntDS",
                 BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server);
    }

    // ── Build: Client ─────────────────────────────────────────────────────────

    /// <summary>
    /// CI/CD entry point — Windows x64 Client build.
    /// Thay BuildTarget.StandaloneWindows64 bằng Android/iOS nếu cần.
    /// </summary>
    public static void BuildClient()
    {
        Debug.Log("╔══════════════════════════════════════════╗");
        Debug.Log("║  NightHunt — Client Build                ║");
        Debug.Log("╚══════════════════════════════════════════╝");
        RunBuild(CLIENT_SCENES, CLIENT_BUILD_PATH, "NightHuntClient",
                 BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player);
    }

    // ── Shared build runner ───────────────────────────────────────────────────

    private static void RunBuild(string[] scenes, string outputPath, string exeName,
                                 BuildTarget target, StandaloneBuildSubtarget subtarget)
    {
        foreach (var scene in scenes)
        {
            if (!File.Exists(scene))
            {
                Debug.LogError($"[BuildScript] Scene not found: {scene}");
                EditorApplication.Exit(1);
                return;
            }
        }

        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, recursive: true);
        Directory.CreateDirectory(outputPath);

        string ext = target == BuildTarget.StandaloneLinux64 ? "" : ".exe";
        var options = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = Path.Combine(outputPath, exeName + ext),
            target           = target,
            subtarget        = (int)subtarget,
            options          = BuildOptions.None,
        };

        BuildReport  report  = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            long sizeMB = (long)summary.totalSize / 1024 / 1024;
            Debug.Log($"[BuildScript] Build succeeded! Output={outputPath}  Size={sizeMB}MB  Time={summary.totalTime}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BuildScript] Build FAILED: {summary.result}  Errors={summary.totalErrors}");
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"  → {msg.content}");
            EditorApplication.Exit(1);
        }
    }
}
