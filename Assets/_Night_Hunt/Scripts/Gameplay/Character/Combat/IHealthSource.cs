using System;

namespace NightHunt.Gameplay.Character.Combat
{
    public readonly struct HealthChangeEvent
    {
        public HealthChangeEvent(
            float previousHealth,
            float currentHealth,
            float maxHealth,
            int instigatorNetworkObjectId = 0,
            bool forceReveal = false)
        {
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            InstigatorNetworkObjectId = instigatorNetworkObjectId;
            ForceReveal = forceReveal;
        }

        public float PreviousHealth { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public int InstigatorNetworkObjectId { get; }
        public bool ForceReveal { get; }
        public bool IsDamage => ForceReveal || CurrentHealth < PreviousHealth;
    }

    public interface IHealthSource
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }
        event Action<HealthChangeEvent> HealthChanged;
    }
}
