using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class ItemConfig
    {
        public string ItemId;
        public string DisplayName;
        public string Category;
        public string UseType;
        public string TargetType;
        public string EffectType;
        public float EffectValue;
        public float EffectDuration;
        public float CastTime;
        public float Cooldown;
        public int MaxStack;
        public float Weight;
        public bool CanDrop;
        public bool IsConsumable;
        public string Rarity;
        public int AllowedPhaseMask;
        public string ExtraParamsJson;
    }
}