using NightHunt.Data;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Blind status effect - vision reduction
    /// </summary>
    public class BlindStatusEffect : StatusEffect
    {
        public BlindStatusEffect(StatusEffectConfigData config, float duration) : base(config, duration)
        {
        }

        public override void OnApply()
        {
            base.OnApply();
            // Apply vision reduction modifier
            // Handled by CharacterStats
        }

        public override void OnRemove()
        {
            base.OnRemove();
            // Remove vision reduction modifier
        }
    }
}

