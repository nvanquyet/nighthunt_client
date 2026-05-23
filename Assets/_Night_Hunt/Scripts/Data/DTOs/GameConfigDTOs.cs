using System;

namespace NightHunt.Data.DTOs
{
    // ══════════════════════════════════════════════════════════════════════════
    // GAME CONFIG DTOs
    // Mirror of backend GameModeDTO / GameMapDTO (Java).
    // JsonUtility field names must match JSON keys exactly.
    //
    // Fetch order: GET /api/game-modes  →  GameModeResponseDTO[]
    //              GET /api/maps        →  GameMapResponseDTO[]
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Mirror of com.nighthunt.gamemode.dto.GameModeDTO.
    /// Backend returns these inside ApiResponse{"success":true,"data":[...]}.
    /// </summary>
    [Serializable]
    public class GameModeResponseDTO
    {
        public long   id;
        public string modeKey;             // "2v2", "3v3", "4v4", "5v5"
        public string displayName;         // "2 vs 2"
        public string description;
        public int    playersPerTeam;
        public int    totalPlayers;
        public bool   allowFill;
        public bool   matchmakingEnabled;
        public int    minElo;
        public int    maxElo;
        public string modeStatus;          // "AVAILABLE" | "LOCKED" | "COMING_SOON" | "DISABLED"
        public int    displayOrder;
        public bool   isActive;            // @JsonProperty("isActive") on backend → key "isActive"
        public bool   isDevMode;           // @JsonProperty("isDevMode") on backend → key "isDevMode"
    }

    /// <summary>
    /// Mirror of com.nighthunt.map.dto.GameMapDTO.
    /// Backend returns these inside ApiResponse{"success":true,"data":[...]}.
    /// </summary>
    [Serializable]
    public class GameMapResponseDTO
    {
        public string   mapId;             // "map_01"
        public string   displayName;       // "Industrial Zone"
        public string   description;
        public string   sceneName;         // Unity scene file name, e.g. "02_Map_01" — matched via SceneConfig reverse lookup
        public string[] supportedModes;    // null = all modes; non-null = ["2v2","3v3"]
        public int[]    supportedPlayerCounts; // null = no restriction; [2,4] = only 2 or 4 players
        public bool     isLocked;          // @JsonProperty("isLocked") on backend → key "isLocked"
        public int      displayOrder;
    }
}
