using UnityEngine;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Event for damage effects
    /// </summary>
    public class DamageEffectEvent : ClientEffectEvent
    {
        public Vector3 HitPoint { get; private set; }
        public Vector3 HitDirection { get; private set; }
        public float DamageAmount { get; private set; }

        public DamageEffectEvent(Vector3 hitPoint, Vector3 hitDirection, float damageAmount,
            GameObject instigator = null)
            : base()
        {
            Position = hitPoint;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            DamageAmount = damageAmount;
            if (instigator != null)
            {
                var networkObj = ComponentResolver.Find<FishNet.Object.NetworkObject>(instigator)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] FishNet.Object.NetworkObject not found")
                    .Resolve();
                NetworkId = networkObj != null ? (int)networkObj.ObjectId : 0;
            }
        }
    }
}