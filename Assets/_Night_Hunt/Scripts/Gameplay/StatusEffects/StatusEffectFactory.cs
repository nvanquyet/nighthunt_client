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
        /// </summary>
        public static IStatusEffect CreateEffect(string statusId, float duration)
        {
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

