using System;

namespace NightHunt.Networking.Player
{
    /// <summary>
    /// PRIVATE SERVER DATA - RegistryService giữ
    /// Chứa sensitive info như BackendPlayerId
    /// </summary>
    [Serializable]
    public struct PlayerRegistryData
    {
        public string BackendPlayerId;           // PRIVATE - Backend database ID
        public string DisplayName;
        public int TeamId;
        public PlayerConnectionStatus Status;
        // Extended fields (ELO, level, etc.) added here when matchmaking data is surfaced to DS.
        
        public static PlayerRegistryData GetData()
        {
            return new PlayerRegistryData
            {
                BackendPlayerId = "1122334455", // Placeholder
                DisplayName = "1122334455",
                TeamId = 0, 
                Status = PlayerConnectionStatus.Connected
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
        // Extended public fields (level, avatar) added when inventory/profile is surfaced.
        
        /// <summary>
        /// Convert từ private data → public data
        /// </summary>
        public static PlayerPublicData FromRegistryData(PlayerRegistryData privateData)
        {
            return new PlayerPublicData
            {
                DisplayName = privateData.DisplayName,
                TeamId = privateData.TeamId,
                Status = privateData.Status
            };
        }
        
        public static PlayerPublicData GetData()
        {
            return new PlayerPublicData
            {
                DisplayName = "1122334455",
                TeamId = 0, 
                Status = PlayerConnectionStatus.Connected
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