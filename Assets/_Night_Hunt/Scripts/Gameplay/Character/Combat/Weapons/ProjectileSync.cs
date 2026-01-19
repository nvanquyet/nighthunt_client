using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Network sync for projectiles
    /// </summary>
    public class ProjectileSync : NetworkBehaviour
    {
        private readonly SyncVar<Vector3> networkPosition = new SyncVar<Vector3>();
        private readonly SyncVar<Vector3> networkDirection = new SyncVar<Vector3>();

        private ProjectileComponent projectileComponent;

        private void Awake()
        {
            projectileComponent = GetComponent<ProjectileComponent>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkPosition.OnChange += OnPositionChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (networkPosition != null)
                networkPosition.OnChange -= OnPositionChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            // Server: Sync position
            if (projectileComponent != null)
            {
                networkPosition.Value = transform.position;
            }
        }

        private void OnPositionChanged(Vector3 oldPos, Vector3 newPos, bool asServer)
        {
            if (!asServer && projectileComponent != null)
            {
                // Client: Apply server position for reconciliation
                transform.position = newPos;
            }
        }
    }
}

