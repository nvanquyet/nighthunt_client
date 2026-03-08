using UnityEngine;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Respawn configuration
    /// </summary>
    [CreateAssetMenu(fileName = "RespawnConfig", menuName = "NightHunt/Gameplay/Respawn Config")]
    public class RespawnConfig : ScriptableObject
    {
        [Header("Respawn Delays")]
        public float Phase1RespawnDelay = 5f;
        public float Phase2RespawnDelay = 5f;
        public float Phase3RespawnDelay = 3f;

        [Header("Beacon Settings")]
        public float BeaconPlacementTime = 3f;
        public float BeaconLifetime = 300f; // 5 minutes
        public int MaxBeaconsPerTeam = 3;

        [Header("Zone Settings")]
        public float SafeZoneRespawnRadius = 20f;
    }
}
