using NightHunt.Data;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Silence status effect - noise reduction
    /// </summary>
    public class SilenceStatusEffect : StatusEffect
    {
        public SilenceStatusEffect(StatusEffectConfigData config, float duration) : base(config, duration)
        {
        }

        public override void OnApply()
        {
            base.OnApply();
            // Apply noise reduction modifier
            // Handled by CharacterStats
        }

        public override void OnRemove()
        {
            base.OnRemove();
            // Remove noise reduction modifier
        }
    }
}

