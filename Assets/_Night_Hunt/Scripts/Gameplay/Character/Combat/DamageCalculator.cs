using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Damage calculation với headshot multiplier và recoil
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// Calculate damage with headshot multiplier
        /// </summary>
        public static float CalculateDamage(WeaponConfigData weapon, bool isHeadshot)
        {
            if (weapon == null) return 0f;

            float damage = weapon.DamageBody;
            if (isHeadshot)
            {
                damage *= weapon.DamageHeadMul;
            }

            return damage;
        }

        /// <summary>
        /// Calculate 2D directional recoil
        /// </summary>
        public static Vector2 CalculateRecoil(WeaponConfigData weapon, float currentSpread)
        {
            if (weapon == null) return Vector2.zero;

            // 2D directional recoil pattern using spread values
            // Use SpreadBase as base recoil value
            float recoilX = Random.Range(-weapon.SpreadBase, weapon.SpreadBase);
            float recoilY = Random.Range(0f, weapon.SpreadBase * 0.5f); // Vertical recoil is typically less

            return new Vector2(recoilX, recoilY) * currentSpread;
        }

        /// <summary>
        /// Apply recoil to aim direction
        /// </summary>
        public static Vector3 ApplyRecoil(Vector3 aimDirection, Vector2 recoil)
        {
            // Convert 2D recoil to 3D direction change
            Quaternion recoilRotation = Quaternion.Euler(-recoil.y, recoil.x, 0f);
            return recoilRotation * aimDirection;
        }
    }
}

