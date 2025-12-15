using UnityEngine;
using FishNet.Object;

namespace NightHunt.Networking
{
    /// <summary>
    /// Base class for server-only components
    /// Automatically disables on client to ensure server authority
    /// </summary>
    public abstract class ServerOnlyComponent : NetworkBehaviour
    {
        protected virtual void Awake()
        {
            // Disable on client immediately
            if (!IsServer)
            {
                enabled = false;
                return;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeServerComponent();
        }

        /// <summary>
        /// Initialize server component
        /// Override this in derived classes
        /// </summary>
        protected virtual void InitializeServerComponent()
        {
            // Override in derived classes
        }

        protected virtual void Update()
        {
            // Only run on server
            if (!IsServer)
            {
                return;
            }
        }

        protected virtual void FixedUpdate()
        {
            // Only run on server
            if (!IsServer)
            {
                return;
            }
        }
    }
}

