using System;
using System.Collections.Generic;

namespace NightHunt.Data.DTOs
{
    [Serializable]
    public class CreateRoomRequest
    {
        public string mode; // 2v2, 3v3, 5v5
        public bool isPublic = true;
        public bool isLocked = false;
        public string password; // Optional password for room
    }

    [Serializable]
    public class JoinRoomRequest
    {
        public string roomCode;
        public string password; // Required if room has password
    }

    [Serializable]
    public class QuickPlayRequest
    {
        public string mode;
    }

    [Serializable]
    public class ReadyRequest
    {
        public bool isReady;
    }

    [Serializable]
    public class ChangeTeamRequest
    {
        public int team; // 1 or 2
        public int slot; // position in team
    }

    [Serializable]
    public class ReconnectRequest
    {
        public string accessToken;
        public string sessionId;
        public long? roomId;
    }

    [Serializable]
    public class RoomResponse
    {
        public long roomId;
        public string roomCode;
        public string mode;
        public string mapId;
        public string status;
        public bool isPublic;
        public bool isLocked;
        public long ownerId;
        public string serverIp;
        public int serverPort;
        public string matchId;
        public string joinToken;
        public List<RoomPlayerResponse> players;
    }

    [Serializable]
    public class RoomPlayerResponse
    {
        public long userId;
        public string username;
        public int team;
        public int slot;
        public bool isReady;
    }

    [Serializable]
    public class SwapRequestRequest
    {
        public long targetUserId;
        public int targetTeam;
        public int targetSlot;
    }

    [Serializable]
    public class SwapRequestDTO
    {
        public long requestId;
        public long requesterId;
        public string requesterUsername;
        public int requesterTeam;
        public int requesterSlot;
        public long targetUserId;
        public int targetTeam;
        public int targetSlot;
        public string status;
    }

    /// <summary>
    /// POST /api/rooms/{id}/update-settings
    /// Always send the full current state — use BuildSettings() in CustomLobbyView.
    /// JsonUtility does NOT serialize Nullable&lt;bool&gt; so all fields must be non-nullable.
    /// Server performs partial update: only non-null fields are changed — but since C#
    /// always sends all fields, the server will apply them all as expected.
    /// </summary>
    [Serializable]
    public class UpdateRoomSettingsRequest
    {
        public string mode;             // required — always send current or new value
        public string mapId;            // required — always send current or new value
        public bool   isPublic = true;  // required — always send current or new value
        public bool   isLocked = false; // required — always send current or new value
        public string password;         // optional — null/empty = keep existing password
    }

    [Serializable]
    public class TransferOwnerRequest
    {
        public long targetUserId;
    }
}

