using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Interaction.Implementations;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Spawn point for containers (chests, crates, etc.).
    /// Similar to LootSpawnPoint but spawns NetworkLootContainer prefabs instead of items.
    /// </summary>
    public class LootContainerPoint : NetworkBehaviour
    {
        [Header("Container Config")]
        [Tooltip("Configuration for prefab, max slots, and visual effects (fixed settings)")]
        [SerializeField] private LootContainerConfig config;

        [Header("Loot Table")]
        [Tooltip("LootTable to generate items from (per-point configuration)")]
        [SerializeField] private LootTable lootTable;

        [Header("Container Mode")]
        [Tooltip("How should this container generate items?")]
        [SerializeField] private LootContainerMode containerMode = LootContainerMode.Random;

        [Header("Pre-Placed Items (Fixed/Hybrid Mode)")]
        [Tooltip("Fixed items that will always be in this container (for Fixed or Hybrid mode)")]
        [SerializeField] private List<ItemInstance> initialItems = new List<ItemInstance>();

        [Header("Loot Generation")]
        [Tooltip("Generate loot when container is first opened (if empty)?")]
        [SerializeField] private bool generateLootOnFirstOpen = true;

        [Header("Pre-Generation")]
        [Tooltip("Pre-generate loot on game start? (instead of on first open)")]
        [SerializeField] private bool preGenerateOnStart = false;

        [Header("Loot Generation Override")]
        [Tooltip("Override LootTable's minItemsPerSpawn? (0 = use LootTable default)")]
        [SerializeField] private int overrideMinItems = 0;

        [Tooltip("Override LootTable's maxItemsPerSpawn? (0 = use LootTable default)")]
        [SerializeField] private int overrideMaxItems = 0;

        [Header("Interaction Settings")]
        [Tooltip("Interaction type: Immediate (instant), Hold (hold to open), or Container (default)")]
        [SerializeField] private InteractionType interactionType = InteractionType.Container;
        
        [Tooltip("Required hold time for Hold interaction type (seconds)")]
        [SerializeField] private float requiredHoldTime = 0f;

        [Header("Container Permissions")]
        [Tooltip("Container access mode: ReadOnly (only remove), WriteOnly (only add), ReadWrite (both), None (no access)")]
        [SerializeField] private ContainerAccessMode containerAccessMode = ContainerAccessMode.ReadWrite;
        
        [Tooltip("Allow players to add items to this container? (Legacy - use containerAccessMode instead)")]
        [SerializeField] private bool allowAddItems = true;
        
        [Tooltip("Allow players to remove items from this container? (Legacy - use containerAccessMode instead)")]
        [SerializeField] private bool allowRemoveItems = true;

        [Header("Spawn Timing")]
        [Tooltip("When should containers spawn?")]
        [SerializeField] private SpawnMode spawnMode = SpawnMode.Once;
        
        [Tooltip("Respawn interval in seconds (for Interval mode)")]
        [SerializeField] private float respawnInterval = 300f;
        
        [Tooltip("Initial delay before first spawn")]
        [SerializeField] private float initialDelay = 0f;

        [Header("Visual")]
        [SerializeField] private bool showGizmos = true;

        private float lastSpawnTime = 0f;
        private bool hasSpawned = false;
        private List<NetworkLootContainer> spawnedContainers = new List<NetworkLootContainer>();

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (config == null)
            {
                Debug.LogError("[LootContainerPoint] Container config is not assigned!");
                return;
            }

            if (config.ContainerPrefab == null)
            {
                Debug.LogError("[LootContainerPoint] Container prefab is not assigned in config!");
                return;
            }

            if (spawnMode == SpawnMode.Once || spawnMode == SpawnMode.Interval)
            {
                Invoke(nameof(SpawnContainer), initialDelay);
            }
        }

        private void Update()
        {
            if (!IsServer)
                return;

            if (spawnMode != SpawnMode.Interval || !hasSpawned)
                return;

            // Check if all spawned containers have been emptied/despawned
            bool allEmpty = true;
            foreach (var container in spawnedContainers)
            {
                if (container != null && container.IsSpawned && !container.IsEmpty())
                {
                    allEmpty = false;
                    break;
                }
            }

            // If all empty and enough time has passed, respawn
            if (allEmpty && Time.time - lastSpawnTime >= respawnInterval)
            {
                SpawnContainer();
            }
        }

        /// <summary>
        /// Spawn container based on config.
        /// </summary>
        [Server]
        public void SpawnContainer()
        {
            if (config == null)
            {
                Debug.LogError("[LootContainerPoint] Container config is not assigned!");
                return;
            }

            if (config.ContainerPrefab == null)
            {
                Debug.LogError("[LootContainerPoint] Container prefab is not assigned in config!");
                return;
            }

            // Clear old spawned containers list
            spawnedContainers.Clear();

            // Calculate spawn position (random within radius)
            float radius = config != null ? config.SpawnRadius : 1f;
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Spawn container prefab
            NetworkLootContainer container = Instantiate(config.ContainerPrefab, spawnPosition, Quaternion.identity);
            NetworkObject containerNO = container.GetComponent<NetworkObject>();
            if (containerNO == null)
            {
                Debug.LogError("[LootContainerPoint] Container prefab must have a NetworkObject component.");
                Destroy(container.gameObject);
                return;
            }

            Spawn(containerNO);

            // Initialize container with settings from point (must be called after Spawn)
            InitializeContainerFromConfig(container, config);
            
            // Generate items immediately if pre-generate is enabled
            if (preGenerateOnStart && container.ShouldPreGenerate())
            {
                container.PreGenerateLoot();
            }
            
            spawnedContainers.Add(container);

            lastSpawnTime = Time.time;
            hasSpawned = true;

            // Spawn visual effect
            if (config != null && config.SpawnEffectPrefab != null)
            {
                GameObject effect = Instantiate(config.SpawnEffectPrefab, spawnPosition, Quaternion.identity);
                NetworkObject effectNO = effect.GetComponent<NetworkObject>();
                if (effectNO != null)
                {
                    Spawn(effectNO);
                }
            }

            string containerName = config != null ? config.ContainerName : "Container";
            Debug.Log($"[LootContainerPoint] Spawned container: {containerName}");
        }

        /// <summary>
        /// Initialize container with settings from point.
        /// All settings come from this point, config only provides prefab/visual settings.
        /// </summary>
        [Server]
        private void InitializeContainerFromConfig(NetworkLootContainer container, LootContainerConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[LootContainerPoint] Config is null, cannot initialize container!");
                return;
            }
            
            // Convert ContainerAccessMode to bool flags
            bool finalAllowAdd = allowAddItems;
            bool finalAllowRemove = allowRemoveItems;
            
            // Override with enum if using enum mode
            switch (containerAccessMode)
            {
                case ContainerAccessMode.ReadOnly:
                    finalAllowAdd = false;
                    finalAllowRemove = true;
                    break;
                case ContainerAccessMode.WriteOnly:
                    finalAllowAdd = true;
                    finalAllowRemove = false;
                    break;
                case ContainerAccessMode.ReadWrite:
                    finalAllowAdd = true;
                    finalAllowRemove = true;
                    break;
                case ContainerAccessMode.None:
                    finalAllowAdd = false;
                    finalAllowRemove = false;
                    break;
            }
            
            // Initialize container with all settings from point
            container.InitializeFromPoint(
                lootTable,                          // From point
                containerMode,                      // From point
                initialItems,                      // From point
                generateLootOnFirstOpen,           // From point
                preGenerateOnStart,                // From point
                overrideMinItems,             // From point
                overrideMaxItems,             // From point
                config.MaxSlots,                  // From config (fixed setting)
                config.IsLocked,                   // From config (fixed setting)
                config.ContainerName,              // From config (fixed setting)
                finalAllowAdd,                     // From point (converted from enum)
                finalAllowRemove                   // From point (converted from enum)
            );
            
            // Set interaction type from point
            var containerInteractable = container.GetComponent<ContainerInteractable>();
            if (containerInteractable != null)
            {
                containerInteractable.SetInteractionType(interactionType, requiredHoldTime);
            }
        }

        /// <summary>
        /// Manually trigger spawn (for OnDemand mode).
        /// </summary>
        [Server]
        public void TriggerSpawn()
        {
            SpawnContainer();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
                return;

            float radius = config != null ? config.SpawnRadius : 1f;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, radius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.2f);
        }
    }
}
