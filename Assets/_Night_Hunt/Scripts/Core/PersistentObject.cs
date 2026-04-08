using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// ⚠️ DEPRECATED — Use SingletonPersistent&lt;T&gt; instead!
    /// 
    /// This class uses a non-generic shared static instance, which causes cross-class
    /// singleton collisions (e.g., PersistentUICanvas gets destroyed if GameManager inits first).
    /// 
    /// MIGRATION PATH:
    ///   OLD:  public class MyService : PersistentObject { ... }
    ///   NEW:  public class MyService : SingletonPersistent&lt;MyService&gt; { ... }
    ///   
    ///   Replace OnPersistentAwake() with OnSingletonAwake()
    ///   Replace PersistentObject.Instance with MyService.Instance
    /// 
    /// WHY?
    ///   - Generic base (SingletonPersistent&lt;T&gt;) uses separate instance field per type
    ///   - No collision between different services
    ///   - Proper duplicate detection
    ///   - Same functionality, zero duplication
    /// 
    /// KEEP THIS FILE?
    ///   - Only if you have custom logic not covered by the generic base
    ///   - Mark [Obsolete] in your custom subclass
    ///   - Plan migration for next refactor sprint
    /// </summary>
    [System.Obsolete("Use SingletonPersistent<T> instead. This class has non-generic shared static instance causing cross-class collisions.")]
    public class PersistentObject : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Nếu true, sẽ destroy duplicate instances")]
        [SerializeField] private bool destroyDuplicates = true;

        [Tooltip("Nếu true, sẽ tự động tạo instance nếu chưa có (chỉ cho singleton)")]
#pragma warning disable CS0414
        [SerializeField] private bool autoCreateIfMissing = false;
#pragma warning restore CS0414

        private static PersistentObject instance;

        /// <summary>
        /// ⚠️ DEPRECATED — Use SingletonPersistent&lt;T&gt;.Instance instead
        /// Instance của PersistentObject (nếu là singleton)
        /// </summary>
        public static PersistentObject Instance => instance;

        /// <summary>
        /// True nếu object này là instance chính
        /// </summary>
        public bool IsInstance => instance == this;

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                OnPersistentAwake();
            }
            else if (destroyDuplicates)
            {
                // Nếu đã có instance và destroyDuplicates = true, destroy duplicate
                Destroy(gameObject);
            }
            else
            {
                // Nếu không destroy duplicates, chỉ đảm bảo DontDestroyOnLoad
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// ⚠️ DEPRECATED — Override OnSingletonAwake() in SingletonPersistent&lt;T&gt; instead
        /// Được gọi khi object được set làm persistent (chỉ instance chính)
        /// Override để thực hiện initialization
        /// </summary>
        protected virtual void OnPersistentAwake()
        {
            // Override trong derived classes
        }

        /// <summary>
        /// Tạo instance nếu chưa có (chỉ hoạt động nếu autoCreateIfMissing = true)
        /// </summary>
        public static T GetOrCreateInstance<T>() where T : PersistentObject
        {
            if (instance == null)
            {
                GameObject go = new GameObject($"{typeof(T).Name}");
                T component = go.AddComponent<T>();
                return component;
            }
            return instance as T;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}

