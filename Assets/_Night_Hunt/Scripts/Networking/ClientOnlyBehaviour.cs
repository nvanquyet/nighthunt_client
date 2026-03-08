using UnityEngine;
using FishNet;
using FishNet.Object;
using FishNet.Managing;

namespace NightHunt.Networking
{
    /// <summary>
    /// Base class for MonoBehaviour UI/client-only scripts that must not run on a
    /// dedicated server.
    ///
    /// Philosophy:
    ///   All UI scripts are MonoBehaviour (NOT NetworkBehaviour) because:
    ///   1. The server build disables every Canvas at startup via <see cref="ServerUISuppressor"/>.
    ///   2. NetworkBehaviour children of a disabled Canvas may Awake() before the
    ///      NetworkObject spawns → IsOwner / IsServer throw NullReferenceException.
    ///   3. UI has no authority – it only reads state from events and SyncVars.
    ///
    /// Inheriting from ClientOnlyBehaviour gives a safe early-exit on server:
    /// <code>
    ///   public class MyPanel : ClientOnlyBehaviour
    ///   {
    ///       protected override void OnClientAwake() { /* safe UI init */ }
    ///   }
    /// </code>
    ///
    /// How server detection works (no NetworkBehaviour dependency):
    ///   Reads <see cref="FishNet.InstanceFinder.IsServerStarted"/> which is safe to
    ///   call from any MonoBehaviour once NetworkManager is in the scene.
    ///   Falls back to <c>false</c> if NetworkManager is not yet ready.
    /// </summary>
    public abstract class ClientOnlyBehaviour : MonoBehaviour
    {
        private bool _isServer;

        private void Awake()
        {
            // InstanceFinder is safe to call without a NetworkBehaviour reference.
            _isServer = InstanceFinder.IsServerStarted && !InstanceFinder.IsClientStarted;

            if (_isServer)
            {
                enabled = false;
                return;
            }

            OnClientAwake();
        }

        private void Start()
        {
            if (_isServer) return;
            OnClientStart();
        }

        private void OnDestroy()
        {
            if (_isServer) return;
            OnClientDestroy();
        }

        // ── Override points ───────────────────────────────────────────────────

        /// <summary>
        /// Called instead of Awake() on client / standalone builds.
        /// Never called on a dedicated server.
        /// </summary>
        protected virtual void OnClientAwake() { }

        /// <summary>
        /// Called instead of Start() on client / standalone builds.
        /// </summary>
        protected virtual void OnClientStart() { }

        /// <summary>
        /// Called instead of OnDestroy() on client / standalone builds.
        /// </summary>
        protected virtual void OnClientDestroy() { }
    }
}
