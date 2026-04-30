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

            float horizontal = weapon.RecoilHorizontal > 0f
                ? weapon.RecoilHorizontal
                : Mathf.Max(0f, weapon.SpreadBase * 0.35f);
            float vertical = weapon.RecoilVertical > 0f
                ? weapon.RecoilVertical
                : Mathf.Max(0f, weapon.SpreadBase * 0.2f);

            float spreadFloor = Mathf.Max(0.001f, weapon.SpreadBase);
            float heat = Mathf.Clamp01((currentSpread - spreadFloor) / Mathf.Max(1f, spreadFloor * 3f));
            float multiplier = Mathf.Lerp(0.65f, 1.15f, heat);

            float recoilX = Random.Range(-horizontal, horizontal);
            float recoilY = Random.Range(0f, vertical);

            return new Vector2(recoilX, recoilY) * multiplier;
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

