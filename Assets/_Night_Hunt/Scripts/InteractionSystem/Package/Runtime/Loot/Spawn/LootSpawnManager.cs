using UnityEngine;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using NightHunt.InteractionSystem.Loot.Spawn;
using NightHunt.InteractionSystem.Loot.Definitions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Items.Runtime;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Manages multiple loot spawn points in the scene.
    /// Can spawn all points at once or manage them individually.
    /// </summary>
    public class LootSpawnManager : LootManagerBase<LootSpawnPoint>
    {
        protected override void Start()
        {
            base.Start();
            // Note: Each LootSpawnPoint handles its own spawning based on its config
        }

        /// <summary>
        /// Find all LootSpawnPoints in the scene.
        /// </summary>
        protected override void FindAllSpawnPoints()
        {
            activeSpawnPoints.Clear();
            LootSpawnPoint[] foundPoints = FindObjectsOfType<LootSpawnPoint>();
            activeSpawnPoints.AddRange(foundPoints);
            Debug.Log($"[LootSpawnManager] Found {activeSpawnPoints.Count} spawn points");
        }

        /// <summary>
        /// Spawn loot at all spawn points (server only).
        /// </summary>
        public void SpawnAll()
        {
            if (!InstanceFinder.IsServer)
            {
                Debug.LogWarning("[LootSpawnManager] SpawnAll can only be called on server!");
                return;
            }

            foreach (var spawnPoint in activeSpawnPoints)
            {
                if (spawnPoint != null)
                {
                    spawnPoint.TriggerSpawn();
                }
            }
        }

        /// <summary>
        /// Spawn loot at a specific spawn point by index.
        /// </summary>
        public void SpawnAt(int index)
        {
            if (index >= 0 && index < activeSpawnPoints.Count)
            {
                activeSpawnPoints[index].TriggerSpawn();
            }
        }

        /// <summary>
        /// Spawn loot at a specific spawn point.
        /// </summary>
        public void SpawnAt(LootSpawnPoint spawnPoint)
        {
            if (spawnPoint != null && activeSpawnPoints.Contains(spawnPoint))
            {
                spawnPoint.TriggerSpawn();
            }
        }

        /// <summary>
        /// Spawn a NetworkLootItem at a specific position (centralized spawning).
        /// Used for dropping items from inventory to world.
        /// Server-only method (checks InstanceFinder.IsServer internally).
        /// </summary>
        public NetworkLootItem SpawnItemAtPosition(ItemInstance itemInstance, LootItemDefinition lootDef, Vector3 position, NetworkLootItem prefabOverride = null)
        {
            if (!InstanceFinder.IsServer)
            {
                Debug.LogWarning("[LootSpawnManager] SpawnItemAtPosition can only be called on server!");
                return null;
            }

            // Get prefab from first spawn point config, or use override
            NetworkLootItem prefab = prefabOverride;
            if (prefab == null && activeSpawnPoints != null && activeSpawnPoints.Count > 0)
            {
                foreach (var point in activeSpawnPoints)
                {
                    if (point != null)
                    {
                        var config = point.GetConfig();
                        if (config != null && config.LootItemPrefab != null)
                        {
                            prefab = config.LootItemPrefab;
                            break;
                        }
                    }
                }
            }

            if (prefab == null)
            {
                Debug.LogError("[LootSpawnManager] NetworkLootItem prefab not found! Assign prefab to LootSpawnConfig or pass prefabOverride.");
                return null;
            }

            // Spawn NetworkLootItem
            NetworkLootItem lootItem = Instantiate(prefab, position, Quaternion.identity);
            NetworkObject lootNO = lootItem.GetComponent<NetworkObject>();
            if (lootNO == null)
            {
                Debug.LogError("[LootSpawnManager] NetworkLootItem prefab must have NetworkObject component!");
                Destroy(lootItem.gameObject);
                return null;
            }

            // Spawn on network (use ServerManager.Spawn for MonoBehaviour)
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
            {
                InstanceFinder.NetworkManager.ServerManager.Spawn(lootNO);
            }
            else
            {
                Debug.LogError("[LootSpawnManager] NetworkManager or ServerManager is null! Cannot spawn item.");
                Destroy(lootItem.gameObject);
                return null;
            }

            // Initialize with preserved state (durability, attachments, customData, etc.)
            if (lootDef != null)
            {
                lootItem.ServerInitializeFromItemInstance(itemInstance, lootDef);
            }
            else
            {
                Debug.LogWarning($"[LootSpawnManager] LootItemDefinition is null for {itemInstance.itemDataId}, item may not display correctly.");
            }

            return lootItem;
        }

        /// <summary>
        /// Get NetworkLootItem prefab from first spawn point config (for fallback).
        /// </summary>
        public NetworkLootItem GetLootItemPrefab()
        {
            if (activeSpawnPoints != null && activeSpawnPoints.Count > 0)
            {
                foreach (var point in activeSpawnPoints)
                {
                    if (point != null)
                    {
                        var config = point.GetConfig();
                        if (config != null && config.LootItemPrefab != null)
                        {
                            return config.LootItemPrefab;
                        }
                    }
                }
            }
            return null;
        }

    }
}
