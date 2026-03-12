using UnityEngine;

namespace NightHunt.StatSystem.Configs
{
    /// <summary>
    /// Consumable stat config - Stats + ConsumableEffects
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableStatConfig", menuName = "NightHunt/StatSystem/Consumable Stat Config")]
    public class ConsumableStatConfig : ItemStatConfig
    {
        [Header("Consumable Effects")]
        [Tooltip("All effects applied when this item finishes being consumed.")]
        public GameplaySystems.Core.Data.ConsumableEffect[] Effects;
    }
}
