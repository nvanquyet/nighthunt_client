using FishNet.Object;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Singleton manager for spawning world pickup items
    /// Server-only: All spawns must be done on server
    /// </summary>
    public class WorldDropManager : NetworkBehaviour
    {
        private static WorldDropManager _instance;

        public static WorldDropManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<WorldDropManager>();
                    
                    if (_instance == null)
                    {
                        var go = new GameObject("[WorldDropManager]");
                        _instance = go.AddComponent<WorldDropManager>();
                        // Note: Cannot use DontDestroyOnLoad in NetworkBehaviour (FishNet restriction)
                        // WorldDropManager should be spawned as a persistent NetworkObject in scene
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            // Note: Cannot use DontDestroyOnLoad in NetworkBehaviour (FishNet restriction)
            // WorldDropManager should be spawned as a persistent NetworkObject in scene
        }

        /// <summary>
        /// Spawn a world pickup item at specified position
        /// Server-only: Must be called from server
        /// </summary>
        /// <param name="data">Item instance data to spawn</param>
        /// <param name="position">World position</param>
        /// <param name="rotation">World rotation</param>
        /// <returns>WorldPickup component (null if failed)</returns>
        [Server]
        public WorldPickup SpawnWorldPickup(ItemInstanceData data, Vector3 position, Quaternion rotation)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldDropManager] SpawnWorldPickup: server-only!");
                return null;
            }

            var def = ItemDatabase.GetDefinition(data.DefinitionID);
            if (def == null)
            {
                Debug.LogError($"[WorldDropManager] Item definition not found: {data.DefinitionID}");
                return null;
            }

            // Create GameObject for WorldPickup
            var go = new GameObject($"WorldPickup_{data.DefinitionID}_{data.InstanceID.Substring(0, 8)}");
            go.transform.position = position;
            go.transform.rotation = rotation;

            // Add NetworkObject
            var netObj = go.AddComponent<NetworkObject>();
            
            // Add WorldPickup component
            var pickup = go.AddComponent<WorldPickup>();

            // Network spawn (all clients will see this)
            ServerManager.Spawn(netObj);

            // Initialize with item data
            pickup.Initialize(data);

            return pickup;
        }

        /// <summary>
        /// Spawn multiple world pickups from spawn results
        /// Server-only
        /// </summary>
        /// <param name="spawnResults">List of items to spawn</param>
        /// <param name="centerPosition">Center position for spawning</param>
        /// <param name="spreadRadius">Radius to spread items around center</param>
        [Server]
        public void SpawnWorldPickupsFromResults(System.Collections.Generic.List<NightHunt.GameplaySystems.Core.Configs.SpawnResult> spawnResults, Vector3 centerPosition, float spreadRadius = 1.5f)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldDropManager] SpawnWorldPickupsFromResults: server-only!");
                return;
            }

            foreach (var result in spawnResults)
            {
                if (result.ItemDef == null) continue;

                // Create ItemInstance from result
                var instance = new ItemInstance(result.ItemDef.ItemID, result.Quantity, -1);
                var data = instance.ToData();

                // Random position around center
                Vector3 spawnPos = centerPosition + Random.insideUnitSphere * spreadRadius;
                spawnPos.y = centerPosition.y; // Keep same Y level

                SpawnWorldPickup(data, spawnPos, Quaternion.identity);
            }
        }
    }
}
