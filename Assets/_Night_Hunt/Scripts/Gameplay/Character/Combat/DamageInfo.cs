using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Immutable snapshot of a single damage event, passed through the damage pipeline.
    /// FishNet can serialize this struct in ServerRpc / ObserversRpc parameters.
    /// </summary>
    [System.Serializable]
    public struct DamageInfo
    {
        /// <summary>Raw damage value before armor reduction.</summary>
        public float Damage;

        /// <summary>True when the shot hit a head-hitbox; triggers headshot multiplier.</summary>
        public bool IsHeadshot;

        /// <summary>World-space contact point (for blood/impact VFX spawned on all clients).</summary>
        public Vector3 HitPoint;

        /// <summary>Surface normal at the contact point.</summary>
        public Vector3 HitNormal;

        /// <summary>FishNet NetworkObject ID of the shooter's player object. -1 = server/world damage.</summary>
        public int ShooterNetworkObjectId;

        /// <summary>Item definition ID of the weapon that caused this damage.</summary>
        public string WeaponId;
    }
}
