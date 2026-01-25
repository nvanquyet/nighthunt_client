using _Night_Hunt.Scripts.Gameplay.Systems.Inventory.Components;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Loot;
using UnityEngine;

namespace NightHunt.InteractionSystem.Combat
{
    public class PlayerHealthComponent : NetworkBehaviour
    {
        
        [Header("Death")]
        [SerializeField] private GameObject corpsePrefab;

        protected override void OnDeath(NetworkConnection killer)
        {
            if (!IsServer) return;
    
            // Spawn corpse
            SpawnPlayerCorpse();
    
            // Clear inventory
            var inventory = GetComponent<GridInventoryComponent>();
            ItemInstance[] items = inventory.Items.ToArray();
            inventory.Clear();
    
            // Respawn player
            // TODO: Implement respawn logic
        }

        [Server]
        private void SpawnPlayerCorpse()
        {
            GameObject corpseObj = Instantiate(corpsePrefab, transform.position, transform.rotation);
            ServerManager.Spawn(corpseObj);
    
            PlayerCorpseLoot corpse = corpseObj.GetComponent<PlayerCorpseLoot>();
    
            var inventory = GetComponent<GridInventoryComponent>();
            corpse.InitializeCorpse(
                playerName: "Player", // TODO: Get actual player name
                playerItems: inventory.Items.ToArray()
            );
        }
    }
}