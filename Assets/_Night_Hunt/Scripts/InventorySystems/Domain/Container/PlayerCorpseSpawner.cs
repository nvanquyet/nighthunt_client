using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Domain.Inventory;
using NightHunt.Inventory.Domain.Equipment;
using NightHunt.Inventory.Domain.Weapon;
using NightHunt.Inventory.Domain.Attachment;
using System.Collections.Generic;

namespace NightHunt.Inventory.Domain.Container
{
    /// <summary>
    /// Spawns player corpse containers when players die.
    /// Corpses are PERMANENT and contain all player items.
    /// </summary>
    public class PlayerCorpseSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject corpsePrefab;
        
        [Header("Configuration")]
        [SerializeField] private ContainerConfig corpseConfig;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject deathVFX;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        #region Lifecycle
        
        void OnEnable()
        {
            PlayerEvents.OnPlayerDied += SpawnCorpse;
        }
        
        void OnDisable()
        {
            PlayerEvents.OnPlayerDied -= SpawnCorpse;
        }
        
        #endregion
        
        #region Corpse Spawning
        
        private void SpawnCorpse(object deadPlayerObj)
        {
            if (corpsePrefab == null)
            {
                Debug.LogError("[PlayerCorpseSpawner] Corpse prefab not assigned!");
                return;
            }
            
            if (corpseConfig == null)
            {
                Debug.LogError("[PlayerCorpseSpawner] Corpse config not assigned!");
                return;
            }
            
            // Get player components
            var deadPlayerTransform = (deadPlayerObj as Component)?.transform;
            if (deadPlayerTransform == null)
            {
                Debug.LogError("[PlayerCorpseSpawner] Invalid player object!");
                return;
            }
            
            var inventoryManager = deadPlayerTransform.GetComponent<InventoryManager>();
            var equipmentManager = deadPlayerTransform.GetComponent<EquipmentManager>();
            var weaponManager = deadPlayerTransform.GetComponent<WeaponManager>();
            var attachmentManager = deadPlayerTransform.GetComponent<AttachmentManager>();
            
            // Collect ALL items from player
            var allItems = CollectAllPlayerItems(
                inventoryManager,
                equipmentManager,
                weaponManager,
                attachmentManager
            );
            
            // Spawn corpse at player position
            var corpseObj = Instantiate(corpsePrefab, deadPlayerTransform.position, deadPlayerTransform.rotation);
            var containerComponent = corpseObj.GetComponent<LootContainer>();
            
            if (containerComponent == null)
            {
                Debug.LogError("[PlayerCorpseSpawner] Container component not found on corpse prefab!");
                Destroy(corpseObj);
                return;
            }
            
            // Initialize corpse container with collected items
            containerComponent.InitializeWithItems(allItems, corpseConfig);
            
            // Spawn death VFX
            if (deathVFX != null)
            {
                Instantiate(deathVFX, deadPlayerTransform.position, Quaternion.identity);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerCorpseSpawner] Spawned corpse with {allItems.Count} items at {deadPlayerTransform.position}");
            }
            
            // Corpse is PERMANENT (no despawn)
        }
        
        #endregion
        
        #region Item Collection
        
        private List<ItemInstance> CollectAllPlayerItems(
            InventoryManager inventory,
            EquipmentManager equipment,
            WeaponManager weapons,
            AttachmentManager attachments)
        {
            var allItems = new List<ItemInstance>();
            
            // 1. Inventory items
            if (inventory != null)
            {
                var inventoryItems = inventory.GetAllItems();
                allItems.AddRange(inventoryItems);
                
                if (enableDebugLogs)
                    Debug.Log($"[PlayerCorpseSpawner] Collected {inventoryItems.Count} inventory items");
            }
            
            // 2. Equipment items (detach attachments first!)
            if (equipment != null && attachments != null)
            {
                foreach (var equip in equipment.GetAllEquipped())
                {
                    // Detach all attachments from equipment
                    var detached = attachments.DetachAllAttachments(equip);
                    allItems.AddRange(detached);
                    
                    // Add equipment itself
                    allItems.Add(equip);
                }
                
                if (enableDebugLogs)
                    Debug.Log($"[PlayerCorpseSpawner] Collected equipment items");
            }
            
            // 3. Weapon items (detach attachments first!)
            if (weapons != null && attachments != null)
            {
                foreach (var weapon in weapons.GetAllWeapons())
                {
                    // Detach all attachments from weapon
                    var detached = attachments.DetachAllAttachments(weapon);
                    allItems.AddRange(detached);
                    
                    // Add weapon itself
                    allItems.Add(weapon);
                }
                
                if (enableDebugLogs)
                    Debug.Log($"[PlayerCorpseSpawner] Collected weapon items");
            }
            
            // 4. QuickSlot items (TODO: when QuickSlotManager is implemented)
            // allItems.AddRange(quickSlots.GetAllItems());
            
            if (enableDebugLogs)
                Debug.Log($"[PlayerCorpseSpawner] Total collected: {allItems.Count} items");
            
            return allItems;
        }
        
        #endregion
    }
}