using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Controls what happens when a second instance of a singleton is found.
    /// </summary>
    public enum SingletonDuplicatePolicy
    {
        /// <summary>
        /// Destroy the NEW (incoming) duplicate — keep the existing instance.
        /// Default behavior. Use for most scene-scoped singletons.
        /// </summary>
        DestroyNew,

        /// <summary>
        /// Destroy the OLD (existing) instance and promote the new one.
        /// Use when the new scene's version should always win
        /// (e.g. a persistent singleton that needs fresh scene-specific state).
        /// </summary>
        DestroyOld,
    }

    /// <summary>
    /// Abstract base for generic singleton patterns.
    /// 
    /// Handles all common singleton logic:
    /// - Duplicate instance detection (configurable: DestroyNew vs DestroyOld)
    /// - Static instance storage
    /// - Instance getter with lazy loading
    /// - OnDestroy cleanup
    /// 
    /// Subclasses (Singleton&lt;T&gt;, SingletonPersistent&lt;T&gt;) only override MakePersistent()
    /// to define whether DontDestroyOnLoad is called.
    ///
    /// To change policy, override <see cref="DuplicatePolicy"/> in your subclass:
    /// <code>
    /// protected override SingletonDuplicatePolicy DuplicatePolicy
    ///     => SingletonDuplicatePolicy.DestroyOld;
    /// </code>
    /// </summary>
    public abstract class SingletonBase<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        [Header("Singleton")]
        [Tooltip("How to resolve a duplicate instance at runtime.\n" +
                 "DestroyNew (default): keep the existing instance, destroy this one.\n" +
                 "DestroyOld: destroy the existing instance and promote this one.")]
        [SerializeField] private SingletonDuplicatePolicy _duplicatePolicy = SingletonDuplicatePolicy.DestroyNew;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                    if (_instance == null && typeof(T).Name == "GameplayEventBus")
                    {
                        GameObject go = new GameObject("GameplayEventBus (Auto-Created)");
                        _instance = go.AddComponent<T>();
                        Debug.Log($"[SingletonBase] Auto-created missing singleton instance for {typeof(T).Name}");
                    }
                }
                return _instance;
            }
        }

        /// <summary>Returns true if a live instance exists.</summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// Defines what to do when a duplicate instance is detected.
        /// Reads from the [SerializeField] _duplicatePolicy set in the Inspector (default: DestroyNew).
        /// Can still be overridden in a subclass to hard-code policy regardless of Inspector value.
        /// </summary>
        protected virtual SingletonDuplicatePolicy DuplicatePolicy => _duplicatePolicy;

        protected virtual void Awake()
        {
            // Duplicate detection — use Unity's == null which properly detects
            // destroyed objects. A scene-scoped Singleton's static _instance may
            // still hold a C# reference to a destroyed object after scene unload.
            // Unity's == null returns true for destroyed objects; C# != null does NOT.
            bool instanceIsDestroyedOrNull = _instance == null || !_instance;
            if (!instanceIsDestroyedOrNull && _instance != this)
            {
                if (DuplicatePolicy == SingletonDuplicatePolicy.DestroyOld)
                {
                    // Keep the newest instance — destroy the old one.
                    Debug.LogWarning(
                        $"[{typeof(T).Name}] Duplicate found — destroying OLD '{_instance.gameObject.name}', " +
                        $"keeping NEW '{gameObject.name}'.");
                    var old = _instance.gameObject;
                    _instance = null;
                    Destroy(old);
                    // fall through to register this instance below
                }
                else // DestroyNew (default)
                {
                    Debug.LogWarning(
                        $"[{typeof(T).Name}] Duplicate found — destroying NEW component on '{gameObject.name}', " +
                        $"keeping existing '{_instance.gameObject.name}'. " +
                        "(Only the component is destroyed to avoid collateral damage on shared GameObjects.)");
                    Destroy(this);
                    return;
                }
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
        /// SingletonPersistent&lt;T&gt; overrides to call DontDestroyOnLoad.
        /// </summary>
        protected virtual void MakePersistent() { }

        /// <summary>
        /// Called once after this object is confirmed as the singleton instance.
        /// Override instead of Awake() in subclasses.
        /// </summary>
        protected virtual void OnSingletonAwake() { }
    }
}
