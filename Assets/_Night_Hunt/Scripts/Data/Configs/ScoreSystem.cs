using System;

namespace NightHunt.Data.Configs
{
    [Serializable]
    public class ScoreSystem
    {
        public string Action;
        public int BaseScore;
        public float PhaseMultiplier;
        public string Notes;
    }

    [Serializable]
    public class RankingSystem
    {
        public string Event;
        public int RPValue;
        public string Condition;
    }
}