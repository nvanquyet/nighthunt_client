using UnityEngine;
using FishNet.Object;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Loot
{
    /// <summary>
    /// Corpse loot container spawned when player dies.
    /// </summary>
    [RequireComponent(typeof(NetworkLootContainer))]
    public class PlayerCorpseLoot : NetworkBehaviour
    {
        [Header("Corpse Settings")]
        [SerializeField] private float despawnDelay = 300f; // 5 minutes
        [SerializeField] private GameObject corpsePrefab;

        private NetworkLootContainer lootContainer;
        private float spawnTime;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Use ComponentFinder to search in hierarchy (component might be in child)
            lootContainer = ComponentFinder.FindComponentInHierarchy<NetworkLootContainer>(gameObject, includeInactive: false);
            spawnTime = Time.time;

            // Auto-despawn after delay
            Invoke(nameof(DespawnCorpse), despawnDelay);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Configure corpse container: ReadOnly (can only take items, not add) and always open
            if (lootContainer != null)
            {
                // Set permissions: ReadOnly (allowRemoveItems = true, allowAddItems = false)
                lootContainer.SetContainerPermissions(allowAdd: false, allowRemove: true);
                
                // Auto-open corpse container (always open for looting)
                lootContainer.SetOpenedState(true);
                
                Debug.Log($"[PlayerCorpseLoot] Corpse container configured - ReadOnly, Always Open");
            }
        }

        private void Update()
        {
            // Check if container is empty and despawn
            if (lootContainer != null && lootContainer.IsEmpty())
            {
                if (Time.time - spawnTime > 5f) // Wait 5 seconds after spawn before auto-despawn when empty
                {
                    DespawnCorpse();
                }
            }
        }

        /// <summary>
        /// Initialize corpse with player inventory.
        /// </summary>
        [Server]
        public void InitializeWithInventory(GridInventoryComponent playerInventory)
        {
            if (playerInventory == null || lootContainer == null)
                return;

            // Transfer all items from player inventory to corpse
            foreach (var item in playerInventory.Items)
            {
                lootContainer.AddItem(item);
            }

            // Clear player inventory
            playerInventory.Clear();
        }

        /// <summary>
        /// Despawn the corpse.
        /// </summary>
        [Server]
        private void DespawnCorpse()
        {
            if (IsSpawned)
            {
                Despawn();
            }
        }
    }
}
