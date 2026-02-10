using System;
using System.Collections.Generic;
using NightHunt.Inventory.Core.Enums;
using UnityEngine;

namespace _Night_Hunt.Scripts.InventorySystems.Core.Stats.CharacterStat
{
    [CreateAssetMenu(menuName = "NightHunt/InventorySystems/Stats/PlayerStatData", fileName = "NewPlayerStatData")]
    public class PlayerStatData : ScriptableObject
    {
        [SerializeField] private CharacterStatStruct[] stats;
        public CharacterStatStruct[] Stats => stats;

        private Dictionary<CharacterStatType, CharacterStatStruct> _statDictionary;

        private Dictionary<CharacterStatType, CharacterStatStruct> StatDictionary
        {
            get
            {
                if (_statDictionary == null)
                {
                    _statDictionary = new Dictionary<CharacterStatType, CharacterStatStruct>();
                    foreach (var stat in stats)
                    {
                        _statDictionary[stat.StatType] = stat;
                    }
                }

                return _statDictionary;
            }
        }

        public float GetValueStat(CharacterStatType statType)
        {
            if (StatDictionary.TryGetValue(statType, out var stat))
            {
                return stat.DefaultValue;
            }

            Debug.LogWarning($"Stat type {statType} not found in PlayerStatData.");
            return 0f;
        }
    }

    [Serializable]
    public struct CharacterStatStruct
    {
        public CharacterStatType StatType;
        public float DefaultValue;
    }
}