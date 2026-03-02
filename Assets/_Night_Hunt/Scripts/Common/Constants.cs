namespace NightHunt.Common
{
    public static class Constants
    {
        // API Endpoints (relative paths - base URL comes from BackendConfig)
        public const string API_AUTH_REGISTER = "/auth/register";
        public const string API_AUTH_LOGIN = "/auth/login";
        public const string API_AUTH_AUTO_LOGIN = "/auth/auto-login";
        public const string API_AUTH_LOGOUT = "/auth/logout";
        public const string API_AUTH_CHANGE_PASSWORD = "/auth/change-password";
        
        public const string API_ROOMS_CREATE = "/rooms/create";
        public const string API_ROOMS_JOIN_BY_CODE = "/rooms/join-by-code";
        public const string API_ROOMS_QUICK_PLAY = "/rooms/quick-play";
        public const string API_ROOMS_RECONNECT = "/rooms/reconnect";
        public const string API_ROOMS_READY = "/rooms/{0}/ready";
        public const string API_ROOMS_CHANGE_TEAM = "/rooms/{0}/change-team";
        public const string API_ROOMS_LEAVE = "/rooms/{0}/leave";
        public const string API_ROOMS_KICK = "/rooms/{0}/kick/{1}";
        public const string API_ROOMS_DISBAND = "/rooms/{0}/disband";
        public const string API_ROOMS_START = "/rooms/{0}/start";
        public const string API_ROOMS_GET = "/rooms/{0}";
        public const string API_ROOMS_SWAP_REQUEST = "/rooms/{0}/swap-request";
        public const string API_ROOMS_SWAP_ACCEPT = "/rooms/{0}/swap-accept/{1}";
        public const string API_ROOMS_SWAP_REJECT = "/rooms/{0}/swap-reject/{1}";
        public const string API_ROOMS_SWAP_CANCEL = "/rooms/{0}/swap-cancel/{1}";
        public const string API_ROOMS_SWAP_REQUESTS = "/rooms/{0}/swap-requests";
        public const string API_ROOMS_UPDATE_SETTINGS = "/rooms/{0}/update-settings";
        public const string API_ROOMS_TRANSFER_OWNER = "/rooms/{0}/transfer-owner";

        // Matchmaking
        public const string API_MATCHMAKING_QUEUE   = "/api/matchmaking/queue";
        public const string API_MATCHMAKING_ACCEPT  = "/api/matchmaking/accept";
        public const string API_MATCHMAKING_DECLINE = "/api/matchmaking/decline";

        // Game Modes
        public const string MODE_2V2 = "2v2";
        public const string MODE_3V3 = "3v3";
        public const string MODE_5V5 = "5v5";

        // Room Status
        public const string ROOM_STATUS_WAITING = "WAITING";
        public const string ROOM_STATUS_IN_GAME = "IN_GAME";
        public const string ROOM_STATUS_CLOSED = "CLOSED";

        // Teams
        public const int TEAM_1 = 1;
        public const int TEAM_2 = 2;

        // PlayerPrefs Keys
        public const string PREFS_ACCESS_TOKEN = "NH_ACCESS_TOKEN";
        public const string PREFS_SESSION_ID = "NH_SESSION_ID";
        public const string PREFS_USER_ID = "NH_USER_ID";
        public const string PREFS_USERNAME = "NH_USERNAME";
    }
}

