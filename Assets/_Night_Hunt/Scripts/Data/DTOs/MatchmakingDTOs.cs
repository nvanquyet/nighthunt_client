using System;
using System.Collections.Generic;

namespace NightHunt.Data.DTOs
{
    [Serializable]
    public class MatchmakingQueueRequest
    {
        public string gameMode;
        public string mapId;        // optional — null = any map
        public bool   allowFill;   // true = fill missing slots with randoms
        public string platform;    // "MOBILE" | "PC" — auto-detected by client
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
    /// Sent by the Custom relay host to POST /api/match/end/custom after match ends.
    /// Must match backend MatchEndRequest.
    /// </summary>
    [Serializable]
    public class MatchResultRequest
    {
        public string matchId;
        public int    winnerTeamId;   // -1 = draw
        public string endReason;      // "TeamEliminated" | "TimerExpired" | "Draw"
        public List<MatchResultPlayerEntry> playerResults;
    }

    [Serializable]
    public class MatchResultPlayerEntry
    {
        public long   userId;         // backend user ID (long)
        public string displayName;
        public int    teamId;
        public int    kills;
        public int    deaths;
        public int    score;
    }
    /// <summary>
    /// Response from GET /api/matchmaking/queue/status.
    /// Null <c>status</c> means the user is not in an active queue entry.
    /// </summary>
    [Serializable]
    public class QueueStatusResponse
    {
        /// <summary>"SEARCHING" | "MATCHED" | null (not in queue)</summary>
        public string status;
        public string gameMode;
        public string lobbyToken;
        /// <summary>Seconds the player has been waiting.</summary>
        public long   waitSeconds;

        public bool IsActive =>
            status == "SEARCHING" || status == "MATCHED";
    }
}
