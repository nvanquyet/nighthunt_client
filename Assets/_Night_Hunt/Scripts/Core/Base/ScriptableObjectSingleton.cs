using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Generic singleton base class for ScriptableObjects.
    ///
    /// HOW IT WORKS:
    ///   1. OnEnable() fires when Unity loads the asset (from prefab/scene reference) — free, zero cost.
    ///   2. If not pre-loaded, Instance getter uses _cachedPath (set via context menu) or falls back.
    ///   3. Right-click the asset → "Cache Resources Path" to store the path in the static cache
    ///      for the current Editor/runtime session (no serialization, no field conflicts).
    ///
    /// LOAD ORDER (first match wins, all subsequent calls are O(1)):
    ///   1. Pre-loaded via scene/prefab reference → OnEnable sets _instance.  FREE.
    ///   2. _cachedPath (set via context menu this session) → Resources.Load. FAST.
    ///   3. typeof(T).Name → Resources.Load at root of Resources/.            FAST.
    ///   4. Resources.LoadAll scan (ONE time) → caches path, warns.           SLOW (once only).
    ///
    /// SETUP (one-time per Editor session):
    ///   - Place the .asset anywhere inside a Resources/ subfolder.
    ///   - Right-click the asset → "Cache Resources Path".
    ///   - After domain reload, run it once again (it is not persisted — by design, to avoid
    ///     the Unity serialization duplicate-field bug with generic base classes).
    ///
    /// USAGE:
    /// <code>
    /// [CreateAssetMenu(fileName = "MyDatabase", menuName = "NightHunt/My Database")]
    /// public class MyDatabase : ScriptableObjectSingleton&lt;MyDatabase&gt;
    /// {
    ///     protected override void OnSingletonEnabled() { /* optional init */ }
    /// }
    /// MyDatabase.Instance.DoSomething();
    /// </code>
    /// </summary>
    public abstract class ScriptableObjectSingleton<T> : ScriptableObject
        where T : ScriptableObjectSingleton<T>
    {
        // NOT [SerializeField] — putting a serialized field in any class that is part of a generic
        // inheritance chain causes Unity to serialize it multiple times (duplicate-field error).
        // _cachedPath is a static, lives for the duration of the domain session, and is re-populated
        // by OnEnable (when a scene/prefab references the asset) or by the context menu.
        private static T      _instance;
        private static string _cachedPath;

        public static T Instance
        {
            get
            {
                if (_instance != null) return _instance;

                // ── Cached path from context menu or previous load ──────────────
                if (!string.IsNullOrEmpty(_cachedPath))
                    _instance = Resources.Load<T>(_cachedPath);

                // ── Root-level name (e.g. Resources/ItemDatabase.asset) ─────────
                if (_instance == null)
                    _instance = Resources.Load<T>(typeof(T).Name);

                // ── One-time LoadAll scan — runs once, then caches ──────────────
                if (_instance == null)
                {
                    T[] all = Resources.LoadAll<T>("");
                    if (all != null && all.Length > 0)
                    {
                        _instance = all[0];
                        if (all.Length > 1)
                            Debug.LogWarning($"[{typeof(T).Name}] Multiple assets found via LoadAll. " +
                                             $"Using '{_instance.name}'. There should be exactly one.");

                        Debug.LogWarning($"[{typeof(T).Name}] Used Resources.LoadAll (slow — runs once per session). " +
                                         "Right-click the asset → 'Cache Resources Path' to avoid this.");
                    }
                }

                if (_instance == null)
                    Debug.LogError($"[{typeof(T).Name}] Instance not found. " +
                                   "Place the asset inside a Resources/ folder.");

                return _instance;
            }
        }

        /// <summary>Returns true if the instance is already loaded without triggering Resources.Load.</summary>
        public static bool HasInstance => _instance != null;

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Multiple instances loaded — " +
                                 $"keeping '{_instance.name}', ignoring '{name}'. " +
                                 $"There should be exactly one {typeof(T).Name}.asset in the project.");
                return;
            }

            _instance = (T)this;
            OnSingletonEnabled();
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                _instance = null;
                OnSingletonDisabled();
            }
        }

        /// <summary>Called after this asset is registered as singleton (OnEnable). Override for init logic.</summary>
        protected virtual void OnSingletonEnabled() { }

        /// <summary>Called when this instance is unloaded (OnDisable). Override to clean up cached state.</summary>
        protected virtual void OnSingletonDisabled() { }

#if UNITY_EDITOR
        /// <summary>
        /// Right-click the asset in Project → "Cache Resources Path".
        /// Extracts the Resources-relative path and stores it in the static _cachedPath so the
        /// Instance getter uses Resources.Load directly (no LoadAll scan) for this session.
        /// Not persisted to disk — run once per Editor session, or after domain reload.
        /// </summary>
        [ContextMenu("Cache Resources Path")]
        private void CacheResourcesPath()
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            const string marker = "Resources/";
            int idx = assetPath.IndexOf(marker, System.StringComparison.Ordinal);

            if (idx < 0)
            {
                Debug.LogError($"[{GetType().Name}] '{assetPath}' is not inside a Resources/ folder. " +
                               "Move the asset into a Resources/ folder first.");
                return;
            }

            string relative = assetPath.Substring(idx + marker.Length);
            if (relative.EndsWith(".asset"))
                relative = relative.Substring(0, relative.Length - ".asset".Length);

            _cachedPath = relative;
            Debug.Log($"[{GetType().Name}] Resources path cached as: \"{_cachedPath}\" (session-only).");
        }
#endif
    }
}
