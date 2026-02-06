using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Utilities;
using System.Collections;
using NightHunt.Inventory.Input;
using UnityEngine.Serialization;

namespace NightHunt.Inventory.Domain.QuickSlot
{
    /// <summary>
    /// Handles consumable usage with progress bar using Unity New Input System.
    /// Can be cancelled by damage, ESC key, or movement.
    /// </summary>
    public class ConsumableUsage : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float consumeDuration = 2f;
        
        [FormerlySerializedAs("inputActions")]
        [Header("Input")]
        [SerializeField] private InventoryInputHandler inventoryHandler;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Coroutine consumeCoroutine;
        private ItemInstance currentItem;
        private Vector3 startPosition;
        private float maxMoveDistance = 1f;
        
        #region Lifecycle
        
        void OnEnable()
        {
            // Enable input
            inventoryHandler.EnableInput();
            
            // Subscribe to cancel input
            inventoryHandler.OnCancel += OnCancelPerformed;
            
            // Subscribe to events
            QuickSlotEvents.OnRequestConsume += StartConsume;
            PlayerEvents.OnPlayerDamaged += OnDamageReceived;
        }
        
        void OnDisable()
        {
            // Unsubscribe from input
            inventoryHandler.OnCancel -= OnCancelPerformed;
            
            // Disable input
            inventoryHandler.DisableInput();
            
            // Unsubscribe from events
            QuickSlotEvents.OnRequestConsume -= StartConsume;
            PlayerEvents.OnPlayerDamaged -= OnDamageReceived;
        }
        
        void Update()
        {
            if (consumeCoroutine == null) return;
            
            // Check move distance
            float dist = Vector3.Distance(startPosition, transform.position);
            if (dist > maxMoveDistance)
            {
                CancelConsume($"Moved too far ({dist:F2}m)");
            }
        }
        
        #endregion
        
        #region Input Callbacks
        
        private void OnCancelPerformed()
        {
            if (consumeCoroutine != null)
            {
                CancelConsume("Player pressed ESC");
            }
        }
        
        #endregion
        
        #region Consume System
        
        private void StartConsume(ItemInstance item)
        {
            if (item == null) return;
            
            // Cancel existing consume
            if (consumeCoroutine != null)
            {
                StopCoroutine(consumeCoroutine);
            }
            
            currentItem = item;
            startPosition = transform.position;
            consumeCoroutine = StartCoroutine(ConsumeRoutine(item));
            
            InventoryLogger.Log("ConsumableUsage", $"Started consuming {item.Definition.ItemId}", enableDebugLogs);
        }
        
        private IEnumerator ConsumeRoutine(ItemInstance item)
        {
            float elapsed = 0f;
            
            // Fire consume started event (for UI to show progress bar)
            QuickSlotEvents.InvokeConsumeStarted();
            
            while (elapsed < consumeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / consumeDuration;
                
                // Fire progress event (for UI to update progress bar)
                QuickSlotEvents.InvokeConsumeProgress(progress);
                
                yield return null;
            }
            
            // Complete
            CompleteConsume(item);
        }
        
        private void CompleteConsume(ItemInstance item)
        {
            InventoryLogger.Log("ConsumableUsage", $"Completed: {item.Definition.ItemId}", enableDebugLogs);
            
            // Fire consume completed event (for UI to hide progress bar)
            QuickSlotEvents.InvokeConsumeCompleted();
            
            consumeCoroutine = null;
            
            // Apply consume effect
            ApplyConsumeEffect(item);
            
            // Fire completion event
            QuickSlotEvents.InvokeConsumeComplete(item);
            
            // Reduce stack or remove item
            item.StackSize--;
            if (item.StackSize <= 0)
            {
                // Remove from inventory
                InventoryEvents.InvokeRequestRemoveItem(item.InstanceId);
            }
            else
            {
                // Update stack size
                InventoryEvents.InvokeStackSizeChanged(item);
            }
        }
        
        private void CancelConsume(string reason)
        {
            InventoryLogger.Log("ConsumableUsage", $"Cancelled: {reason}", enableDebugLogs);
            
            if (consumeCoroutine != null)
            {
                StopCoroutine(consumeCoroutine);
                consumeCoroutine = null;
            }
            
            // Fire consume completed event (for UI to hide progress bar)
            QuickSlotEvents.InvokeConsumeCompleted();
            
            QuickSlotEvents.InvokeConsumeCancelled(currentItem, reason);
        }
        
        #endregion
        
        #region Effect Application
        
        private void ApplyConsumeEffect(ItemInstance item)
        {
            // Apply stat modifiers (if any)
            if (item.Definition.CharacterStatModifiers != null)
            {
                foreach (var modifier in item.Definition.CharacterStatModifiers)
                {
                    // Fire event for stats system to handle
                    // For consumables, we apply effects immediately (not persistent modifiers)
                    ConsumableEvents.InvokeApplyEffect(item);
                }
            }
            
            // TODO: Implement specific consumable effects system
            // For now, just fire event for external systems to handle
            
            InventoryLogger.Log("ConsumableUsage", $"Applied effects for {item.Definition.ItemId}", enableDebugLogs);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnDamageReceived(float damage)
        {
            if (consumeCoroutine != null)
            {
                CancelConsume($"Took {damage} damage");
            }
        }
        
        #endregion
    }
}