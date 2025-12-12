using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class StaminaWeightConfig
    {
        public string WeightThreshold;
        public float MoveSpeedPenalty;
        public float StaminaDrainMultiplier;
        public bool SprintAllowed;
    }
}