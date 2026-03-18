using System;

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
}
