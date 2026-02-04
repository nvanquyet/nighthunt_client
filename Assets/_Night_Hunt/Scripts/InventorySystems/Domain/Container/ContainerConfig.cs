using NightHunt.Inventory.Core.Enums;
using UnityEngine;

namespace NightHunt.Inventory.Domain.Container
{
    /// <summary>
    /// ScriptableObject configuration for containers.
    /// </summary>
    [CreateAssetMenu(fileName = "Container_", menuName = "NightHunt/Inventory/Container Config")]
    public class ContainerConfig : ScriptableObject
    {
        [Header("Container Type")] public ContainerType Type;

        [Header("Permissions")] [Tooltip("Can players take items out?")]
        public bool CanTakeOut = true;

        [Tooltip("Can players put items in?")] public bool CanPutIn = false;

        [Header("Capacity (Weight-Based)")] [Tooltip("Maximum weight capacity (no slot count limit)")]
        public float MaxWeight = 50f;

        [Header("Loot Table")] [Tooltip("Items that always spawn")]
        public ItemSpawnRule[] FixedItems;

        [Tooltip("Random item selection")] public LootTableEntry[] RandomItems;

        [Header("Despawn (BossLoot only)")] [Tooltip("Delay before container despawns (seconds)")]
        public float DespawnDelay = 15f;

        [Tooltip("Warning time before despawn (seconds)")]
        public float DespawnWarning = 3f;
    }
}