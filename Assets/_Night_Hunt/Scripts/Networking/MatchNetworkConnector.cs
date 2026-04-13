using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// OBSOLETE — Logic đã được migrate vào <see cref="NetworkGameManager"/>.
    ///
    /// NetworkGameManager tự subscribe SceneManager.OnLoadEnd và auto-connect
    /// khi game map scene (02_Map_XX) load xong. Script này không còn cần thiết.
    ///
    /// Cách xử lý:
    ///   1. Xóa component này khỏi mọi scene / prefab trong Unity Editor.
    ///   2. Xóa file MatchNetworkConnector.cs.
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
