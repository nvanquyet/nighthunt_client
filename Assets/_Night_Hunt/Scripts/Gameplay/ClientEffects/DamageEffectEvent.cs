using UnityEngine;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Raised when a bullet / melee attack lands — drives hit VFX (sparks, decals).
    ///
    /// READONLY STRUCT — zero heap allocation per event.
    /// Pass networkId explicitly; no ComponentResolver dependency.
    /// </summary>
    public readonly struct DamageEffectEvent : IGameplayEvent
    {
        public float   Timestamp   { get; }
        public Vector3 HitPoint    { get; }
        public Vector3 HitDirection { get; }
        public float   DamageAmount { get; }
        public bool    IsHeadshot  { get; }
        public int     NetworkId   { get; }

        public DamageEffectEvent(Vector3 hitPoint, Vector3 hitDirection, float damageAmount,
                                 bool isHeadshot = false, int networkId = 0)
        {
            Timestamp   = Time.time;
            HitPoint    = hitPoint;
            HitDirection = hitDirection;
            DamageAmount = damageAmount;
            IsHeadshot  = isHeadshot;
            NetworkId   = networkId;
        }
    }
}