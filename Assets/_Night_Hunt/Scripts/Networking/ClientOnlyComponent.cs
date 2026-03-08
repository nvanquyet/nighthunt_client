using UnityEngine;
using FishNet.Object;

namespace NightHunt.Networking
{
    /// <summary>
    /// Base class for client-only components
    /// Automatically disables on server to ensure client-only logic
    /// </summary>
    public abstract class ClientOnlyComponent : NetworkBehaviour
    {
        protected virtual void Awake()
        {
            // In Awake, FishNet NetworkBehaviour properties like IsServer are not yet
            // initialized (object not spawned). Use InstanceFinder for reliable detection.
            // Disable ONLY on dedicated server (server started, client NOT started).
            // On host (both server+client started) the client side must run.
            bool isDedicatedServer = FishNet.InstanceFinder.IsServerStarted
                                     && !FishNet.InstanceFinder.IsClientStarted;
            if (isDedicatedServer)
            {
                enabled = false;
                return;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            InitializeClientComponent();
        }

        /// <summary>
        /// Initialize client component
        /// Override this in derived classes
        /// </summary>
        protected virtual void InitializeClientComponent()
        {
            // Override in derived classes
        }

        protected virtual void Update()
        {
            // Only run on client
            if (IsServer)
            {
                return;
            }
        }
    }
}

