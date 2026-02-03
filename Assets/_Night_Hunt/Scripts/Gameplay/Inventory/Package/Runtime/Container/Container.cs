using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Interaction;
using NightHunt.Inventory.Domain;
using NightHunt.Networking;

namespace NightHunt.Inventory.Container
{
    /// <summary>
    /// Container component for chests, boss loot, and player corpses.
    /// Implements IInteractable for hold-to-open interaction.
    /// </summary>
    public class Container : MonoBehaviour, IInteractable
    {
        [SerializeField] private ContainerConfig config;
        
        private List<ItemInstance> items = new List<ItemInstance>();
        private float currentWeight = 0f;
        private bool isOpen = false;
        
        public ContainerConfig Config => config;
        public List<ItemInstance> Items => items;
        public float CurrentWeight => currentWeight;
        public float MaxWeight => config != null ? config.MaxWeight : 0f;
        public bool CanTakeOut => config != null && config.CanTakeOut;
        public bool CanPutIn => config != null && config.CanPutIn;
        
        public void Initialize(ContainerConfig containerConfig)
        {
            config = containerConfig;
            GenerateLoot();
        }
        
        public void InitializeWithItems(List<ItemInstance> initialItems, ContainerConfig containerConfig)
        {
            config = containerConfig;
            items = initialItems;
            RecalculateWeight();
        }
        
        private void GenerateLoot()
        {
            items.Clear();
            
            if (config == null) return;
            
            // Add fixed items
            if (config.FixedItems != null)
            {
                foreach (var rule in config.FixedItems)
                {
                    if (rule.Item != null)
                    {
                        for (int i = 0; i < rule.Quantity; i++)
                        {
                            var instance = CreateItemInstance(rule.Item, rule.DurabilityPercent);
                            items.Add(instance);
                        }
                    }
                }
            }
            
            // Add random items
            if (config.RandomItems != null)
            {
                foreach (var entry in config.RandomItems)
                {
                    if (entry.Item != null && Random.value <= entry.SpawnChance)
                    {
                        int quantity = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                        float durability = Random.Range(entry.MinDurability, entry.MaxDurability);
                        
                        for (int i = 0; i < quantity; i++)
                        {
                            var instance = CreateItemInstance(entry.Item, durability);
                            items.Add(instance);
                        }
                    }
                }
            }
            
            RecalculateWeight();
        }
        
        private ItemInstance CreateItemInstance(ItemDefinition definition, float durabilityPercent)
        {
            return new ItemInstance
            {
                InstanceId = System.Guid.NewGuid().ToString(),
                Definition = definition,
                StackSize = 1,
                CurrentDurability = definition.MaxDurability * (durabilityPercent / 100f),
                CurrentAmmo = 0
            };
        }
        
        public bool TryAddItem(ItemInstance item)
        {
            if (!CanPutIn) return false;
            
            float itemWeight = WeightCalculator.CalculateItemWeight(item);
            if (currentWeight + itemWeight > MaxWeight)
            {
                return false; // Over weight limit
            }
            
            items.Add(item);
            currentWeight += itemWeight;
            return true;
        }
        
        public bool TryRemoveItem(ItemInstance item)
        {
            if (!CanTakeOut) return false;
            
            if (items.Remove(item))
            {
                float itemWeight = WeightCalculator.CalculateItemWeight(item);
                currentWeight -= itemWeight;
                return true;
            }
            
            return false;
        }
        
        private void RecalculateWeight()
        {
            currentWeight = 0f;
            foreach (var item in items)
            {
                currentWeight += WeightCalculator.CalculateItemWeight(item);
            }
        }
        
        public float GetCurrentWeight() => currentWeight;
        public float GetMaxWeight() => MaxWeight;
        
        #region IInteractable Implementation
        
        public InteractionType GetInteractionType() => InteractionType.HoldToOpen;
        
        public string GetInteractText() => $"Hold E to open {config?.Type}";
        
        public Sprite GetInteractIcon()
        {
            // TODO: Return appropriate icon based on container type
            return null;
        }
        
        public Vector3 GetPosition() => transform.position;
        
        public float GetHoldDuration() => 2f; // Configurable
        
        public void OnHoldStart(NetworkPlayer player)
        {
            isOpen = true;
            // TODO: Open container UI
        }
        
        public void OnHoldProgress(float progress)
        {
            // Progress bar handled by InteractionPromptUI
        }
        
        public void OnHoldComplete(NetworkPlayer player)
        {
            // Container UI should already be open from OnHoldStart
            Debug.Log($"[Container] Opened: {gameObject.name}");
        }
        
        public void OnHoldCancelled()
        {
            isOpen = false;
        }
        
        public void OnInstantInteract(NetworkPlayer player)
        {
            // Not used for containers (they use hold interaction)
        }
        
        #endregion
    }
}
