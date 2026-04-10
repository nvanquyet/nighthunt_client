using System;
using System.Collections.Generic;

namespace NightHunt.Data.DTOs
{
    [Serializable]
    public class MatchmakingQueueRequest
    {
        public string gameMode;
        public string mapId;      // optional — null = any map
    }

    [Serializable]
    public class MatchmakingAcceptRequest
    {
        public string lobbyToken;
    }

    [Serializable]
    public class MatchmakingDeclineRequest
    {
        public string lobbyToken;
    }

    /// <summary>
    /// Sent by any connected client to POST /api/matches/{matchId}/result after match ends.
    /// The backend validates the matchId against the authenticated user's active match.
    /// </summary>
    [Serializable]
    public class MatchResultRequest
    {
        public string matchId;
        public int    winnerTeamId;   // -1 = draw
        public string endReason;      // "TeamEliminated" | "TimerExpired" | "Draw"
        public List<MatchResultPlayerEntry> players;
    }

    [Serializable]
    public class MatchResultPlayerEntry
    {
        public string backendPlayerId;
        public int    teamId;
        public int    kills;
        public int    deaths;
        public int    score;
    }
}
