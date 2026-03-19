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
    public abstract class SingletonPersistent<T> : SingletonBase<T> where T : MonoBehaviour
    {
        /// <summary>
        /// Override MakePersistent to call DontDestroyOnLoad.
        /// This is the only difference between Singleton<T> and SingletonPersistent<T>.
        /// </summary>
        protected override void MakePersistent()
        {
            DontDestroyOnLoad(transform.root.gameObject);
        }
    }
}
