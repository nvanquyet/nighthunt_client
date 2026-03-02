using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Scene-scoped generic singleton base class.
    ///
    /// The instance is destroyed when the scene unloads (standard MonoBehaviour lifetime).
    /// Use this for managers that live inside one specific scene and should NOT survive
    /// scene transitions.
    ///
    /// Examples (scene = 05_Game):
    ///   SpectateManager, GameplayEventBus, GameBootstrap
    ///
    /// Usage:
    /// <code>
    /// public class MyManager : Singleton&lt;MyManager&gt;
    /// {
    ///     protected override void OnSingletonAwake() { /* init */ }
    /// }
    /// </code>
    /// Then access via: MyManager.Instance
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        /// <summary>Returns true if a live instance exists in the current scene.</summary>
        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance found — destroying {gameObject.name}.");
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            OnSingletonAwake();
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Called once after this object is confirmed as the singleton instance.
        /// Override instead of Awake() in subclasses.
        /// </summary>
        protected virtual void OnSingletonAwake() { }
    }
}
