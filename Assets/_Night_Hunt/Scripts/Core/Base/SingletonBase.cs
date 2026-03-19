using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Abstract base for generic singleton patterns.
    /// 
    /// Handles all common singleton logic:
    /// - Duplicate instance detection
    /// - Static instance storage
    /// - Instance getter with lazy loading
    /// - OnDestroy cleanup
    /// 
    /// Subclasses (Singleton<T>, SingletonPersistent<T>) only override MakePersistent()
    /// to define whether DontDestroyOnLoad is called.
    /// </summary>
    public abstract class SingletonBase<T> : MonoBehaviour where T : MonoBehaviour
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

        /// <summary>Returns true if a live instance exists.</summary>
        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            // Duplicate detection
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance found — destroying {gameObject.name}.");
                Destroy(gameObject);
                return;
            }

            // Set as instance
            _instance = this as T;

            // Apply persistence rules (override in subclasses)
            MakePersistent();

            // Call subclass-specific initialization
            OnSingletonAwake();
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Override to implement persistence logic.
        /// Default does nothing (scene-scoped).
        /// SingletonPersistent<T> overrides to call DontDestroyOnLoad.
        /// </summary>
        protected virtual void MakePersistent() { }

        /// <summary>
        /// Called once after this object is confirmed as the singleton instance.
        /// Override instead of Awake() in subclasses.
        /// </summary>
        protected virtual void OnSingletonAwake() { }
    }
}
