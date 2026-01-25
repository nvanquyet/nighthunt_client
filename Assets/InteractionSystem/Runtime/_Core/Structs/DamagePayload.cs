using System;
using FishNet.Connection;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
    [Serializable]
    public struct DamagePayload
    {
        public float rawDamage;
        public DamageType damageType;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public bool isHeadshot;
        public bool isCritical;
        public NetworkConnection attacker;
        public string weaponId;
    }

    public enum DamageType
    {
        Bullet,
        Melee,
        Explosion,
        Fall,
        Environmental
    }
}