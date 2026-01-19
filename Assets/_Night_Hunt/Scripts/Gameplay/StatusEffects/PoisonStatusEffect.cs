using NightHunt.Data;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Poison status effect - damage over time
    /// </summary>
    public class PoisonStatusEffect : StatusEffect
    {
        private CharacterStats characterStats;
        private float damagePerSecond;

        public PoisonStatusEffect(StatusEffectConfigData config, float duration) : base(config, duration)
        {
            damagePerSecond = config.Value; // Damage per second
        }

        public override void OnApply()
        {
            base.OnApply();
            characterStats = UnityEngine.Object.FindObjectOfType<CharacterStats>();
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);

            // Apply damage over time
            if (characterStats != null && config.Operation == "DamageOverTime")
            {
                float damage = damagePerSecond * deltaTime;
                characterStats.TakeDamage(damage);
            }
        }
    }
}

