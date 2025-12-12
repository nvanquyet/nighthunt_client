using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class StatusEffectConfig
    {
        public string StatusId;
        public string StatusType;
        public string TargetStat;
        public string Operation;
        public float Value;
        public float Duration;
        public bool Stackable;
        public string Description;
    }
}