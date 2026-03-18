using System;
using System.Collections.Generic;

namespace NightHunt.Data.DTOs
{
    // ══════════════════════════════════════════════════════════════════════════
    // FRIEND SYSTEM DTOs
    // Server ref: FriendController, FriendService (Java)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/friends/requests
    /// Server AddFriendRequest accepts username OR userId (either/or).
    /// Primary flow: send by username (search-and-add).
    /// </summary>
    [Serializable]
    public class SendFriendRequestRequest
    {
        public string username;   // send by exact username (primary)
        public long   userId;     // send by userId (alternative, 0 = not set)
    }

    [Serializable]
    public class RemoveFriendRequest
    {
        public long friendId;
    }

    [Serializable]
    public class BlockUserRequest
    {
        public long targetUserId;
    }

    [Serializable]
    public class UnblockUserRequest
    {
        public long targetUserId;
    }

    /// <summary>
    /// Response from GET /api/friends
    /// Maps to server FriendDTO exactly.
    /// </summary>
    [Serializable]
    public class FriendResponse
    {
        // Server fields (FriendDTO):
        public long   friendId;          // id of the friendship row for this friend
        public long   userId;            // the friend's userId — NOT the authenticated user [WAS: "authenticated user's id" — WRONG comment, FIXED]
        public string username;          // friend's username  [WAS: friendUsername — FIXED]
        public string onlineStatus;      // "ONLINE" | "OFFLINE" | "AWAY" | "IN_GAME"
        public string lastSeenAt;        // ISO datetime string (nullable)
        public string friendsSince;      // ISO datetime string
        public string friendshipStatus;  // "ACTIVE" | "BLOCKED"
        public long   currentPartyId;    // 0 if not in a party
        public long   currentRoomId;     // 0 if not in a room

        // Convenience helpers (not from server)
        public bool IsOnline   => onlineStatus == "ONLINE";
        public bool IsInGame   => onlineStatus == "IN_GAME";
        public bool IsInParty  => currentPartyId != 0;
    }

    /// <summary>
    /// Response from GET /api/friends/requests/incoming or /outgoing
    /// Maps to server FriendRequestDTO exactly.
    /// </summary>
    [Serializable]
    public class FriendRequestResponse
    {
        // Server fields (FriendRequestDTO):
        public long   requestId;          // [WAS: id — FIXED]
        public long   requesterUserId;    // [WAS: senderId — FIXED]
        public string requesterUsername;  // [WAS: senderUsername — FIXED]
        public long   addresseeUserId;    // [WAS: receiverId — FIXED]
        public string addresseeUsername;
        public string requestStatus;      // "PENDING"|"ACCEPTED"|"DECLINED"|"CANCELLED" [WAS: "REJECTED" — FIXED]
        public string expiresAt;          // ISO datetime string (nullable)
        public string createdAt;
    }

    /// <summary>
    /// Response from GET /api/friends/blocked
    /// Server returns List&lt;Long&gt; (just user IDs), but we store as this DTO for UI display.
    /// The blocked list endpoint only returns IDs — username must be fetched separately or omitted.
    /// </summary>
    [Serializable]
    public class BlockedUserResponse
    {
        public long   blockedUserId;
        public string blockedUsername;   // populated client-side if available, else empty
    }

    /// <summary>
    /// Wrapper combining incoming + outgoing friend requests.
    /// Built client-side from two separate API calls.
    /// </summary>
    [Serializable]
    public class PendingRequestsResponse
    {
        public List<FriendRequestResponse> received; // Incoming (addressee = me)
        public List<FriendRequestResponse> sent;     // Outgoing (requester = me)
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PARTY SYSTEM DTOs
    // Server ref: PartyController, PartyService (Java)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/party/invite
    /// Server InviteToPartyRequest expects field "inviteeUserId".
    /// </summary>
    [Serializable]
    public class InviteToPartyRequest
    {
        public long inviteeUserId;   // [WAS: inviteeId — FIXED]
    }

    [Serializable]
    public class PartyMatchmakingRequest
    {
        public string gameMode;        // "2v2" | "3v3" | "4v4" | "5v5"
        public bool   allowFill = true;
        public string mapId;           // optional — null = any map
    }

    [Serializable]
    public class JoinRoomWithPartyRequest
    {
        public string roomCode;
        public string password; // optional
    }

    /// <summary>
    /// Response from POST /api/party/create, GET /api/party/current, etc.
    /// Maps to server PartyDTO exactly.
    /// </summary>
    [Serializable]
    public class PartyResponse
    {
        // Server fields (PartyDTO):
        public long   partyId;              // [WAS: id — FIXED]
        public long   hostUserId;           // [WAS: leaderId — FIXED]
        public string hostUsername;
        public string partyStatus;          // "IDLE"|"IN_QUEUE"|"IN_ROOM"|"IN_GAME"|"DISBANDED" [WAS: status — FIXED]
        public int    maxMembers;
        public int    currentMemberCount;
        public string createdAt;
        public List<PartyMemberResponse> members;

        // Convenience helpers
        public bool IsInQueue => partyStatus == "IN_QUEUE";
        public bool IsInRoom  => partyStatus == "IN_ROOM";
    }

    /// <summary>
    /// Maps to server PartyMemberDTO exactly.
    /// </summary>
    [Serializable]
    public class PartyMemberResponse
    {
        // Server fields (PartyMemberDTO):
        public long   userId;
        public string username;
        public string onlineStatus;         // [WAS: status — FIXED to match server "onlineStatus"]
        public int    joinOrder;            // 0 = host, 1/2/3 = guests  [NEW — was missing]
        public bool   isHost;              // [WAS: isLeader — FIXED]
        public string joinedAt;
        public string selectedCharacterId; // Phase 2 — server not yet sending; always null for now
    }

    /// <summary>
    /// Response from POST /api/party/invite, GET /api/party/invitations
    /// Maps to server PartyInvitationDTO exactly.
    /// </summary>
    [Serializable]
    public class PartyInviteResponse
    {
        // Server fields (PartyInvitationDTO):
        public long   invitationId;        // [WAS: id — FIXED]
        public long   partyId;
        public long   inviterUserId;       // [WAS: inviterId — FIXED]
        public string inviterUsername;
        public long   inviteeUserId;       // [WAS: inviteeId — FIXED]
        public string inviteeUsername;
        public string invitationStatus;    // "PENDING"|"ACCEPTED"|"DECLINED"|"EXPIRED"|"CANCELLED" [WAS: status — FIXED]
        public int    secondsRemaining;    // countdown before invite expires [NEW]
        public string expiresAt;
        public string createdAt;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GAME MODE CONFIG DTOs
    // Server ref: GameModeController (GET /api/game-modes)
    // ══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class GameModeResponse
    {
        public long   id;
        public string modeKey;        // "2v2" | "3v3" | "4v4" | "5v5"
        public string displayName;
        public int    maxPlayers;
        public int    playersPerTeam;
        public bool   isRanked;
        public bool   isEnabled;
        public string status;         // "ACTIVE" | "COMING_SOON" | "DISABLED"
    }
}
