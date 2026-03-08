using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Configs;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Weapon item definition.
    ///
    /// AMMO MODEL:
    /// - StatConfig[ItemStatType.MaxAmmo]      = total ammo reserve capacity (e.g. 300). Attachment-buffable.
    /// - StatConfig[ItemStatType.MagazineSize] = magazine capacity (e.g. 30). Attachment-buffable.
    /// - instance.CurrentResource              = current reserve ammo remaining (runtime, decreases on reload)
    /// - instance.CurrentMagazine             = rounds currently in chamber (runtime)
    ///
    /// REMOVED FIELDS (do not re-add):
    /// - MaxAmmo / DefaultAmmo  → use StatConfig[ItemStatType.MaxAmmo]
    /// - MagazineSize field     → use StatConfig[ItemStatType.MagazineSize] (attachment-buffable)
    /// - ReloadTime field       → use StatConfig[ItemStatType.ReloadSpeed]
    /// - ResourceType / MaxResource / DefaultResource → removed from ItemDefinition entirely
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "NightHunt/Items/Weapon Definition")]
    public class WeaponDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Weapon;

        // ── Stat Config ───────────────────────────────────────────────────────
        [Header("Stat Config")]
        [Tooltip("WeaponStatConfig containing Damage/FireRate/Accuracy/Spread/MagazineSize/DrawSpeed/ReloadSpeed + PlayerModifiers")]
        public WeaponStatConfig StatConfig;

        // ── Weapon Identity ───────────────────────────────────────────────────
        [Header("Weapon Identity")]
        [Tooltip("Weapon class for attachment compatibility and UI grouping")]
        public WeaponClass WeaponClass = WeaponClass.Rifle;

        [Tooltip("How damage is applied: instant raycast or spawned projectile")]
        public BallisticType BallisticType = BallisticType.Hitscan;

        [Tooltip("Default fire mode. Player can toggle on HUD if AllowFireModeToggle = true")]
        public FireMode DefaultFireMode = FireMode.Auto;

        [Tooltip("Allow player to toggle between Auto and Single on HUD")]
        public bool AllowFireModeToggle = true;

        // ── Reload ────────────────────────────────────────────────────────────
        [Header("Reload")]
        [Tooltip("Can reload when magazine still has ammo (tactical reload)")]
        public bool CanTacticalReload = true;
        // ── Visual FX (per-weapon) ──────────────────────────────────────
        // Each weapon owns its own VFX so CharacterCombat never needs centralized references.
        // WeaponVFXController reads these fields when OnShotFired is raised.
        [Header("Visual FX")]
        [Tooltip("For BallisticType.Projectile: spawn at muzzle point on fire.")]
        public GameObject ProjectilePrefab;

        [Tooltip("Muzzle flash particle or prefab spawned at the weapon's muzzle point on every shot.")]
        public GameObject MuzzleFlashPrefab;

        [Tooltip("Hitscan bullet trail prefab (LR / VFX Graph). Spawned between muzzle and hit point.")]
        public GameObject BulletTrailPrefab;

        [Tooltip("Impact particle spawned at hit point (stone/flesh/metal variants managed by caller).")]
        public GameObject HitEffectPrefab;
        // ── Stat Helpers ──────────────────────────────────────────────────────
        /// <summary>Read a stat from StatConfig (base value before attachments).</summary>
        public float GetStatValue(ItemStatType statType)
            => StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;

        public bool HasStat(ItemStatType statType)
            => StatConfig != null && StatConfig.HasStat(statType);

        /// <summary>PlayerModifiers applied to the PLAYER when this weapon is SELECTED (not just equipped).</summary>
        public PlayerStatModifier[] GetPlayerModifiers()
            => StatConfig?.PlayerModifiers;

        // ── Validation ────────────────────────────────────────────────────────
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false;

            if (StatConfig == null)
            {
                error = "[WeaponDefinition] StatConfig is required";
                return false;
            }

            float mag = StatConfig.GetStatValue(ItemStatType.MagazineSize);
            if (mag < 1f)
            {
                error = "[WeaponDefinition] StatConfig[MagazineSize] must be >= 1";
                return false;
            }

            float maxAmmo = StatConfig.GetStatValue(ItemStatType.MaxAmmo);
            if (maxAmmo > 0f && maxAmmo < mag)
            {
                error = "[WeaponDefinition] StatConfig[MaxAmmo] must be >= MagazineSize (or 0 for infinite ammo)";
                return false;
            }

            error = null;
            return true;
        }
    }

    // ── Supporting Enums ──────────────────────────────────────────────────────

    /// <summary>Weapon class for grouping and attachment compatibility.</summary>
    public enum WeaponClass
    {
        Pistol,
        SMG,
        Rifle,
        Shotgun,
        Sniper,
        Melee
    }

    /// <summary>How damage is applied.</summary>
    public enum BallisticType
    {
        /// <summary>Instant raycast — also spawns a visual-only bullet trail from pool.</summary>
        Hitscan,
        /// <summary>Physical projectile spawned from pool, deactivated when travel > VisionRange.</summary>
        Projectile
    }

    /// <summary>Fire mode: hold = continuous shots (Auto) or tap-per-shot (Single).</summary>
    public enum FireMode
    {
        Auto,
        Single
    }
}