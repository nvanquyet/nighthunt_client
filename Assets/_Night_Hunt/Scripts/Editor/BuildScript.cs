using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace NightHunt.Editor
{
    /// <summary>
    /// Build scripts for automated builds - DISABLED
    /// Headless server build functionality has been disabled.
    /// This script remains for backward compatibility but headless server build methods are no-ops.
    /// </summary>
    public class BuildScript
    {
        /// <summary>
        /// Build headless server - DISABLED
        /// Headless server functionality has been disabled.
        /// </summary>
        [MenuItem("Build/Build Headless Server (Linux)")]
        public static void BuildHeadlessServer()
        {
            EditorUtility.DisplayDialog("Headless Server Disabled", 
                "Headless server build functionality has been disabled.\n\nPlease use client build options instead.", 
                "OK");
            Debug.LogWarning("Headless server build disabled - use client build options instead");
        }

        /// <summary>
        /// Build headless server with version - DISABLED
        /// </summary>
        public static void BuildHeadlessServerWithVersion(string version)
        {
            Debug.LogWarning($"Headless server build disabled - version {version} ignored");
        }

        /// <summary>
        /// Clean build directory
        /// </summary>
        [MenuItem("Build/Clean Build Directory")]
        public static void CleanBuildDirectory()
        {
            const string BUILD_OUTPUT_DIR = "Builds";
            if (Directory.Exists(BUILD_OUTPUT_DIR))
            {
                if (EditorUtility.DisplayDialog("Clean Build Directory", 
                    $"Are you sure you want to delete:\n{BUILD_OUTPUT_DIR}?", 
                    "Yes", "No"))
                {
                    Directory.Delete(BUILD_OUTPUT_DIR, true);
                    Debug.Log("Build directory cleaned");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Clean Build Directory", 
                    "Build directory does not exist.", 
                    "OK");
            }
        }
    }
}
