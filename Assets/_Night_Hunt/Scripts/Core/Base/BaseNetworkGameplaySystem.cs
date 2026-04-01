using FishNet.Object;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.Core.Base
{
    /// <summary>
    /// Abstract base for all server-authoritative gameplay systems (NetworkBehaviour).
    ///
    /// RESPONSIBILITIES:
    /// - Provides a single, consistent Awake() → ResolveReferences() entry point.
    /// - Provides <see cref="NightHuntDebugConfig"/> for conditional debug logging.
    /// - Delegates reference wiring to <see cref="OnResolveReferences"/> (Template Method).
    /// - Delegates network lifecycle to <see cref="OnNetworkStarted"/> / <see cref="OnNetworkStopped"/>.
    ///
    /// USAGE:
    /// <code>
    /// public class MySystem : BaseNetworkGameplaySystem
    /// {
    ///     [SerializeField] private SomeConcreteType _sourceField; // Inspector drag-drop
    ///     private ISomeInterface _dep;
    ///
    ///     protected override void OnResolveReferences()
    ///     {
    ///         _dep = this.ResolveWithFallback&lt;ISomeInterface&gt;(_sourceField as ISomeInterface,
    ///             "[MySystem] ISomeInterface not found");
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class BaseNetworkGameplaySystem : NetworkBehaviour
    {
        [Header("Debug")]
        [SerializeField] protected NightHuntDebugConfig _debugConfig;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        protected virtual void Awake()
        {
            OnResolveReferences();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            OnNetworkStarted();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            OnNetworkStopped();
        }

        // ── Template methods (override in subclass) ───────────────────────────

        /// <summary>
        /// Wire all component dependencies here (called from Awake).
        /// Use <see cref="NightHunt.Utilities.ComponentResolverExtensions.ResolveWithFallback{T}"/>
        /// for consistent lookup order: Inspector-assigned → OnSelf → InChildren → InParent → InRootChildren.
        /// </summary>
        protected virtual void OnResolveReferences() { }

        /// <summary>Called after <c>base.OnStartNetwork()</c>. Override instead of OnStartNetwork.</summary>
        protected virtual void OnNetworkStarted() { }

        /// <summary>Called after <c>base.OnStopNetwork()</c>. Override instead of OnStopNetwork.</summary>
        protected virtual void OnNetworkStopped() { }

        // ── Logging helpers ───────────────────────────────────────────────────

        /// <summary>Logs a debug message when the relevant debug flag is enabled.</summary>
        protected void LogDebug(bool flag, string message)
        {
            if (flag) Debug.Log(message);
        }

        protected void LogWarning(string message) => Debug.LogWarning(message);
        protected void LogError(string message) => Debug.LogError(message);
    }
}
