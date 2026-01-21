using System;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Weapon item configuration
    /// Extends BaseItemConfig with weapon-specific fields
    /// </summary>
    [Serializable]
    public class WeaponItemConfig : BaseItemConfig
    {
        public string ProjectilePrefabId; // Reference tới projectile prefab
        public WeaponConfigData WeaponData; // Reuse existing WeaponConfigData for damage, fireRate, ammo...
        public string[] AllowedAttachments; // VD: ["Scope", "Suppressor", "Grip"]

        public WeaponItemConfig()
        {
            Type = ItemType.Weapon;
        }
    }
}

