using System.Collections;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.QuickSlot
{
    /// <summary>
    /// Handles consumable usage with progress bar and cancellation.
    /// </summary>
    public class ConsumableUsage : MonoBehaviour
    {
        [SerializeField] private float consumeDuration = 2f;
        [SerializeField] private ProgressBarUI progressBar;
        
        private Coroutine consumeCoroutine;
        private ItemInstance currentItem;
        
        void OnEnable()
        {
            QuickSlotEvents.OnRequestConsume += StartConsume;
            // TODO: Subscribe to PlayerEvents.OnPlayerDamaged when available
        }
        
        void OnDisable()
        {
            QuickSlotEvents.OnRequestConsume -= StartConsume;
            // TODO: Unsubscribe from PlayerEvents.OnPlayerDamaged
        }
        
        public void StartConsume(ItemInstance item)
        {
            if (consumeCoroutine != null)
            {
                StopCoroutine(consumeCoroutine);
            }
            
            currentItem = item;
            consumeCoroutine = StartCoroutine(ConsumeRoutine(item));
        }
        
        private IEnumerator ConsumeRoutine(ItemInstance item)
        {
            float elapsed = 0f;
            progressBar.Show();
            
            while (elapsed < consumeDuration)
            {
                // Check cancel with ESC
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelConsume("Player cancelled");
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                float progress = elapsed / consumeDuration;
                progressBar.UpdateProgress(progress);
                
                yield return null;
            }
            
            // Complete
            CompleteConsume(item);
        }
        
        private void OnDamageReceived(float damage)
        {
            if (consumeCoroutine != null)
            {
                CancelConsume($"Took {damage} damage");
            }
        }
        
        private void CancelConsume(string reason)
        {
            Debug.Log($"[Consumable] Cancelled: {reason}");
            
            if (consumeCoroutine != null)
            {
                StopCoroutine(consumeCoroutine);
                consumeCoroutine = null;
            }
            
            progressBar.Hide();
            QuickSlotEvents.FireConsumeCancelled(currentItem, reason);
        }
        
        private void CompleteConsume(ItemInstance item)
        {
            Debug.Log($"[Consumable] Completed: {item.Definition.ItemId}");
            
            progressBar.Hide();
            consumeCoroutine = null;
            
            // Apply consume effect (health restoration, etc.)
            ApplyConsumeEffect(item);
            
            // Fire completion event
            QuickSlotEvents.FireConsumeComplete(item);
        }
        
        private void ApplyConsumeEffect(ItemInstance item)
        {
            // TODO: Implement consumable effects system
            // For now, fire event for external systems to handle
            // ConsumableEvents.OnApplyEffect?.Invoke(item);
        }
    }
}
