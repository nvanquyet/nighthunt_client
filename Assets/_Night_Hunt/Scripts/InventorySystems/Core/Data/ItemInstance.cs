using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Database;
using NightHunt.Inventory.Stats;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// Runtime instance of an item.
    /// Represents a specific item in player's inventory with unique ID and state.
    /// </summary>
    [System.Serializable]
    public class ItemInstance
    {
        [Header("Identity")]
        public string InstanceId;
        public ItemDefinition Definition;
        
        [Header("Stack & State")]
        public int StackSize = 1;
        
        [Header("Resource System")]
        public float CurrentResource = 0f;

        [Header("Location")]
        public bool IsEquipped = false;
        
        // ✅ NEW: Index in inventory (position in UI)
        public int InventoryIndex = -1; // -1 = not in inventory, >= 0 = slot position
        
        [Header("Attachments")]
        public List<ItemInstance> AttachedItems { get; private set; }
        
        public ItemInstance(ItemDefinition definition, string instanceId = null)
        {
            Definition = definition;
            InstanceId = instanceId ?? System.Guid.NewGuid().ToString();
            StackSize = 1;
            CurrentResource = definition != null ? definition.MaxResource : 0f;
            AttachedItems = new List<ItemInstance>();
        }
        
        // === Weight Calculation ===
        
        public float GetTotalWeight()
        {
            if (Definition == null)
                return 0f;
            
            return Definition.WeightPerUnit * StackSize;
        }
        
        // === Durability ===
        
        public void ModifyResource(float amount)
        {
            if (Definition == null || Definition.ResourceType == ItemResourceType.None)
                return;
            
            CurrentResource = Mathf.Clamp(CurrentResource + amount, 0f, Definition.MaxResource);
        }

        /// <summary>
        /// Convenience helper to decrease durability by a positive amount.
        /// </summary>
        public void DecreaseResource(float amount)
        {
            if (amount <= 0f)
                return;

            ModifyResource(-Mathf.Abs(amount));
        }
        /// <summary>
        /// Check if this item currently has an attachment in the given slot type.
        /// </summary>
        public bool HasAttachment(AttachmentSlotType slotType)
        {
            return GetAttachment(slotType) != null;
        }

        
        public float GetResourcePercent()
        {
            if (Definition == null ||
                Definition.ResourceType == ItemResourceType.None ||
                Definition.MaxResource <= 0f)
                return 100f;

            return (CurrentResource / Definition.MaxResource) * 100f;
        }
        
        public bool IsResourceDepleted()
        {
            return Definition != null && Definition.ResourceType != ItemResourceType.None && CurrentResource <= 0f;
        }
        
        // === Serialization ===
        
        public ItemInstanceData Serialize()
        {
            return new ItemInstanceData
            {
                InstanceId = this.InstanceId,
                ItemId = this.Definition?.ItemId ?? "",
                StackSize = this.StackSize,
                CurrentResource = this.CurrentResource,
                IsEquipped = this.IsEquipped,
                InventoryIndex = this.InventoryIndex, // ✅ Serialize index
                Attachments = AttachedItems?
                    .Select(a => a.Serialize())
                    .ToList()
            };
        }
        
        public static ItemInstance Deserialize(ItemInstanceData data, ItemDefinition definition)
        {
            var item = new ItemInstance(definition, data.InstanceId)
            {
                StackSize = data.StackSize,
                CurrentResource = data.CurrentResource,
                IsEquipped = data.IsEquipped,
                InventoryIndex = data.InventoryIndex
            };
            if (data.Attachments != null)
            {
                foreach (var attData in data.Attachments)
                {
                    var attDef = ItemDefinitionDatabase.Instance.GetDefinition(attData.ItemId);
                    if (attDef == null)
                    {
                        UnityEngine.Debug.LogError($"Missing ItemDefinition for {attData.ItemId}");
                        continue;
                    }
                    var attachment = Deserialize(attData, attDef);
                    item.AttachedItems.Add(attachment);
                }
            }
            return item;
        }
        
        // === Cloning ===
        public ItemInstance Clone(string newInstanceId = null)
        {
            var clone = new ItemInstance(Definition, newInstanceId ?? System.Guid.NewGuid().ToString())
            {
                StackSize = this.StackSize,
                IsEquipped = this.IsEquipped,
                InventoryIndex = -1, 
                CurrentResource = this.CurrentResource,
            };
            
            return clone;
        }
        
        // === Attachments ===

        /// <summary>
        /// Check if this item can accept the given attachment instance.
        /// Only checks basic rules: definition slot and no duplicate slot type.
        /// </summary>
        public bool CanAcceptAttachment(ItemInstance attachment)
        {
            if (Definition == null || attachment == null || attachment.Definition == null)
                return false;

            var slotType = attachment.Definition.AttachmentType;

            // Must have this slot type defined.
            if (Definition.AttachmentSlots == null ||
                !Definition.AttachmentSlots.Contains(slotType))
                return false;
            
            // Must not already have an attachment of this slot type.
            if (AttachedItems != null)
            {
                foreach (var att in AttachedItems)
                {
                    if (att?.Definition != null && att.Definition.AttachmentType == slotType)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get a single attachment by slot type.
        /// </summary>
        public ItemInstance GetAttachment(AttachmentSlotType slotType)
        {
            if (AttachedItems == null)
                return null;

            return AttachedItems.Find(a => a != null &&
                                           a.Definition != null &&
                                           a.Definition.AttachmentType == slotType);
        }

        /// <summary>
        /// Add an attachment to this item.
        /// </summary>
        public void AddAttachment(ItemInstance attachment)
        {
            if (attachment == null)
                return;

            if (AttachedItems == null)
                AttachedItems = new List<ItemInstance>();

            if (!AttachedItems.Contains(attachment))
            {
                AttachedItems.Add(attachment);
                attachment.IsEquipped = true;
            }
        }

        /// <summary>
        /// Remove an attachment by slot type.
        /// </summary>
        public void RemoveAttachment(AttachmentSlotType slotType, out ItemInstance detached)
        {
            detached = null;

            if (AttachedItems == null)
                return;

            int index = AttachedItems.FindIndex(a => a != null &&
                                                     a.Definition != null &&
                                                     a.Definition.AttachmentType == slotType);
            if (index < 0)
                return;

            detached = AttachedItems[index];
            AttachedItems.RemoveAt(index);

            if (detached != null)
            {
                detached.IsEquipped = false;
            }
        }
        
        // === Validation ===
        
        public bool IsValid()
        {
            if (Definition == null)
                return false;
            
            if (string.IsNullOrEmpty(InstanceId))
                return false;
            
            if (StackSize <= 0)
                return false;
            
            if (Definition.IsStackable && StackSize > Definition.MaxStackSize)
                return false;
            if (!Definition.IsStackable && StackSize > 1)
                return false;
            
            return true;
        }

        // === Stat Modifier Provider (IStatModifierProvider) ===

        /// <summary>
        /// Collect stat modifiers from this item definition and its attachments.
        /// </summary>
        public List<StatModifierData> GetStatModifiers()
        {
            var result = new List<StatModifierData>();

            // Base modifiers from own definition.
            if (Definition != null && Definition.StatModifiers != null)
            {
                foreach (var def in Definition.StatModifiers)
                {
                    result.Add(def.ToStatModifierData());
                }
            }

            // Modifiers from attachments (if they also provide modifiers).
            // if (AttachedItems != null)
            // {
            //     foreach (var attachment in AttachedItems)
            //     {
            //         if (attachment is IStatModifierProvider provider)
            //         {
            //             result.AddRange(provider.GetStatModifiers());
            //         }
            //     }
            // }

            return result;
        }

        /// <summary>
        /// Unique source ID used for tracking modifiers coming from this item.
        /// </summary>
        public string GetModifierSourceId()
        {
            return $"Item:{InstanceId}";
        }
    }
    
    // === Network Sync Data Structure ===
    
    [System.Serializable]
    public struct ItemInstanceData
    {
        public string InstanceId;
        public string ItemId;
        public int StackSize;
        public float CurrentResource;
        public bool IsEquipped;
        public int InventoryIndex; // ✅ Sync index over network
        public List<ItemInstanceData> Attachments;
    }
}