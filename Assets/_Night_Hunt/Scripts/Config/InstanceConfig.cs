using UnityEngine;
using NightHunt.Utils;

namespace NightHunt.Config
{
    // ══════════════════════════════════════════════════════════════════════════
    // InstanceConfig — static class. No .asset file, no ScriptableObject.
    // Multi-instance (ParrelSync) support is auto-detected: enabled in Editor,
    // disabled in Build. No manual flags needed.
    // ══════════════════════════════════════════════════════════════════════════

    public static class InstanceConfig
    {
        // Auto: enabled in Editor (ParrelSync), disabled in production Build
        public static bool   IsMultiInstanceEnabled()       => Application.isEditor;

        public static int    GetInstanceId()                => Application.isEditor ? InstanceHelper.GetInstanceId() : 0;
        public static string GetInstanceKey(string baseKey) => Application.isEditor ? InstanceHelper.GetInstanceKey(baseKey) : baseKey;

        // Run in background only in Editor (multi-instance testing)
        public static bool   ShouldRunInBackground()        => Application.isEditor;

        // Always refresh data when regaining focus
        public static bool   ShouldRefreshOnFocusReturn()   => true;
    }
}

