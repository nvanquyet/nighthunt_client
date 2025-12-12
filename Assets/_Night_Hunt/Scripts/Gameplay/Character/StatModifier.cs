using System;
using UnityEngine;

namespace _Night_Hunt.Scripts.Gameplay.Character
{
    public enum StatType
    {
        MaxHP,
        MaxStamina,
        MoveSpeed,
        WeightCapacity,
        VisionRadius,
        NoiseLevel,
        ReviveTime,
        BleedoutTime
    }

    public enum ModifierOperation
    {
        Add,              // BaseStat + value
        Multiply,         // BaseStat * value
        MultiplyAdditive  // BaseStat * (1 + (value - 1))
    }

    [Serializable]
    public class StatModifier
    {
        public string modifierId;
        public StatType targetStat;
        public ModifierOperation operationType;
        public float value;
        public float duration; // 0 = permanent
        public bool stackable;
        public string source; // "Weapon", "Item", "Zone", "StatusEffect"
        
        [NonSerialized] public float timeApplied;

        public StatModifier(string id, StatType stat, ModifierOperation op, float val, bool stack = false, float dur = 0f)
        {
            modifierId = id;
            targetStat = stat;
            operationType = op;
            value = val;
            stackable = stack;
            duration = dur;
            timeApplied = Time.time;
        }

        public bool IsExpired()
        {
            return duration > 0 && (Time.time - timeApplied) >= duration;
        }
    }
}