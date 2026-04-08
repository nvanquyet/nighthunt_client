using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Definition asset for a weapon item.
    ///
    /// AMMO MODEL:
    ///   StatConfig[MagazineSize]  — magazine capacity (attachment-buffable).
    ///   StatConfig[MaxAmmo]       — total reserve capacity (attachment-buffable).
    ///   instance.CurrentMagazine  — rounds currently in chamber (runtime).
    ///   instance.CurrentResource  — current reserve ammo remaining (runtime).
    ///
    /// BALLISTIC / VFX CONFIG:
    ///   Lives on the weapon model prefab component (HitscanWeapon or ProjectileWeapon)
    ///   so it can be tweaked per-prefab without modifying the data asset.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "NightHunt/Items/Weapon Definition")]
    public class WeaponDefinition : EquippableItemDefinition
    {
        public override ItemType Type => ItemType.Weapon;

        [Header("Stat Config")]
        [Tooltip("All numeric combat stats: Damage, FireRate, MagazineSize, ReloadSpeed, SpreadBase …")]
        public WeaponStatConfig StatConfig;

        [Header("Weapon Identity")]
        [Tooltip("Weapon class for UI slot grouping and attachment compatibility.")]
        public WeaponClass WeaponClass = WeaponClass.Rifle;

        // ── EquippableItemDefinition overrides ───────────────────────────────
        protected override ItemStatConfig StatConfigBase => StatConfig;

        /// <summary>Starting reserve ammo = StatConfig[MaxAmmo].  0 = infinite ammo weapons.</summary>
        public override float GetDefaultCurrentValue()
            => StatConfig != null ? StatConfig.GetStatValue(ItemStatType.MaxAmmo) : 0f;

        /// <summary>Player-stat modifiers applied when this weapon is drawn (selected).</summary>
        public PlayerStatModifier[] GetPlayerModifiers()
            => StatConfig?.PlayerModifiers;

        // ── Validation ───────────────────────────────────────────────────────
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false;

            if (StatConfig == null)
            { error = "[WeaponDefinition] StatConfig is required"; return false; }

            float mag = StatConfig.GetStatValue(ItemStatType.MagazineSize);
            if (mag < 1f)
            { error = "[WeaponDefinition] StatConfig[MagazineSize] must be >= 1"; return false; }

            float maxAmmo = StatConfig.GetStatValue(ItemStatType.MaxAmmo);
            if (maxAmmo > 0f && maxAmmo < mag)
            { error = "[WeaponDefinition] StatConfig[MaxAmmo] must be >= MagazineSize or 0 for infinite"; return false; }

            error = null;
            return true;
        }
    }

    // ── Supporting Enums ─────────────────────────────────────────────────────

    /// <summary>Weapon class used for UI grouping and attachment slot compatibility checks.</summary>
    public enum WeaponClass
    {
        Pistol  = 0,
        SMG     = 1,
        Rifle   = 2,
        Shotgun = 3,
        Sniper  = 4,
        Melee   = 5,
        Launcher = 6,  // Rocket launchers, grenade launchers
    }

    /// <summary>How projectiles deliver damage. Set on the weapon model component, not here.</summary>
    public enum BallisticType
    {
        /// <summary>Instant raycast — spawns a visual-only trail from the projectile pool.</summary>
        Hitscan    = 0,
        /// <summary>Physical projectile from the pool — applies damage on collision.</summary>
        Projectile = 1,
    }

    /// <summary>Auto fires while the trigger is held; Single requires a fresh press per shot.</summary>
    public enum FireMode
    {
        Auto   = 0,
        Single = 1,
    }
}