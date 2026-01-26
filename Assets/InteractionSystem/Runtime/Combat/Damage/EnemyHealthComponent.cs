using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Items;
using UnityEngine;

namespace NightHunt.InteractionSystem.Combat
{
    public class EnemyHealthComponent : HealthComponentBase
{
    [Header("Enemy Health")]
    [SerializeField] private float enemyMaxHealth = 150f;
    
    [Header("Loot")]
    [SerializeField] private ItemDropTable dropTable;
    
    public override void OnStartServer()
    {
        maxHealth = enemyMaxHealth;
        base.OnStartServer();
    }
    
    protected override float CalculateDamage(DamagePayload damage)
    {
        // Simple damage calculation for enemies
        return damage.rawDamage;
    }
    
    protected override void OnDamageTaken(DamagePayload damage, float finalDamage)
    {
        // Visual feedback
        ObserversShowDamageEffect(damage.hitPoint);
    }
    
    protected override void OnDeath(NetworkConnection killer)
    {
        // Drop loot
        DropLoot();
        
        // Despawn enemy
        Invoke(nameof(DespawnEnemy), 2f);
    }
    
    [Server]
    private void DropLoot()
    {
        if (dropTable == null) return;
        
        ItemInstance[] items = dropTable.GenerateLoot();
        
        ItemDropService.Instance?.DropItemsInRadius(items, transform.position);
    }
    
    [Server]
    private void DespawnEnemy()
    {
        ServerManager.Despawn(gameObject);
    }
    
    [ObserversRpc]
    private void ObserversShowDamageEffect(Vector3 hitPoint)
    {
        // TODO: Blood particle effect
    }
}

// Drop table system
[CreateAssetMenu(fileName = "DropTable", menuName = "NightHunt/Loot/Drop Table")]
public class ItemDropTable : ScriptableObject
{
    [System.Serializable]
    public class DropEntry
    {
        public string itemDataId;
        [Range(0f, 1f)] public float dropChance = 0.5f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }
    
    public DropEntry[] possibleDrops;
    
    public ItemInstance[] GenerateLoot()
    {
        List<ItemInstance> loot = new List<ItemInstance>();
        
        foreach (var entry in possibleDrops)
        {
            if (UnityEngine.Random.value <= entry.dropChance)
            {
                int quantity = UnityEngine.Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                ItemInstance item = ItemInstanceFactory.CreateInstance(entry.itemDataId, quantity);
                loot.Add(item);
            }
        }
        
        return loot.ToArray();
    }
}

}