using UnityEngine;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Respawn configuration
    /// </summary>
    [CreateAssetMenu(fileName = "RespawnConfig", menuName = "NightHunt/Gameplay/Respawn Config")]
    public class RespawnConfig : ScriptableObject
    {
        [Header("Respawn Rules")]
        [Min(0f)] public float RespawnDelaySeconds = 5f;
        [Min(0f)] public float FinalZoneReviveDelaySeconds = 3f;
        [Tooltip("When the final zone starts, revive every player who died without an available beacon. This happens once per match.")]
        public bool ReviveAllDeadPlayersOnFinalZoneStart = true;

        [Header("Global Respawn Positioning")]
        [Tooltip("Bán kính tối thiểu của Safe Zone khi Respawn trong Phase-3")]
        public float SafeZoneRespawnRadius = 20f;
    }
}
