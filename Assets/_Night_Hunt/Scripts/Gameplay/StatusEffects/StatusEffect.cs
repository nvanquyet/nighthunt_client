using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.StatusEffects
{
    /// <summary>
    /// Base status effect class
    /// </summary>
    public abstract class StatusEffect : IStatusEffect
    {
        public string StatusId { get; protected set; }
        public string Description { get; protected set; }
        public float Duration { get; protected set; }
        public float TimeRemaining { get; protected set; }
        public bool IsExpired => TimeRemaining <= 0f;

        protected StatusEffectConfigData config;

        public StatusEffect(StatusEffectConfigData config, float duration)
        {
            this.config = config;
            StatusId = config.StatusId;
            Description = config.Description;
            Duration = duration;
            TimeRemaining = duration;
        }

        public virtual void OnApply()
        {
            // Override in derived classes
        }

        public virtual void OnUpdate(float deltaTime)
        {
            TimeRemaining -= deltaTime;
        }

        public virtual void OnRemove()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Refresh duration (for non-stackable effects)
        /// </summary>
        public void RefreshDuration(float newDuration)
        {
            Duration = newDuration;
            TimeRemaining = newDuration;
        }
    }
}

