using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Abstract base for client-side effect events.
    /// Concrete event types live in NightHunt.Gameplay.ClientEffects as readonly structs
    /// (DamageEffectEvent, ProjectileSpawnEvent) — zero GC per publish.
    /// </summary>
    public abstract class ClientEffectEvent : IGameplayEvent
    {
        public float   Timestamp { get; set; }
        public Vector3 Position  { get; set; }
        public int     NetworkId { get; set; }

        protected ClientEffectEvent()
        {
            Timestamp = Time.time;
        }
    }
}

