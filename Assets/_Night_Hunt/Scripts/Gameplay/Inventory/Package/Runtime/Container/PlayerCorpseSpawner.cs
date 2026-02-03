using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Container;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain;
using NightHunt.Inventory.QuickSlot;
using NightHunt.Networking;

namespace NightHunt.Inventory.Container
{
    /// <summary>
    /// Spawns player corpse container when player dies.
    /// Collects ALL items from player (detaches attachments first).
    /// </summary>
    public class PlayerCorpseSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject corpsePrefab;
        [SerializeField] private ContainerConfig corpseConfig;
        
        void OnEnable()
        {
            // TODO: Subscribe to PlayerDeathEvents.OnPlayerDied when available
            // PlayerDeathEvents.OnPlayerDied += SpawnCorpse;
        }
        
        void OnDisable()
        {
            // TODO: Unsubscribe
            // PlayerDeathEvents.OnPlayerDied -= SpawnCorpse;
        }
        
        void SpawnCorpse(NetworkPlayer deadPlayer)
        {
            Debug.Log($"[PlayerCorpse] Spawning corpse for {deadPlayer.name}");
            
            // Spawn corpse at player position
            var corpse = Instantiate(corpsePrefab, deadPlayer.transform.position, deadPlayer.transform.rotation);
            var containerComponent = corpse.GetComponent<Container>();
            
            // Collect ALL items from player (detach attachments first!)
            var allItems = CollectAllPlayerItems(deadPlayer);
            
            // Initialize corpse container with collected items
            containerComponent.InitializeWithItems(allItems, corpseConfig);
            
            // Corpse is PERMANENT (no despawn)
        }
        
        List<ItemInstance> CollectAllPlayerItems(NetworkPlayer player)
        {
            var items = new List<ItemInstance>();
            
            // 1. Inventory items - use ComponentFinder
            var inventoryManager = ComponentFinder.FindInHierarchy<InventoryManager>(player);
            if (inventoryManager != null)
            {
                var slots = inventoryManager.GetAllSlots();
                items.AddRange(slots.Where(s => s.Item != null).Select(s => s.Item));
            }
            
            // 2. Equipment items (detach attachments) - use ComponentFinder
            var equipmentManager = ComponentFinder.FindInHierarchy<EquipmentManager>(player);
            if (equipmentManager != null)
            {
                foreach (var equip in equipmentManager.GetAllEquipped())
                {
                    items.AddRange(DetachAllAttachments(equip));
                    items.Add(equip);
                }
            }
            
            // 3. Weapon items (detach attachments) - use ComponentFinder
            var weaponManager = ComponentFinder.FindInHierarchy<WeaponManager>(player);
            if (weaponManager != null)
            {
                foreach (var weapon in weaponManager.GetAllWeapons())
                {
                    items.AddRange(DetachAllAttachments(weapon));
                    items.Add(weapon);
                }
            }
            
            // 4. QuickSlot items - use ComponentFinder
            var quickSlotManager = ComponentFinder.FindInHierarchy<QuickSlot.QuickSlotManager>(player);
            if (quickSlotManager != null)
            {
                items.AddRange(quickSlotManager.GetAllItems());
            }
            
            Debug.Log($"[PlayerCorpse] Collected {items.Count} items from {player.name}");
            
            return items;
        }
        
        List<ItemInstance> DetachAllAttachments(ItemInstance item)
        {
            var attachments = item.AttachedItems.ToList();
            item.AttachedItems.Clear();
            return attachments;
        }
    }
}
