// using System;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// using NightHunt.Inventory.Core;
// using NightHunt.Inventory.Core.Data;
// using NightHunt.Inventory.Core.Enums;
//
// namespace NightHunt.Inventory.Stats
// {
//     /// <summary>
//     /// Manages stat modifiers from equipped items and attachments.
//     /// Integrates with InventorySystem to apply/remove modifiers.
//     /// 
//     /// INTEGRATION NOTE:
//     /// This manager TRACKS modifiers and fires events.
//     /// PlayerStats should subscribe to these events to apply/remove modifiers.
//     /// </summary>
//     [RequireComponent(typeof(PlayerStats))]
//     public class PlayerStatsManager : MonoBehaviour
//     {
//         [Header("Debug")]
//         [SerializeField] private bool enableDebugLogs = true;
//         
//         // Components
//         private PlayerStats playerStats;
//         private InventorySystem inventorySystem;
//         
//         // Modifier tracking
//         // Key: Item InstanceId, Value: List of modifiers from that item
//         private Dictionary<string, List<StatModifierData>> activeModifiers = new Dictionary<string, List<StatModifierData>>();
//         
//         // Events for PlayerStats to subscribe to
//         public event Action<StatModifierData, string> OnModifierAdded;   // (modifier, sourceItemId)
//         public event Action<StatModifierData, string> OnModifierRemoved; // (modifier, sourceItemId)
//         public event Action<string> OnAllModifiersRemovedForSource;     // (sourceItemId)
//         
//         #region Lifecycle
//         
//         private void Awake()
//         {
//             playerStats = GetComponent<PlayerStats>();
//             inventorySystem = GetComponent<InventorySystem>();
//             
//             if (playerStats == null)
//             {
//                 LogError("PlayerStats component not found!");
//             }
//             
//             if (inventorySystem == null)
//             {
//                 LogError("InventorySystem component not found!");
//             }
//         }
//         
//         private void OnEnable()
//         {
//             SubscribeToInventoryEvents();
//         }
//         
//         private void OnDisable()
//         {
//             UnsubscribeFromInventoryEvents();
//         }
//         
//         #endregion
//         
//         #region Event Subscription
//         
//         private void SubscribeToInventoryEvents()
//         {
//             if (inventorySystem == null) return;
//             
//             // Equipment events
//             inventorySystem.OnItemEquipped += HandleItemEquipped;
//             inventorySystem.OnItemUnequipped += HandleItemUnequipped;
//             
//             // Weapon events
//             inventorySystem.OnWeaponEquipped += HandleWeaponEquipped;
//             inventorySystem.OnWeaponUnequipped += HandleWeaponUnequipped;
//             
//             // Attachment events
//             inventorySystem.OnAttachmentAdded += HandleAttachmentAdded;
//             inventorySystem.OnAttachmentRemoved += HandleAttachmentRemoved;
//         }
//         
//         private void UnsubscribeFromInventoryEvents()
//         {
//             if (inventorySystem == null) return;
//             
//             inventorySystem.OnItemEquipped -= HandleItemEquipped;
//             inventorySystem.OnItemUnequipped -= HandleItemUnequipped;
//             inventorySystem.OnWeaponEquipped -= HandleWeaponEquipped;
//             inventorySystem.OnWeaponUnequipped -= HandleWeaponUnequipped;
//             inventorySystem.OnAttachmentAdded -= HandleAttachmentAdded;
//             inventorySystem.OnAttachmentRemoved -= HandleAttachmentRemoved;
//         }
//         
//         #endregion
//         
//         #region Event Handlers - Equipment
//         
//         private void HandleItemEquipped(ItemInstance item, EquipmentSlotType slotType)
//         {
//             Log($"Equipment equipped: {item.Definition.DisplayName}");
//             ApplyItemModifiers(item);
//             
//             // Also apply modifiers from attachments on this item
//             ApplyAttachmentModifiersRecursive(item);
//         }
//         
//         private void HandleItemUnequipped(ItemInstance item, EquipmentSlotType slotType)
//         {
//             Log($"Equipment unequipped: {item.Definition.DisplayName}");
//             RemoveItemModifiers(item);
//             
//             // Also remove modifiers from attachments on this item
//             RemoveAttachmentModifiersRecursive(item);
//         }
//         
//         #endregion
//         
//         #region Event Handlers - Weapons
//         
//         private void HandleWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
//         {
//             Log($"Weapon equipped: {weapon.Definition.DisplayName}");
//             ApplyItemModifiers(weapon);
//             
//             // Apply modifiers from weapon attachments
//             ApplyAttachmentModifiersRecursive(weapon);
//         }
//         
//         private void HandleWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType)
//         {
//             Log($"Weapon unequipped: {weapon.Definition.DisplayName}");
//             RemoveItemModifiers(weapon);
//             
//             // Remove modifiers from weapon attachments
//             RemoveAttachmentModifiersRecursive(weapon);
//         }
//         
//         #endregion
//         
//         #region Event Handlers - Attachments
//         
//         private void HandleAttachmentAdded(ItemInstance parentItem, ItemInstance attachment, AttachmentSlotType slotType)
//         {
//             Log($"Attachment added: {attachment.Definition.DisplayName} to {parentItem.Definition.DisplayName}");
//             
//             // Check if parent item is equipped
//             if (IsItemEquipped(parentItem))
//             {
//                 // Apply attachment modifiers
//                 ApplyItemModifiers(attachment);
//             }
//         }
//         
//         private void HandleAttachmentRemoved(ItemInstance parentItem, ItemInstance attachment, AttachmentSlotType slotType)
//         {
//             Log($"Attachment removed: {attachment.Definition.DisplayName} from {parentItem.Definition.DisplayName}");
//             
//             // Check if parent item is equipped
//             if (IsItemEquipped(parentItem))
//             {
//                 // Remove attachment modifiers
//                 RemoveItemModifiers(attachment);
//             }
//         }
//         
//         #endregion
//         
//         #region Modifier Application
//         
//         /// <summary>
//         /// Apply stat modifiers from an item.
//         /// Gets modifiers from ItemDefinition.StatModifiers array.
//         /// </summary>
//         public void ApplyItemModifiers(ItemInstance item)
//         {
//             if (item?.Definition == null) return;
//             
//             // Get modifiers from ItemDefinition
//             var modifierDefinitions = item.Definition.StatModifiers;
//             if (modifierDefinitions == null || modifierDefinitions.Length == 0)
//             {
//                 Log($"Item {item.Definition.DisplayName} has no stat modifiers");
//                 return;
//             }
//             
//             // Convert to StatModifierData
//             var modifiers = new List<StatModifierData>();
//             foreach (var def in modifierDefinitions)
//             {
//                 modifiers.Add(def.ToStatModifierData());
//             }
//             
//             // Store modifiers
//             activeModifiers[item.InstanceId] = new List<StatModifierData>(modifiers);
//             
//             // Fire events for each modifier
//             foreach (var modifier in modifiers)
//             {
//                 OnModifierAdded?.Invoke(modifier, item.InstanceId);
//                 Log($"Applied modifier: {modifier.GetDisplayString()} from {item.Definition.DisplayName}");
//             }
//             
//             Log($"Applied {modifiers.Count} modifiers from {item.Definition.DisplayName}");
//             
//             // Notify stats changed
//             NotifyStatsChanged();
//         }
//         
//         /// <summary>
//         /// Apply modifiers from all attachments on an item recursively.
//         /// </summary>
//         private void ApplyAttachmentModifiersRecursive(ItemInstance item)
//         {
//             if (item == null || item.AttachedItems  == null) return;
//             
//             foreach (var attachment in item.AttachedItems )
//             {
//                 if (attachment != null)
//                 {
//                     ApplyItemModifiers(attachment);
//                 }
//             }
//         }
//         
//         #endregion
//         
//         #region Modifier Removal
//         
//         /// <summary>
//         /// Remove all stat modifiers from an item.
//         /// </summary>
//         public void RemoveItemModifiers(ItemInstance item)
//         {
//             if (item == null) return;
//             
//             if (!activeModifiers.ContainsKey(item.InstanceId))
//             {
//                 return;
//             }
//             
//             var modifiers = activeModifiers[item.InstanceId];
//             
//             // Fire events for each modifier
//             foreach (var modifier in modifiers)
//             {
//                 OnModifierRemoved?.Invoke(modifier, item.InstanceId);
//                 Log($"Removed modifier: {modifier.GetDisplayString()} from {item.Definition.DisplayName}");
//             }
//             
//             // Fire all-removed event
//             OnAllModifiersRemovedForSource?.Invoke(item.InstanceId);
//             
//             // Clear stored modifiers
//             activeModifiers.Remove(item.InstanceId);
//             
//             Log($"Removed {modifiers.Count} modifiers from {item.Definition.DisplayName}");
//             
//             // Notify stats changed
//             NotifyStatsChanged();
//         }
//         
//         /// <summary>
//         /// Remove modifiers from all attachments on an item recursively.
//         /// </summary>
//         private void RemoveAttachmentModifiersRecursive(ItemInstance item)
//         {
//             if (item == null || item.AttachedItems  == null) return;
//             
//             foreach (var attachment in item.AttachedItems )
//             {
//                 if (attachment != null)
//                 {
//                     RemoveItemModifiers(attachment);
//                 }
//             }
//         }
//         
//         #endregion
//         
//         #region Query Methods
//         
//         /// <summary>
//         /// Check if an item is currently equipped.
//         /// </summary>
//         private bool IsItemEquipped(ItemInstance item)
//         {
//             if (inventorySystem == null) return false;
//             
//             // Check equipment slots
//             foreach (EquipmentSlotType slotType in System.Enum.GetValues(typeof(EquipmentSlotType)))
//             {
//                 var equippedItem = inventorySystem.GetEquippedItem(slotType);
//                 if (equippedItem?.InstanceId == item.InstanceId)
//                 {
//                     return true;
//                 }
//             }
//             
//             // Check weapon slots
//             foreach (WeaponSlotType slotType in System.Enum.GetValues(typeof(WeaponSlotType)))
//             {
//                 var equippedWeapon = inventorySystem.GetEquippedWeapon(slotType);
//                 if (equippedWeapon?.InstanceId == item.InstanceId)
//                 {
//                     return true;
//                 }
//             }
//             
//             return false;
//         }
//         
//         /// <summary>
//         /// Get all active modifiers.
//         /// </summary>
//         public Dictionary<string, List<StatModifierData>> GetActiveModifiers()
//         {
//             return new Dictionary<string, List<StatModifierData>>(activeModifiers);
//         }
//         
//         /// <summary>
//         /// Get total modifier value for a specific CharacterStatType.
//         /// </summary>
//         public float GetTotalModifier(CharacterStatType statType)
//         {
//             float total = 0f;
//             
//             foreach (var kvp in activeModifiers)
//             {
//                 foreach (var modifier in kvp.Value)
//                 {
//                     if (modifier.Target == StatModifierTarget.Character && 
//                         modifier.CharacterStat == statType)
//                     {
//                         total += modifier.Value;
//                     }
//                 }
//             }
//             
//             return total;
//         }
//         
//         /// <summary>
//         /// Get all modifiers for a specific stat type.
//         /// </summary>
//         public List<StatModifierData> GetModifiersForStat(CharacterStatType statType)
//         {
//             var result = new List<StatModifierData>();
//             
//             foreach (var kvp in activeModifiers)
//             {
//                 foreach (var modifier in kvp.Value)
//                 {
//                     if (modifier.Target == StatModifierTarget.Character && 
//                         modifier.CharacterStat == statType)
//                     {
//                         result.Add(modifier);
//                     }
//                 }
//             }
//             
//             return result;
//         }
//         
//         #endregion
//         
//         #region Debug Helpers
//         
//         /// <summary>
//         /// Log all active modifiers for debugging.
//         /// </summary>
//         [ContextMenu("Log Active Modifiers")]
//         public void LogActiveModifiers()
//         {
//             if (activeModifiers.Count == 0)
//             {
//                 Debug.Log("[PlayerStatsManager] No active modifiers");
//                 return;
//             }
//             
//             Debug.Log("=== Active Stat Modifiers ===");
//             foreach (var kvp in activeModifiers)
//             {
//                 Debug.Log($"Source: {kvp.Key}");
//                 foreach (var modifier in kvp.Value)
//                 {
//                     Debug.Log($"  - {modifier.GetDisplayString()}");
//                 }
//             }
//         }
//         
//         /// <summary>
//         /// Clear all modifiers (for testing).
//         /// </summary>
//         [ContextMenu("Clear All Modifiers")]
//         public void ClearAllModifiers()
//         {
//             foreach (var kvp in activeModifiers)
//             {
//                 OnAllModifiersRemovedForSource?.Invoke(kvp.Key);
//             }
//             
//             activeModifiers.Clear();
//             NotifyStatsChanged();
//             
//             Debug.Log("[PlayerStatsManager] All modifiers cleared");
//         }
//         
//         #endregion
//         
//         private void NotifyStatsChanged()
//         {
//             inventorySystem.NotifyStatsChanged();
//         }
//         
//         #region Logging
//         
//         private void Log(string message)
//         {
//             if (enableDebugLogs)
//             {
//                 Debug.Log($"[PlayerStatsManager] {message}");
//             }
//         }
//         
//         private void LogError(string message)
//         {
//             Debug.LogError($"[PlayerStatsManager] {message}");
//         }
//         
//         
//         #endregion
//     }
// }