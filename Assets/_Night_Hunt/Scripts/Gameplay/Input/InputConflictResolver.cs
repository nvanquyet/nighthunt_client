using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Resolves conflicts between action maps
    /// </summary>
    public static class InputConflictResolver
    {
        /// <summary>
        /// Check if two action maps have conflicting bindings
        /// </summary>
        public static bool HasConflicts(InputActionMapController map1, InputActionMapController map2)
        {
            // This is a simplified check - in a real implementation, you'd compare actual bindings
            // For now, we rely on the priority system in InputLayerManager
            return false;
        }

        /// <summary>
        /// Resolve conflicts by disabling lower priority map
        /// </summary>
        public static void ResolveConflicts(Dictionary<string, InputActionMapController> maps, string priorityMapName)
        {
            if (!maps.ContainsKey(priorityMapName))
            {
                Debug.LogWarning($"[InputConflictResolver] Priority map '{priorityMapName}' not found");
                return;
            }

            // Disable all other maps when priority map is enabled
            foreach (var kvp in maps)
            {
                if (kvp.Key != priorityMapName && kvp.Value.IsEnabled)
                {
                    kvp.Value.Disable();
                }
            }
        }
    }
}

