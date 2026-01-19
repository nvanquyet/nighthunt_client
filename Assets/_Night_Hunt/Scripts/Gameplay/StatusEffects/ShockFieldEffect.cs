using NightHunt.Data;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Shock field effect - slow effect from zone
    /// </summary>
    public class ShockFieldEffect : StatusEffect
    {
        public ShockFieldEffect(StatusEffectConfigData config, float duration) : base(config, duration)
        {
        }

        public override void OnApply()
        {
            base.OnApply();
            // Apply slow modifier (speed reduction)
            // Handled by CharacterStats
        }

        public override void OnRemove()
        {
            base.OnRemove();
            // Remove slow modifier
        }
    }
}

