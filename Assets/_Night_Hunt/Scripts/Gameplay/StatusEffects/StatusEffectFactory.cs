using NightHunt.Data;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Factory pattern to create effects from config
    /// </summary>
    public static class StatusEffectFactory
    {
        /// <summary>
        /// Create status effect from config
        /// TODO: Implement StatusEffectConfig ScriptableObject system to replace GameConfigLoader
        /// </summary>
        public static IStatusEffect CreateEffect(string statusId, float duration)
        {
            // TODO: Load status effect config from ScriptableObject registry
            // For now, status effect creation is disabled
            UnityEngine.Debug.LogWarning($"[StatusEffectFactory] CreateEffect({statusId}) - Status effect system needs StatusEffectConfig ScriptableObject implementation");
            return null;
            
            /* OLD CODE - REMOVED (GameConfigLoader dependency)
            var config = GameConfigLoader.Instance?.GetStatusEffectConfig(statusId);
            if (config == null)
            {
                UnityEngine.Debug.LogWarning($"[StatusEffectFactory] Status effect config not found: {statusId}");
                return null;
            }

            // Create appropriate effect type based on config
            switch (config.StatusType)
            {
                case "Speed":
                    return new SpeedStatusEffect(config, duration);
                case "Poison":
                    return new PoisonStatusEffect(config, duration);
                case "Blind":
                    return new BlindStatusEffect(config, duration);
                case "Silence":
                    return new SilenceStatusEffect(config, duration);
                default:
                    return new GenericStatusEffect(config, duration);
            }
            */
        }
    }

    /// <summary>
    /// Generic status effect for unknown types
    /// </summary>
    public class GenericStatusEffect : StatusEffect
    {
        public GenericStatusEffect(StatusEffectConfigData config, float duration) : base(config, duration)
        {
        }
    }
}

