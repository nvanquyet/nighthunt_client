using FishNet.Connection;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Utilities
{
    public static class DamageCalculator
    {
        public static DamagePayload CreateBulletDamage(
            float baseDamage,
            Vector3 hitPoint,
            Vector3 hitNormal,
            bool isHeadshot,
            NetworkConnection attacker,
            string weaponId)
        {
            return new DamagePayload
            {
                rawDamage = baseDamage,
                damageType = DamageType.Bullet,
                hitPoint = hitPoint,
                hitNormal = hitNormal,
                isHeadshot = isHeadshot,
                isCritical = UnityEngine.Random.value < 0.1f, // 10% crit chance
                attacker = attacker,
                weaponId = weaponId
            };
        }

        public static DamagePayload CreateExplosionDamage(
            float baseDamage,
            Vector3 explosionCenter,
            Vector3 targetPosition,
            float explosionRadius,
            NetworkConnection attacker)
        {
            float distance = Vector3.Distance(explosionCenter, targetPosition);
            float falloff = 1f - (distance / explosionRadius);
            float finalDamage = baseDamage * Mathf.Max(0f, falloff);

            return new DamagePayload
            {
                rawDamage = finalDamage,
                damageType = DamageType.Explosion,
                hitPoint = targetPosition,
                hitNormal = (targetPosition - explosionCenter).normalized,
                isHeadshot = false,
                isCritical = false,
                attacker = attacker,
                weaponId = "explosion"
            };
        }

        public static DamagePayload CreateFallDamage(float fallDistance, NetworkConnection victim)
        {
            // Fall damage = (distance - 5m) * 10
            float damage = Mathf.Max(0f, (fallDistance - 5f) * 10f);

            return new DamagePayload
            {
                rawDamage = damage,
                damageType = DamageType.Fall,
                hitPoint = Vector3.zero,
                hitNormal = Vector3.up,
                isHeadshot = false,
                isCritical = false,
                attacker = victim, // Self damage
                weaponId = "fall"
            };
        }

        public static bool IsHeadshot(RaycastHit hit)
        {
            // Check if hit collider is tagged as "Head"
            return hit.collider.CompareTag("Head");
        }
    }
}