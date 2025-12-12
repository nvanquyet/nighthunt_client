using System;
using System.Collections.Generic;
using NightHunt.Data.Configs;

namespace NightHunt.Data
{
    /// <summary>
    /// Root data structure matching JSON config
    /// </summary>
    [Serializable]
    public class GameData
    {
        public List<WeaponConfig> WeaponConfig;
        public List<ItemConfig> ItemConfig;
        public List<VisionModifier> VisionModifiers;
        public List<RespawnConfig> RespawnConfig;
        public List<MatchPhaseConfig> MatchPhaseConfig;
        public List<ZoneConfig> ZoneConfig;
        public List<StaminaWeightConfig> StaminaWeightConfig;
        public List<InventoryConfig> InventoryConfig;
        public List<ScoreSystem> ScoreSystem;
        public List<RankingSystem> RankingSystem;
        public List<CharacterConfig> CharacterConfig;
        public List<StatusEffectConfig> StatusEffectConfig;
    }
}