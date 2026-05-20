using System;

namespace NightHunt.Data.DTOs
{
    [Serializable]
    public class MatchPresenceRequest
    {
        public string matchId;
        public long userId;
        public string state;
        public string reason;
        public string serverId;
        public string serverSecret;
    }
}
