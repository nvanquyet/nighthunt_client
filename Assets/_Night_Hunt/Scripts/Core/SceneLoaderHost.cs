using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Minimal persistent MonoBehaviour used by the static SceneLoader to run coroutines.
    /// Auto-created on first access. Survives all scene loads (DontDestroyOnLoad).
    /// Never place this in a scene — it is self-managed.
    /// </summary>
    internal sealed class SceneLoaderHost : MonoBehaviour
    {
        private static SceneLoaderHost _instance;

        internal static SceneLoaderHost Instance
        {
            get
            {
                if (_instance == null)
                    Create();
                return _instance;
            }
        }

        private static void Create()
        {
            var go = new GameObject("[SceneLoaderHost]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneLoaderHost>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
