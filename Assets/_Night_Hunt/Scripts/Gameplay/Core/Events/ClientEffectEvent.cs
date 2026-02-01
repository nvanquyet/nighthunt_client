using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Base class for client-side effects.
    /// Concrete implementations are in NightHunt.Gameplay.ClientEffects namespace:
    /// - DamageEffectEvent (in DamageEffectEvent.cs)
    /// - ProjectileSpawnEvent (in ProjectileSpawnEvent.cs)
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
}

