using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class ZoneConfig
    {
        public string ZoneId;
        public string ZoneType;
        public float Radius;
        public float Duration;
        public float DamagePerSec;
        public float VisionMultiplier;
        public float SpeedMultiplier;
        public float StaminaRegen;
        public bool PingReveal;
        public string Description;
    }
}