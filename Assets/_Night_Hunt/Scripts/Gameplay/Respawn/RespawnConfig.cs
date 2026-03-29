using UnityEngine;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Respawn configuration
    /// </summary>
    [CreateAssetMenu(fileName = "RespawnConfig", menuName = "NightHunt/Gameplay/Respawn Config")]
    public class RespawnConfig : ScriptableObject
    {
        [Header("Global Respawn Positioning")]
        [Tooltip("Bán kính tối thiểu của Safe Zone khi Hồi sinh trong Phase-3")]
        public float SafeZoneRespawnRadius = 20f;
    }
}
