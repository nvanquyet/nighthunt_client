using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// DontDestroyOnLoad generic singleton base class.
    ///
    /// The instance survives all scene loads and is destroyed only when the app quits.
    /// Use this for truly cross-scene services that ALL scenes may need.
    ///
    /// Persistent systems (examples):
    ///   GameManager, SessionState, RoomState,
    ///   PersistentUICanvas, ToastService, UINotificationService
    ///
    /// Usage:
    /// <code>
    /// public class MyService : SingletonPersistent&lt;MyService&gt;
    /// {
    ///     protected override void OnSingletonAwake() { /* init */ }
    /// }
    /// </code>
    /// Then access via: MyService.Instance
    /// </summary>
    public abstract class SingletonPersistent<T> : MonoBehaviour where T : MonoBehaviour
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

        /// <summary>Returns true if a live persistent instance exists.</summary>
        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate persistent instance found — destroying {gameObject.name}.");
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
            OnSingletonAwake();
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Called once after this object is confirmed as the persistent singleton instance.
        /// Override instead of Awake() in subclasses.
        /// </summary>
        protected virtual void OnSingletonAwake() { }
    }
}
