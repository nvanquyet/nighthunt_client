using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class RespawnConfig
    {
        public string ConfigName;
        public float BeaconHP;
        public float PlaceTime;
        public float RespawnDelay;
        public float RedeployCooldown;
        public float MinDistance;
        public string DisableInPhase;
        public int DestroyScoreReward;
    }
}