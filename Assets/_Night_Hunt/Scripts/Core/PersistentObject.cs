using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Generic script để tạo object DontDestroyOnLoad
    /// Sử dụng Singleton pattern để đảm bảo chỉ có 1 instance
    /// </summary>
    public class PersistentObject : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Nếu true, sẽ destroy duplicate instances")]
        [SerializeField] private bool destroyDuplicates = true;

        [Tooltip("Nếu true, sẽ tự động tạo instance nếu chưa có (chỉ cho singleton)")]
        [SerializeField] private bool autoCreateIfMissing = false;

        private static PersistentObject instance;

        /// <summary>
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

