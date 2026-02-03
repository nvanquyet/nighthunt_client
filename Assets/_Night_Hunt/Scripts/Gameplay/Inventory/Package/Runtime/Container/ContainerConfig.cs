using System;
using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Container
{
    /// <summary>
    /// Configuration for container types.
    /// </summary>
    [CreateAssetMenu(fileName = "Container_", menuName = "Inventory/ContainerConfig")]
    public class ContainerConfig : ScriptableObject
    {
        [Header("Container Type")]
        public ContainerType Type;
        
        [Header("Permissions")]
        public bool CanTakeOut = true;
        public bool CanPutIn = false;   // Most containers: loot only
        
        [Header("Capacity (Weight-Based)")]
        public float MaxWeight = 50f;   // No slot count limit, only weight
        
        [Header("Loot Table")]
        public ItemSpawnRule[] FixedItems;  // Always spawn these
        public LootTableEntry[] RandomItems; // Random selection
        
        [Header("Despawn (BossLoot only)")]
        public float DespawnDelay = 15f;
    }
    
    [Serializable]
    public class ItemSpawnRule
    {
        public ItemDefinition Item;
        public int Quantity = 1;
        [Range(0f, 100f)]
        public float DurabilityPercent = 100f;
    }
    
    [Serializable]
    public class LootTableEntry
    {
        public ItemDefinition Item;
        [Range(0f, 1f)]
        public float SpawnChance; // 0-1
        public int MinQuantity;
        public int MaxQuantity;
        [Range(0f, 100f)]
        public float MinDurability = 50f;
        [Range(0f, 100f)]
        public float MaxDurability = 100f;
    }
}
