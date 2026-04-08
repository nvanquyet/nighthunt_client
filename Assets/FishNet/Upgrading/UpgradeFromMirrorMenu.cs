#if UNITY_EDITOR
using FishNet.Documenting;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FishNet.Upgrading.Mirror.Editing
{
    /* IMPORTANT IMPORTANT IMPORTANT IMPORTANT
     * If you receive errors about missing Mirror components,
     * such as NetworkIdentity, then remove MIRROR and any other
     * MIRROR defines.
     * Project Settings -> Player -> Other -> Scripting Define Symbols.
     *
     * If you are also using my assets add FGG_ASSETS to the defines, and
     * then remove it after running this script. */
    [APIExclude]
    public class UpgradeFromMirrorMenu : MonoBehaviour
    {
        /// <summary>
        /// Replaces all components.
        /// </summary>
        [MenuItem("Tools/Fish-Networking/Utility/Upgrading/From Mirror/Replace Components", false, 1)]
        private static void ReplaceComponents()
        {
#if MIRROR
#if UNITY_2023_2_OR_NEWER
            MirrorUpgrade result = FindFirstObjectByType<MirrorUpgrade>();
#else
            MirrorUpgrade result = GameObject.FindObjectOfType<MirrorUpgrade>();
#endif
            if (result != null)
            {
                Debug.LogError("MirrorUpgrade already exist in the scene. This suggests an operation is currently running.");
                return;
            }

            GameObject iteratorGo = new GameObject();
            iteratorGo.AddComponent<MirrorUpgrade>();
#else
            Debug.LogError("Mirror must be imported to perform this function.");
#endif
        }

        [MenuItem("Tools/Fish-Networking/Utility/Upgrading/From Mirror/Remove Defines", false, 2)]
        private static void RemoveDefines()
        {
            // Use NamedBuildTarget API where available to avoid obsolete warnings.
            BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            /* Convert current defines into a hashset. This is so we can
             * determine if any of our defines were added. Only save playersettings
             * when a define is added. */
            HashSet<string> definesHs = new();
            string[] currentArr = currentDefines.Split(';');

            bool removed = false;
            // Add any define which doesn't contain MIRROR.
            foreach (string item in currentArr)
            {
                string itemLower = item.ToLower();
                if (itemLower != "mirror" && !itemLower.StartsWith("mirror_"))
                    definesHs.Add(item);
                else
                    removed = true;
            }

            if (removed)
            {
                Debug.Log("Removed Mirror defines to player settings.");
                string changedDefines = string.Join(";", definesHs);
                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, changedDefines);
            }
        }
    }
}
#endif