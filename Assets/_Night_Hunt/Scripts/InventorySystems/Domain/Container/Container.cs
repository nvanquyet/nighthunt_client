using System;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Interfaces;
using UnityEngine;

namespace NightHunt.Inventory.Domain.Container
{
     /// <summary>
    /// Base container class for chests, loot, corpses.
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [Header("Configuration")]
        [SerializeField] protected ContainerConfig config;
        
        [Header("Interaction")]
        [SerializeField] protected Sprite interactIcon;
        [SerializeField] protected float holdDuration = 2f;
        
        [Header("Debug")]
        [SerializeField] protected bool enableDebugLogs = false;
        
        protected List<ItemInstance> items;
        protected bool isInitialized = false;
        
        #region Lifecycle
        
        protected virtual void Awake()
        {
            items = new List<ItemInstance>();
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initializes container with loot table.
        /// </summary>
        public virtual void Initialize(ContainerConfig containerConfig)
        {
            config = containerConfig;
            items.Clear();
            
            // Generate loot from config
            GenerateLoot();
            
            isInitialized = true;
            
            if (enableDebugLogs)
                Debug.Log($"[Container] Initialized with {items.Count} items");
        }
        
        /// <summary>
        /// Initializes container with specific items (for player corpse).
        /// </summary>
        public virtual void InitializeWithItems(List<ItemInstance> itemList, ContainerConfig containerConfig)
        {
            config = containerConfig;
            items = new List<ItemInstance>(itemList);
            isInitialized = true;
            
            if (enableDebugLogs)
                Debug.Log($"[Container] Initialized with {items.Count} custom items");
        }
        
        #endregion
        
        #region Loot Generation
        
        protected virtual void GenerateLoot()
        {
            if (config == null) return;
            
            // Add fixed items
            if (config.FixedItems != null)
            {
                foreach (var rule in config.FixedItems)
                {
                    for (int i = 0; i < rule.Quantity; i++)
                    {
                        var item = CreateItemFromRule(rule);
                        items.Add(item);
                    }
                }
            }
            
            // Add random items
            if (config.RandomItems != null)
            {
                foreach (var entry in config.RandomItems)
                {
                    if (UnityEngine.Random.value <= entry.SpawnChance)
                    {
                        int quantity = UnityEngine.Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                        
                        for (int i = 0; i < quantity; i++)
                        {
                            var item = CreateItemFromLootEntry(entry);
                            items.Add(item);
                        }
                    }
                }
            }
        }
        
        private ItemInstance CreateItemFromRule(ItemSpawnRule rule)
        {
            var item = new ItemInstance(rule.Item, Guid.NewGuid().ToString())
            {
                CurrentDurability = rule.DurabilityPercent,
                StackSize = 1
            };
            return item;
        }
        
        private ItemInstance CreateItemFromLootEntry(LootTableEntry entry)
        {
            float durability = UnityEngine.Random.Range(entry.MinDurability, entry.MaxDurability);
            
            var item = new ItemInstance(entry.Item, Guid.NewGuid().ToString())
            {
                CurrentDurability = durability,
                StackSize = 1
            };
            return item;
        }
        
        #endregion
        
        #region Container Operations
        
        /// <summary>
        /// Tries to add an item to the container.
        /// </summary>
        public virtual bool TryAddItem(ItemInstance item)
        {
            if (!config.CanPutIn) return false;
            
            // Check weight limit
            float currentWeight = GetCurrentWeight();
            float itemWeight = item.GetTotalWeight();
            
            if (currentWeight + itemWeight > config.MaxWeight)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[Container] Cannot add - weight limit exceeded");
                return false;
            }
            
            items.Add(item);
            
            if (enableDebugLogs)
                Debug.Log($"[Container] Added {item.Definition.ItemId}");
            
            return true;
        }
        
        /// <summary>
        /// Tries to remove an item from the container.
        /// </summary>
        public virtual bool TryRemoveItem(string instanceId)
        {
            if (!config.CanTakeOut) return false;
            
            var item = items.Find(i => i.InstanceId == instanceId);
            if (item != null)
            {
                items.Remove(item);
                
                if (enableDebugLogs)
                    Debug.Log($"[Container] Removed {item.Definition.ItemId}");
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets all items in the container.
        /// </summary>
        public virtual List<ItemInstance> GetAllItems() => new List<ItemInstance>(items);
        
        /// <summary>
        /// Gets current weight of all items.
        /// </summary>
        public virtual float GetCurrentWeight()
        {
            return items.Sum(i => i.GetTotalWeight());
        }
        
        /// <summary>
        /// Gets maximum weight capacity.
        /// </summary>
        public virtual float GetMaxWeight() => config?.MaxWeight ?? 0f;
        
        /// <summary>
        /// Checks if container is empty.
        /// </summary>
        public virtual bool IsEmpty() => items.Count == 0;
        
        #endregion
        
        #region IInteractable Implementation
        
        public virtual InteractionType GetInteractionType() => InteractionType.HoldToOpen;
        
        public virtual string GetInteractText()
        {
            return config.Type switch
            {
                ContainerType.StaticChest => "Hold E to open chest",
                ContainerType.BossLoot => "Hold E to loot",
                ContainerType.PlayerCorpse => "Hold E to loot corpse",
                _ => "Hold E to open"
            };
        }
        
        public virtual Sprite GetInteractIcon() => interactIcon;
        
        public virtual Vector3 GetPosition() => transform.position;
        
        public virtual float GetHoldDuration() => holdDuration;
        
        public virtual void OnHoldStart(object player)
        {
            if (enableDebugLogs)
                Debug.Log("[Container] Hold started");
        }
        
        public virtual void OnHoldProgress(float progress)
        {
            // Visual feedback can be added here
        }
        
        public virtual void OnHoldComplete(object player)
        {
            if (enableDebugLogs)
                Debug.Log("[Container] Opened");
            
            // Fire event to open container UI
            // TODO: Fire container opened event
        }
        
        public virtual void OnHoldCancelled()
        {
            if (enableDebugLogs)
                Debug.Log("[Container] Opening cancelled");
        }
        
        // Instant interaction not used
        public virtual void OnInstantInteract(object player) { }
        
        #endregion
    }
}