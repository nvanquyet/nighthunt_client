using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Automated build script dành cho CI/CD pipeline (GitHub Actions).
/// Chạy bằng command line:
///   Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildDedicatedServer -quit
/// </summary>
public static class BuildScript
{
    // Output path - GitHub Actions sẽ COPY folder này vào Docker context
    private const string DS_BUILD_PATH = "Builds/DedicatedServer";

    // Scenes dùng cho Dedicated Server (không có UI, chỉ có network + gameplay)
    private static readonly string[] SERVER_SCENES =
    {
        "Assets/_Night_Hunt/Scenes/99_Dedicated_Server.unity",
    };

    /// <summary>
    /// Entry point cho CI/CD - build Dedicated Server Linux x64
    /// </summary>
    public static void BuildDedicatedServer()
    {
        Debug.Log("╔══════════════════════════════════════════╗");
        Debug.Log("║  NightHunt - Dedicated Server Build      ║");
        Debug.Log("╚══════════════════════════════════════════╝");

        // Validate scenes
        foreach (var scene in SERVER_SCENES)
        {
            if (!File.Exists(scene))
            {
                Debug.LogError($"[BuildScript] Scene not found: {scene}");
                EditorApplication.Exit(1);
                return;
            }
        }

        // Đảm bảo output folder tồn tại
        if (Directory.Exists(DS_BUILD_PATH))
            Directory.Delete(DS_BUILD_PATH, recursive: true);
        Directory.CreateDirectory(DS_BUILD_PATH);

        var options = new BuildPlayerOptions
        {
            scenes            = SERVER_SCENES,
            locationPathName  = Path.Combine(DS_BUILD_PATH, "NightHuntServer"),
            target            = BuildTarget.StandaloneLinux64,
            // Dedicated Server mode: không có graphics, không có audio
            subtarget         = (int)StandaloneBuildSubtarget.Server,
            options           = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            long sizeMB = (long)summary.totalSize / 1024 / 1024;
            Debug.Log($"[BuildScript] ✅ Build succeeded!");
            Debug.Log($"[BuildScript]    Output : {DS_BUILD_PATH}");
            Debug.Log($"[BuildScript]    Size   : {sizeMB} MB");
            Debug.Log($"[BuildScript]    Time   : {summary.totalTime}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[BuildScript] ❌ Build FAILED: {summary.result}");
            Debug.LogError($"[BuildScript]    Errors  : {summary.totalErrors}");
            Debug.LogError($"[BuildScript]    Warnings: {summary.totalWarnings}");

            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"  → {msg.content}");
                }
            }

            EditorApplication.Exit(1);
        }
    }
}
