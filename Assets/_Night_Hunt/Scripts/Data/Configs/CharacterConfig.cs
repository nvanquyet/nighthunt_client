using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class CharacterConfig
    {
        public string CharacterId;
        public string DisplayName;
        public float BaseHP;
        public float BaseStamina;
        public float BaseMoveSpeed;
        public float BaseWeightCapacity;
        public float BaseVisionRadius;
        public float BaseNoiseLevel;
        public float BaseReviveTime;
        public float BaseRevivedHP;
        public float BaseBleedoutTime;
        public float BaseADS_Slow;
    }
}