using NightHunt.Data;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Speed buff/debuff status effect
    /// </summary>
    public class SpeedStatusEffect : StatusEffect
    {
        private CharacterStats characterStats;

        public SpeedStatusEffect(StatusEffectConfigData config, float duration) : base(config, duration)
        {
        }

        public override void OnApply()
        {
            base.OnApply();
            // Apply speed modifier to character stats
            // This would be handled by CharacterStats applying the modifier
        }

        public override void OnRemove()
        {
            base.OnRemove();
            // Remove speed modifier
        }
    }
}

