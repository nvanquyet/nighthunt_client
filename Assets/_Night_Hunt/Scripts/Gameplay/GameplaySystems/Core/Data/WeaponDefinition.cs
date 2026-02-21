using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Weapon item definition
    /// Extends ItemDefinition with weapon-specific properties
    /// 
    /// Key Features:
    /// - Weapon stats (damage, fire rate, accuracy, etc.) - defined HERE in Stats array
    /// - Ammo system (magazine + total ammo)
    /// - Reload mechanics
    /// - Player modifiers (how weapon affects player when equipped)
    /// - No separate ammo items - each weapon has limited total ammo
    /// 
    /// NOTE: Weapon stats are item-specific, NOT global config
    /// Each weapon definition has its own Stats[] array
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "GameplaySystems/Items/Weapon Definition")]
    public class WeaponDefinition : ItemDefinition
    {
        public override ItemType Type => ItemType.Weapon;
        
        #region Weapon Stats
        
        [Header("Weapon Stats")]
        [Tooltip("Array of weapon stats với default values")]
        public WeaponStatValue[] Stats;
        
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
        
        #region Player Modifiers
        
        [Header("Player Modifiers")]
        [Tooltip("Player stat modifiers when this weapon is equipped")]
        public PlayerStatModifier[] PlayerModifiers;
        
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
        
        /// <summary>
        /// Get stat default value by type
        /// </summary>
        public float GetStatValue(ItemStatType statType)
        {
            if (Stats == null)
                return 0f;
            
            foreach (var stat in Stats)
            {
                if (stat.StatType == statType)
                    return stat.Value;
            }
            
            return 0f;
        }
        
        /// <summary>
        /// Check if weapon has specific stat
        /// </summary>
        public bool HasStat(ItemStatType statType)
        {
            if (Stats == null)
                return false;
            
            foreach (var stat in Stats)
            {
                if (stat.StatType == statType)
                    return true;
            }
            
            return false;
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
        
        #region Editor Setup
        
#if UNITY_EDITOR
        [ContextMenu("Setup Default Rifle Stats")]
        private void SetupDefaultRifleStats()
        {
            Stats = new WeaponStatValue[]
            {
                new WeaponStatValue { StatType = ItemStatType.Damage, Value = 30 },
                new WeaponStatValue { StatType = ItemStatType.FireRate, Value = 600 },  // 600 RPM
                new WeaponStatValue { StatType = ItemStatType.Accuracy, Value = 70 },
                new WeaponStatValue { StatType = ItemStatType.Recoil, Value = 25 },
                new WeaponStatValue { StatType = ItemStatType.Spread, Value = 0.5f },
                new WeaponStatValue { StatType = ItemStatType.Range, Value = 150 }
            };
            
            MagazineSize = 30;
            MaxAmmo = 300;
            DefaultAmmo = 300;
            ReloadTime = 2.5f;
            
            ResourceType = ItemResourceType.Ammo;
            MaxResource = MaxAmmo;
            DefaultResource = DefaultAmmo;
            
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
            AttachmentSlots = new AttachmentSlotType[] 
            { 
                AttachmentSlotType.Optic, 
                AttachmentSlotType.Grip, 
                AttachmentSlotType.Magazine, 
                AttachmentSlotType.Barrel 
            };
            
            PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.MovementSpeed,
                    Value = -5f,
                    ModifierType = ModifierType.Percentage,
                    Description = "Rifle movement penalty"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[WeaponDefinition] Setup default rifle stats complete!");
        }
        
        [ContextMenu("Setup Default Pistol Stats")]
        private void SetupDefaultPistolStats()
        {
            Stats = new WeaponStatValue[]
            {
                new WeaponStatValue { StatType = ItemStatType.Damage, Value = 20 },
                new WeaponStatValue { StatType = ItemStatType.FireRate, Value = 300 },
                new WeaponStatValue { StatType = ItemStatType.Accuracy, Value = 60 },
                new WeaponStatValue { StatType = ItemStatType.Recoil, Value = 15 },
                new WeaponStatValue { StatType = ItemStatType.Range, Value = 50 }
            };
            
            MagazineSize = 12;
            MaxAmmo = 120;
            DefaultAmmo = 120;
            ReloadTime = 1.8f;
            
            ResourceType = ItemResourceType.Ammo;
            MaxResource = MaxAmmo;
            DefaultResource = DefaultAmmo;
            
            ValidSlots = new SlotLocationType[] { SlotLocationType.Inventory, SlotLocationType.Weapon };
            AttachmentSlots = new AttachmentSlotType[] 
            { 
                AttachmentSlotType.Barrel, 
                AttachmentSlotType.Magazine 
            };
            
            PlayerModifiers = new PlayerStatModifier[]
            {
                new PlayerStatModifier
                {
                    StatType = PlayerStatType.MovementSpeed,
                    Value = -2f,
                    ModifierType = ModifierType.Percentage,
                    Description = "Pistol movement penalty"
                }
            };
            
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[WeaponDefinition] Setup default pistol stats complete!");
        }
#endif
        
        #endregion
    }
    
    #region Supporting Types
    
    /// <summary>
    /// Weapon stat value
    /// Used in ScriptableObject config
    /// </summary>
    [System.Serializable]
    public struct WeaponStatValue
    {
        [Tooltip("Loại stat")]
        public ItemStatType StatType;
        
        [Tooltip("Giá trị mặc định")]
        public float Value;
    }
    
    #endregion
}