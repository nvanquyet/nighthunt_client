using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Base class for client-side effects
    /// </summary>
    public abstract class ClientEffectEvent : IGameplayEvent
    {
        public float Timestamp { get; set; }
        public Vector3 Position { get; set; }
        public int NetworkId { get; set; }

        protected ClientEffectEvent()
        {
            Timestamp = Time.time;
        }
    }

    /// <summary>
    /// Example: Damage effect event
    /// </summary>
    public class DamageEffectEvent : ClientEffectEvent
    {
        public float Damage { get; set; }
        public Vector3 HitPoint { get; set; }
        public Vector3 HitDirection { get; set; }
    }

    /// <summary>
    /// Example: Projectile spawn event
    /// </summary>
    public class ProjectileSpawnEvent : ClientEffectEvent
    {
        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public string ProjectilePrefabId { get; set; }
    }
}

