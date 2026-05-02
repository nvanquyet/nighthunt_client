using System;

namespace NightHunt.Networking.Player
{
    /// <summary>
    /// PRIVATE SERVER DATA - RegistryService giữ
    /// Contains sensitive info như BackendPlayerId
    /// </summary>
    [Serializable]
    public struct PlayerRegistryData
    {
        public string BackendPlayerId;           // PRIVATE - Backend database ID
        public string DisplayName;
        public int TeamId;
        public PlayerConnectionStatus Status;
        /// <summary>
        /// Index into PlayerModelLoader._modelPrefabs on the PlayerPrefab.
        /// 0 = default (Soldier_White). Sent by the client during login.
        /// </summary>
        public int CharacterModelIndex;
        // Extended fields (ELO, level, etc.) added here when matchmaking data is surfaced to DS.
        
        public static PlayerRegistryData GetData()
        {
            return new PlayerRegistryData
            {
                BackendPlayerId      = "1122334455", // Placeholder
                DisplayName          = "1122334455",
                TeamId               = 0, 
                Status               = PlayerConnectionStatus.Connected,
                CharacterModelIndex  = 0
            };
        }
    }
    
    /// <summary>
    /// PUBLIC DATA - NetworkPlayer sync
    /// Chỉ chứa info clients được phép thấy
    /// </summary>
    [Serializable]
    public struct PlayerPublicData
    {
        public string DisplayName;      // Public - mọi người thấy được
        public int TeamId;              // Public - team hiện tại
        public PlayerConnectionStatus Status;
        /// <summary>
        /// Index into PlayerModelLoader._modelPrefabs — replicated so every
        /// client renders the correct character skin for each player.
        /// </summary>
        public int CharacterModelIndex;
        // Extended public fields (level) added when inventory/profile is surfaced.
        
        /// <summary>
        /// Convert từ private data → public data
        /// </summary>
        public static PlayerPublicData FromRegistryData(PlayerRegistryData privateData)
        {
            return new PlayerPublicData
            {
                DisplayName         = privateData.DisplayName,
                TeamId              = privateData.TeamId,
                Status              = privateData.Status,
                CharacterModelIndex = privateData.CharacterModelIndex
            };
        }
        
        public static PlayerPublicData GetData()
        {
            return new PlayerPublicData
            {
                DisplayName         = "1122334455",
                TeamId              = 0, 
                Status              = PlayerConnectionStatus.Connected,
                CharacterModelIndex = 0
            };
        }
    }
    
    public enum PlayerConnectionStatus
    {
        Disconnected,
        Connected,
        InGame,
        Reconnecting
    }
}