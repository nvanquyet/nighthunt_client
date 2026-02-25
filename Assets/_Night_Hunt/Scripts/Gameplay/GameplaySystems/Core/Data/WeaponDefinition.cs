using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Weapon item definition. Stats + PlayerModifiers from StatConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "GameplaySystems/Items/Weapon Definition")]
    public class WeaponDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Weapon;
        
        #region Stat Config
        
        [Header("Stat Config")]
        [Tooltip("Kéo thả WeaponStatConfig vào đây")]
        public WeaponStatConfig StatConfig;
        
        #endregion
        
        #region Ammo System
        
        [Header("Ammo System")]
        [Tooltip("Magazine size (số đạn trong băng)")]
        [Min(1)]
        public int MagazineSize = 30;
        
        [Tooltip("Total ammo capacity (tổng đạn súng có thể mang)")]
        [Min(1)]
        public int MaxAmmo = 300;
        
        [Tooltip("Default ammo when spawned (usually = MaxAmmo)")]
        [Min(0)]
        public int DefaultAmmo = 300;
        
        #endregion
        
        #region Reload
        
        [Header("Reload")]
        [Tooltip("Reload time (seconds)")]
        [Min(0.1f)]
        public float ReloadTime = 2.5f;
        
        [Tooltip("Can reload when magazine not empty (tactical reload)")]
        public bool CanTacticalReload = true;
        
        #endregion
        
        #region Override Methods
        
        public override float GetMaxResource()
        {
            // Weapon uses Ammo as resource
            return MaxAmmo;
        }
        
        public override float GetDefaultResource()
        {
            return DefaultAmmo;
        }
        
        #endregion
        
        #region Stat Helpers
        
        public float GetStatValue(ItemStatType statType)
        {
            return StatConfig != null ? StatConfig.GetStatValue(statType) : 0f;
        }
        
        public bool HasStat(ItemStatType statType)
        {
            return StatConfig != null && StatConfig.HasStat(statType);
        }
        
        public PlayerStatModifier[] GetPlayerModifiers()
        {
            return StatConfig?.PlayerModifiers;
        }
        
        #endregion
        
        #region Validation
        
        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error))
                return false;
            
            if (MagazineSize < 1)
            {
                error = "MagazineSize must be >= 1";
                return false;
            }
            
            if (MaxAmmo < MagazineSize)
            {
                error = "MaxAmmo must be >= MagazineSize";
                return false;
            }
            
            if (DefaultAmmo > MaxAmmo)
            {
                error = "DefaultAmmo cannot exceed MaxAmmo";
                return false;
            }
            
            error = null;
            return true;
        }
        
        #endregion
    }
}