namespace NightHunt.Common
{
    public static class Constants
    {
        // API Endpoints (relative paths - base URL comes from BackendConfig)
        // Base URL = http(s)://{apiHost} (no /api suffix). Context-path on server = /api.
        // AuthController:            @RequestMapping("/auth")          → /api/auth/...
        // ProfileController:         @RequestMapping("/profile")       → /api/profile/...
        // MatchmakingQueueController:@RequestMapping("/matchmaking/queue") → /api/matchmaking/queue/...
        public const string API_AUTH_REGISTER        = "/api/auth/register";
        public const string API_AUTH_LOGIN            = "/api/auth/login";
        public const string API_AUTH_REFRESH_TOKEN    = "/api/auth/refresh-token";
        public const string API_AUTH_AUTO_LOGIN       = "/api/auth/auto-login";
        public const string API_AUTH_LOGOUT           = "/api/auth/logout";
        public const string API_AUTH_CHANGE_PASSWORD  = "/api/auth/change-password";

        // Profile endpoints — ProfileController @RequestMapping("/profile") + context-path /api → /api/profile/...
        public const string API_PROFILE_GET           = "/api/profile";
        public const string API_PROFILE_PUBLIC        = "/api/profile/{0}";   // GET /profile/{userId} — public card
        public const string API_PROFILE_SET_CHARACTER = "/api/profile/character";
        
        // Room endpoints — RoomController @RequestMapping("/rooms") + context-path /api → /api/rooms/...
        public const string API_ROOMS_CREATE          = "/api/rooms/create";
        public const string API_ROOMS_JOIN_BY_CODE    = "/api/rooms/join-by-code";
        public const string API_ROOMS_QUICK_PLAY      = "/api/rooms/quick-play";
        public const string API_ROOMS_RECONNECT       = "/api/rooms/reconnect";
        public const string API_ROOMS_READY           = "/api/rooms/{0}/ready";
        public const string API_ROOMS_CHANGE_TEAM     = "/api/rooms/{0}/change-team";
        public const string API_ROOMS_LEAVE           = "/api/rooms/{0}/leave";
        public const string API_ROOMS_KICK            = "/api/rooms/{0}/kick/{1}";
        public const string API_ROOMS_DISBAND         = "/api/rooms/{0}/disband";
        public const string API_ROOMS_START           = "/api/rooms/{0}/start";
        public const string API_ROOMS_GET             = "/api/rooms/{0}";
        public const string API_ROOMS_SWAP_REQUEST    = "/api/rooms/{0}/swap-request";
        public const string API_ROOMS_SWAP_ACCEPT     = "/api/rooms/{0}/swap-accept/{1}";
        public const string API_ROOMS_SWAP_REJECT     = "/api/rooms/{0}/swap-reject/{1}";
        public const string API_ROOMS_SWAP_CANCEL     = "/api/rooms/{0}/swap-cancel/{1}";
        public const string API_ROOMS_SWAP_REQUESTS   = "/api/rooms/{0}/swap-requests";
        public const string API_ROOMS_UPDATE_SETTINGS = "/api/rooms/{0}/update-settings";
        public const string API_ROOMS_TRANSFER_OWNER  = "/api/rooms/{0}/transfer-owner";

        // Matchmaking — MatchmakingQueueController @RequestMapping("/matchmaking/queue")
        // + context-path /api → /api/matchmaking/queue/...
        public const string API_MATCHMAKING_QUEUE   = "/api/matchmaking/queue";

