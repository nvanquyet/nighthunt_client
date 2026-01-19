using UnityEngine;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Event for projectile spawn effects
    /// </summary>
    public class ProjectileSpawnEvent : ClientEffectEvent
    {
        public Vector3 Direction { get; private set; }
        public float Speed { get; private set; }

        public ProjectileSpawnEvent(Vector3 position, Vector3 direction, float speed, GameObject instigator = null)
            : base()
        {
            Position = position;
            Direction = direction;
            Speed = speed;
            if (instigator != null)
            {
                var networkObj = instigator.GetComponent<FishNet.Object.NetworkObject>();
                NetworkId = networkObj != null ? (int)networkObj.ObjectId : 0;
            }
        }
    }
}

