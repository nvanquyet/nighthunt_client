namespace NightHunt.InteractionSystem.Core
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }
        void TakeDamage(DamagePayload damage);
        void Heal(float amount);
    }
}