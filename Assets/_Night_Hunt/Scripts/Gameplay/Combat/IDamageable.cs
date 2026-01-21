using UnityEngine;

namespace NightHunt.Gameplay.Combat
{
    /// <summary>
    /// Interface for objects that can take damage
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Apply damage to this object
        /// </summary>
        /// <param name="damage">Damage amount</param>
        /// <param name="context">Damage context with source information</param>
        void TakeDamage(float damage, DamageContext context);

        /// <summary>
        /// Check if this object can take damage
        /// </summary>
        bool CanTakeDamage();

        /// <summary>
        /// Get current health
        /// </summary>
        float GetCurrentHealth();

        /// <summary>
        /// Get max health
        /// </summary>
        float GetMaxHealth();
    }

    /// <summary>
    /// Damage context - contains information about the damage source
    /// </summary>
    public struct DamageContext
    {
        public IAttackSource Source;
        public Vector3 HitPoint;
        public Vector3 HitDirection;
        public string DamageType; // "Bullet", "Explosion", "Melee", etc.
        public bool IsHeadshot;
        public bool IsCritical;

        public DamageContext(IAttackSource source, Vector3 hitPoint, Vector3 hitDirection, string damageType = "Bullet", bool isHeadshot = false, bool isCritical = false)
        {
            Source = source;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            DamageType = damageType;
            IsHeadshot = isHeadshot;
            IsCritical = isCritical;
        }
    }
}

