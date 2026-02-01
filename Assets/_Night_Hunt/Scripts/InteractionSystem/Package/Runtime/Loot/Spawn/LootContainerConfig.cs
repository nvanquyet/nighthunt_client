using UnityEngine;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Loot.Spawn;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Configuration for container spawning (fixed settings only).
    /// Contains prefab, max slots, lock state, name, interaction type, and visual effects.
    /// LootTable, container mode, and generation settings are configured per container/point.
    /// </summary>
    [CreateAssetMenu(fileName = "LootContainerConfig", menuName = "NightHunt/InteractionSystem/Loot/Container Config", order = 2)]
    public class LootContainerConfig : ScriptableObject
    {
        [Header("Container Prefab")]
        [Tooltip("NetworkLootContainer prefab to spawn")]
        [SerializeField] private NetworkLootContainer containerPrefab;

        [Header("Container Settings")]
        [Tooltip("Maximum number of slots in container")]
        [SerializeField] private int maxSlots = 12;
        
        [Tooltip("Is container locked?")]
        [SerializeField] private bool isLocked = false;
        
        [Tooltip("Container display name")]
        [SerializeField] private string containerName = "Container";

        [Header("Spawn Settings")]
        [Tooltip("Radius around spawn point to randomly place containers")]
        [SerializeField] private float spawnRadius = 1f;

        [Header("Visual")]
        [Tooltip("Spawn effect prefab (optional)")]
        [SerializeField] private GameObject spawnEffectPrefab;

        public NetworkLootContainer ContainerPrefab => containerPrefab;
        public int MaxSlots => maxSlots;
        public bool IsLocked => isLocked;
        public string ContainerName => containerName;
        public float SpawnRadius => spawnRadius;
        public GameObject SpawnEffectPrefab => spawnEffectPrefab;
    }
}
