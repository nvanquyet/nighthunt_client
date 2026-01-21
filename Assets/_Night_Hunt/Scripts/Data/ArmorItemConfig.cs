using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Data
{
    /// <summary>
    /// Armor item configuration
    /// Extends BaseItemConfig with armor-specific fields
    /// </summary>
    [Serializable]
    public class ArmorItemConfig : BaseItemConfig
    {
        public float DefenseValue;
        public float DamageReductionPercent;
        public Dictionary<string, float> StatModifiers; // VD: {"MoveSpeed": 0.1f, "StaminaRegen": 0.2f}

        public ArmorItemConfig()
        {
            Type = ItemType.Armor;
            StatModifiers = new Dictionary<string, float>();
        }
    }
}

