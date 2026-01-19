using UnityEngine;
using NightHunt.Gameplay.Core.Events;

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

        public DamageEffectEvent(Vector3 hitPoint, Vector3 hitDirection, float damageAmount, GameObject instigator = null)
            : base()
        {
            Position = hitPoint;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            DamageAmount = damageAmount;
            if (instigator != null)
            {
                var networkObj = instigator.GetComponent<FishNet.Object.NetworkObject>();
                NetworkId = networkObj != null ? (int)networkObj.ObjectId : 0;
            }
        }
    }
}

