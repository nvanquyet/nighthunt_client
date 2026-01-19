using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Gameplay.Scoring
{
    /// <summary>
    /// Score event for gameplay event bus
    /// </summary>
    public class ScoreEvent : IGameplayEvent
    {
        public float Timestamp { get; set; }
        public int TeamId { get; set; }
        public int Score { get; set; }
        public string ScoreType { get; set; } // Kill, Assist, BossKill, Objective, Survival

        public ScoreEvent(int teamId, int score, string scoreType)
        {
            Timestamp = UnityEngine.Time.time;
            TeamId = teamId;
            Score = score;
            ScoreType = scoreType;
        }
    }
}