        // Friend System
        // Server: FriendController @RequestMapping("/friends") + context-path /api
        // → all friend URLs are /api/friends/...
        public const string API_FRIENDS                    = "/api/friends";
        public const string API_FRIENDS_REMOVE             = "/api/friends/{0}";           // DELETE /friends/{friendUserId}
        public const string API_FRIENDS_SEND_REQUEST       = "/api/friends/requests";       // POST  — body: { username } or { userId }
        public const string API_FRIENDS_INCOMING_REQUESTS  = "/api/friends/requests/incoming";
        public const string API_FRIENDS_OUTGOING_REQUESTS  = "/api/friends/requests/outgoing";
        public const string API_FRIENDS_ACCEPT_REQUEST     = "/api/friends/requests/{0}/accept";
        public const string API_FRIENDS_DECLINE_REQUEST    = "/api/friends/requests/{0}/decline"; // server uses "decline" not "reject"
        public const string API_FRIENDS_CANCEL_REQUEST     = "/api/friends/requests/{0}";  // DELETE — no /cancel suffix
        public const string API_FRIENDS_BLOCKED            = "/api/friends/blocked";        // returns List<Long> (user IDs only)
        public const string API_FRIENDS_BLOCK              = "/api/friends/block/{0}";      // POST path param
        public const string API_FRIENDS_UNBLOCK            = "/api/friends/block/{0}";      // DELETE path param (same path, different verb)

        // Party System
        // Server: PartyController @RequestMapping("/party") + context-path /api
        // → all party URLs are /api/party/...
        public const string API_PARTY                      = "/api/party/current";              // GET current party
        public const string API_PARTY_CREATE               = "/api/party/create";
        public const string API_PARTY_LEAVE                = "/api/party/leave";
        public const string API_PARTY_DISBAND              = "/api/party/disband";
        public const string API_PARTY_INVITE               = "/api/party/invite";               // POST — body: { inviteeUserId }
        public const string API_PARTY_INVITATIONS          = "/api/party/invitations";          // GET pending invitations
        public const string API_PARTY_ACCEPT_INVITATION    = "/api/party/invitations/{0}/accept";
        public const string API_PARTY_DECLINE_INVITATION   = "/api/party/invitations/{0}/decline";
        public const string API_PARTY_CANCEL_INVITE        = "/api/party/invitations/{0}/cancel"; // DELETE cancel sent invite
        public const string API_PARTY_KICK                 = "/api/party/kick/{0}";             // POST path param
        public const string API_PARTY_TRANSFER_LEADER      = "/api/party/transfer-leader";       // POST body: { newLeaderId }
        public const string API_PARTY_QUEUE                = "/api/party/queue";
        public const string API_PARTY_CANCEL_QUEUE         = "/api/party/cancel-queue";
        public const string API_PARTY_JOIN_ROOM            = "/api/party/join-room";

        // Game Config endpoints — populated at startup by GameConfigService
        public const string API_GAME_MODES = "/api/game-modes";

        // Match result (post-match summary — client reports to backend after match ends)
        public const string API_MATCH_RESULT      = "/api/matches/{0}/result";   // reserved / unused by DS
        public const string API_MATCH_END_RANKED  = "/api/match/end/ranked";     // called by DS (server-only)
        public const string API_MATCH_END_CUSTOM  = "/api/match/end/custom";     // called by relay host client
        public const string API_MAPS       = "/api/maps";

        // Game Mode keys — must match GameMode.modeKey in DB
        public const string MODE_2V2 = "2v2";
        public const string MODE_3V3 = "3v3";
        public const string MODE_4V4 = "4v4";
        public const string MODE_5V5 = "5v5";

        // Room Status
        public const string ROOM_STATUS_WAITING = "WAITING";
        public const string ROOM_STATUS_IN_GAME = "IN_GAME";
        public const string ROOM_STATUS_CLOSED = "CLOSED";

        // Teams
        public const int TEAM_1 = 1;
        public const int TEAM_2 = 2;

        // PlayerPrefs Keys
        public const string PREFS_ACCESS_TOKEN           = "NH_ACCESS_TOKEN";
        public const string PREFS_SESSION_ID             = "NH_SESSION_ID";
        public const string PREFS_USER_ID                = "NH_USER_ID";
        public const string PREFS_USERNAME               = "NH_USERNAME";
        public const string PREFS_SELECTED_CHARACTER_ID  = "SelectedCharacterId"; // Read by ClientNetworkHandler
    }
}

