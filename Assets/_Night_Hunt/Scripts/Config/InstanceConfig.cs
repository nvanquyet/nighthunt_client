using UnityEngine;
using NightHunt.Utils;

namespace NightHunt.Config
{
    // ══════════════════════════════════════════════════════════════════════════
    // InstanceConfig — static class. No .asset file, no ScriptableObject.
    // Multi-instance support is auto-detected in both Editor and builds.
    // Builds can pass -cloneIndex/--cloneIndex or run from *_CloneN folders.
    // ══════════════════════════════════════════════════════════════════════════

    public static class InstanceConfig
    {
        public static bool   IsMultiInstanceEnabled()       => Application.isEditor || InstanceHelper.GetInstanceId() != 0;

        public static int    GetInstanceId()                => InstanceHelper.GetInstanceId();
        public static string GetInstanceKey(string baseKey) => InstanceHelper.GetInstanceKey(baseKey);

        public static bool   ShouldRunInBackground()        => Application.isEditor || IsMultiInstanceEnabled();

        // Always refresh data when regaining focus
        public static bool   ShouldRefreshOnFocusReturn()   => true;
    }
}

