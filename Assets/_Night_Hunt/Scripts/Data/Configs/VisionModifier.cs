using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class VisionModifier
    {
        public string ModifierId;
        public string SourceType;
        public string Target;
        public string OpType;
        public float Value;
        public float Duration;
        public bool Stackable;
    }
}