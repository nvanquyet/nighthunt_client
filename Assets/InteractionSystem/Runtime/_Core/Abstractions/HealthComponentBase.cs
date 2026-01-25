using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
    public abstract class HealthComponentBase : NetworkBehaviour, IDamageable
    {
        [SyncVar] protected float currentHealth;
        [SerializeField] protected float maxHealth = 100f;
    
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0;
    
        public override void OnStartServer()
        {
            base.OnStartServer();
            currentHealth = maxHealth;
        }
    
        [Server]
        public virtual void TakeDamage(DamagePayload damage)
        {
            if (IsDead) return;
        
            float finalDamage = CalculateDamage(damage);
            currentHealth -= finalDamage;
        
            OnDamageTaken(damage, finalDamage);
        
            if (IsDead)
            {
                OnDeath(damage.attacker);
            }
        }
    
        [Server]
        public virtual void Heal(float amount)
        {
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }
    
        protected abstract float CalculateDamage(DamagePayload damage);
        protected abstract void OnDamageTaken(DamagePayload damage, float finalDamage);
        protected abstract void OnDeath(NetworkConnection killer);
    }
}