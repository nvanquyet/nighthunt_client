using UnityEngine;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Raised when a projectile is spawned — drives visual trail effects on observers.
    ///
    /// READONLY STRUCT — zero heap allocation per event.
    /// </summary>
    public readonly struct ProjectileSpawnEvent : IGameplayEvent
    {
        public float   Timestamp { get; }
        public Vector3 Position  { get; }
        public Vector3 Direction { get; }
        public float   Speed     { get; }
        public int     NetworkId { get; }

        public ProjectileSpawnEvent(Vector3 position, Vector3 direction, float speed, int networkId = 0)
        {
            Timestamp = Time.time;
            Position  = position;
            Direction = direction;
            Speed     = speed;
            NetworkId = networkId;
        }
    }
}