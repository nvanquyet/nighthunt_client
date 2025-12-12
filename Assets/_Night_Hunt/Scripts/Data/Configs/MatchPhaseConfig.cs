using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class MatchPhaseConfig
    {
        public string Phase;
        public float DurationMin;
        public float DurationMax;
        public bool RespawnEnabled;
        public bool BeaconEnabled;
        public string ZoneActive;
        public string ObjectiveEvents;
        public float ScoreMultiplier;
        public float SurvivalMultiplier;
        public string BuffForLosingTeam;
        public string NerfForWinningTeam;
    }
}