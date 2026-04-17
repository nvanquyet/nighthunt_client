using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// OBSOLETE — Logic đã migrated into <see cref="NetworkGameManager"/>.
    ///
    /// NetworkGameManager tự subscribe SceneManager.OnLoadEnd và auto-connect
    /// khi game map scene (02_Map_XX) load xong. Script này no longer needed.
    ///
    /// Cách handle:
    ///   1. Remove component này khỏi mọi scene / prefab trong Unity Editor.
    ///   2. Remove file MatchNetworkConnector.cs.
    /// </summary>
    [System.Obsolete("Migrated into NetworkGameManager. Remove this component from all scenes.")]
    public sealed class MatchNetworkConnector : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[MatchNetworkConnector] OBSOLETE \u2014 Remove this component from the scene. " +
                             "Auto-connect is now handled by NetworkGameManager.");
        }
    }
}
