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
            // Disable on server immediately
            if (IsServer)
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

