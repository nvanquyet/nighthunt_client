using UnityEngine;
using NightHunt.InteractionSystem.Loot.Spawn;
using NightHunt.InteractionSystem.Items.Runtime;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Configuration for item spawning (fixed settings only).
    /// Contains prefab, spawn radius, and visual effects.
    /// LootTable and spawn timing are configured per LootSpawnPoint.
    /// </summary>
    [CreateAssetMenu(fileName = "LootSpawnConfig", menuName = "NightHunt/InteractionSystem/Loot/Loot Spawn Config", order = 1)]
    public class LootSpawnConfig : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Generic NetworkLootItem prefab to spawn (will be initialized with loot data)")]
        [SerializeField] private NetworkLootItem lootItemPrefab;

        [Header("Spawn Settings")]
        [Tooltip("Radius around spawn point to randomly place items")]
        [SerializeField] private float spawnRadius = 1f;

        [Header("Visual")]
        [Tooltip("Spawn effect prefab (optional)")]
        [SerializeField] private GameObject spawnEffectPrefab;

        public NetworkLootItem LootItemPrefab => lootItemPrefab;
        public float SpawnRadius => spawnRadius;
        public GameObject SpawnEffectPrefab => spawnEffectPrefab;
    }

    /// <summary>
    /// Spawn mode for loot items.
    /// </summary>
    public enum SpawnMode
    {
        Once,           // Spawn once when scene starts
        Interval,        // Spawn every N seconds
        OnDemand        // Spawn manually via code
    }
}
