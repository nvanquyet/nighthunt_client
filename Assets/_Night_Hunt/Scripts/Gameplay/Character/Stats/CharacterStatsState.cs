using System;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// Serializable state for character stats prediction
    /// </summary>
    [Serializable]
    public struct CharacterStatsState
    {
        public float HP;
        public float Stamina;
        public float CurrentWeight;
        public float MoveSpeedMultiplier;
        public float VisionRadius;
        public float NoiseLevel;

        public bool IsStateDifferent(CharacterStatsState other, float threshold = 0.1f)
        {
            return Mathf.Abs(HP - other.HP) > threshold ||
                   Mathf.Abs(Stamina - other.Stamina) > threshold ||
                   Mathf.Abs(CurrentWeight - other.CurrentWeight) > threshold ||
                   Mathf.Abs(MoveSpeedMultiplier - other.MoveSpeedMultiplier) > threshold ||
                   Mathf.Abs(VisionRadius - other.VisionRadius) > threshold ||
                   Mathf.Abs(NoiseLevel - other.NoiseLevel) > threshold;
        }
    }
}

