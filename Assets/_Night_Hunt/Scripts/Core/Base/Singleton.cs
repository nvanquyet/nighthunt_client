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
    public abstract class Singleton<T> : SingletonBase<T> where T : MonoBehaviour
    {
        // No override needed — MakePersistent() in base does nothing
        // which is exactly what we want for scene-scoped singletons
    }
}
