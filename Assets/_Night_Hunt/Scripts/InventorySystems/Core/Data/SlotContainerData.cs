// using System;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// using NightHunt.Inventory.Core.Enums;
// using NightHunt.Inventory.Core.Interfaces;
//
// namespace NightHunt.Inventory.Core.Data
// {
//     /// <summary>
//     /// Generic slot-based container data structure.
//     /// Can be used for: Inventory, Equipment, Weapons, QuickSlots, Containers.
//     /// Implements ISlotContainer for polymorphic usage.
//     /// </summary>
//     [Serializable]
//     public class SlotContainerData : ISlotContainer
//     {
//         [SerializeField]
//         private string containerName;
//
//         [SerializeField]
//         private int maxSlots;
//
//         [SerializeField]
//         private SlotLocationType containerType;
//
//         [SerializeField]
//         private List<ContainerSlot> slots;
//
//         /// <summary>
//         /// Represents a single slot in the container.
//         /// </summary>
//         [Serializable]
//         public class ContainerSlot
//         {
//             public int Index;
//             public ItemInstance Item; // Null if empty
//             public bool IsLocked; // For future: locked slots that can't be used
//
//             public bool IsEmpty => Item == null;
//         }
//
//         // === Constructors ===
//
//         public SlotContainerData(string name, int slotCount, SlotLocationType type)
//         {
//             containerName = name;
//             maxSlots = slotCount;
//             containerType = type;
//             slots = new List<ContainerSlot>(maxSlots);
//
//             for (int i = 0; i < maxSlots; i++)
//             {
//                 slots.Add(new ContainerSlot { Index = i, Item = null, IsLocked = false });
//             }
//         }
//
//         // === ISlotContainer Implementation ===
//
//         public int GetSlotCount() => maxSlots;
//
//         public ItemInstance GetItemAtSlot(int slotIndex)
//         {
//             if (slotIndex < 0 || slotIndex >= maxSlots)
//                 return null;
//
//             return slots[slotIndex].Item;
//         }
//
//         public bool SetItemAtSlot(int slotIndex, ItemInstance item)
//         {
//             if (slotIndex < 0 || slotIndex >= maxSlots)
//                 return false;
//
//             if (slots[slotIndex].IsLocked)
//                 return false;
//
//             slots[slotIndex].Item = item;
//             return true;
//         }
//
//         public bool IsSlotEmpty(int slotIndex)
//         {
//             if (slotIndex < 0 || slotIndex >= maxSlots)
//                 return false;
//
//             return slots[slotIndex].Item == null;
//         }
//
//         public List<ItemInstance> GetAllItems()
//         {
//             return slots.Where(s => s.Item != null).Select(s => s.Item).ToList();
//         }
//
//         public int GetEmptySlotCount()
//         {
//             return slots.Count(s => s.Item == null && !s.IsLocked);
//         }
//
//         public void Clear()
//         {
//             foreach (var slot in slots)
//             {
//                 slot.Item = null;
//             }
//         }
//
//         public int FindFirstEmptySlot()
//         {
//             for (int i = 0; i < slots.Count; i++)
//             {
//                 if (slots[i].Item == null && !slots[i].IsLocked)
//                     return i;
//             }
//             return -1;
//         }
//
//         public bool CanAcceptItem(ItemInstance item)
//         {
//             if (item == null || item.Definition == null)
//                 return false;
//
//             // Check if item's allowed locations include this container type
//             return item.Definition.IsAllowedInLocation(containerType);
//         }
//
//         // === Additional Methods ===
//
//         /// <summary>
//         /// Try to add item to first available empty slot.
//         /// </summary>
//         public bool TryAddItem(ItemInstance item, out int assignedSlot)
//         {
//             assignedSlot = -1;
//
//             if (!CanAcceptItem(item))
//                 return false;
//
//             int emptySlot = FindFirstEmptySlot();
//             if (emptySlot == -1)
//                 return false;
//
//             slots[emptySlot].Item = item;
//             assignedSlot = emptySlot;
//             return true;
//         }
//
//         /// <summary>
//         /// Try to add item at specific slot index.
//         /// </summary>
//         public bool TryAddItemAtSlot(ItemInstance item, int slotIndex)
//         {
//             if (!CanAcceptItem(item))
//                 return false;
//
//             if (slotIndex < 0 || slotIndex >= maxSlots)
//                 return false;
//
//             if (slots[slotIndex].Item != null || slots[slotIndex].IsLocked)
//                 return false;
//
//             slots[slotIndex].Item = item;
//             return true;
//         }
//
//         /// <summary>
//         /// Remove item by instance ID.
//         /// </summary>
//         public bool RemoveItem(string instanceId, out int slotIndex)
//         {
//             slotIndex = -1;
//
//             var slot = slots.Find(s => s.Item != null && s.Item.InstanceId == instanceId);
//             if (slot == null)
//                 return false;
//
//             slotIndex = slot.Index;
//             slot.Item = null;
//             return true;
//         }
//
//         /// <summary>
//         /// Remove item at specific slot.
//         /// </summary>
//         public ItemInstance RemoveItemAtSlot(int slotIndex)
//         {
//             if (slotIndex < 0 || slotIndex >= maxSlots)
//                 return null;
//
//             var item = slots[slotIndex].Item;
//             slots[slotIndex].Item = null;
//             return item;
//         }
//
//         /// <summary>
//         /// Swap items between two slots.
//         /// </summary>
//         public void SwapSlots(int slotA, int slotB)
//         {
//             if (slotA < 0 || slotA >= maxSlots || slotB < 0 || slotB >= maxSlots)
//                 return;
//
//             if (slots[slotA].IsLocked || slots[slotB].IsLocked)
//                 return;
//
//             (slots[slotA].Item, slots[slotB].Item) = (slots[slotB].Item, slots[slotA].Item);
//         }
//
//         /// <summary>
//         /// Find item by instance ID.
//         /// </summary>
//         public ItemInstance FindItem(string instanceId)
//         {
//             var slot = slots.Find(s => s.Item != null && s.Item.InstanceId == instanceId);
//             return slot?.Item;
//         }
//
//         /// <summary>
//         /// Check if container has item.
//         /// </summary>
//         public bool HasItem(string instanceId)
//         {
//             return slots.Any(s => s.Item != null && s.Item.InstanceId == instanceId);
//         }
//
//         /// <summary>
//         /// Lock/unlock slot (for future features).
//         /// </summary>
//         public void SetSlotLocked(int slotIndex, bool locked)
//         {
//             if (slotIndex >= 0 && slotIndex < maxSlots)
//             {
//                 slots[slotIndex].IsLocked = locked;
//             }
//         }
//
//         /// <summary>
//         /// Expand container size (for backpack upgrades).
//         /// </summary>
//         public void ExpandCapacity(int additionalSlots)
//         {
//             int newMax = maxSlots + additionalSlots;
//             for (int i = maxSlots; i < newMax; i++)
//             {
//                 slots.Add(new ContainerSlot { Index = i, Item = null, IsLocked = false });
//             }
//             maxSlots = newMax;
//         }
//
//         /// <summary>
//         /// Get all slots (for UI iteration).
//         /// </summary>
//         public List<ContainerSlot> GetAllSlots() => slots;
//
//         /// <summary>
//         /// Calculate total weight of all items.
//         /// </summary>
//         public float GetTotalWeight()
//         {
//             float total = 0f;
//             foreach (var slot in slots)
//             {
//                 if (slot.Item != null)
//                 {
//                     if (slot.Item.Definition == null)
//                     {
//                         Debug.LogError($"[SlotContainerData] Item at slot {slot.Index} has null Definition. InstanceId: {slot.Item.InstanceId}");
//                         continue; // Skip this item
//                     }
//                     total += slot.Item.GetTotalWeight();
//                 }
//             }
//             return total;
//         }
//
//         // === Properties ===
//
//         public string ContainerName => containerName;
//         public SlotLocationType ContainerType => containerType;
//     }
// }