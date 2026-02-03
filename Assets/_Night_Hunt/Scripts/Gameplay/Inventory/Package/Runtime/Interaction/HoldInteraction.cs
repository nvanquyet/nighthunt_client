using UnityEngine;
using NightHunt.Inventory.Interaction;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// Interruptible hold interaction with multiple conditions.
    /// </summary>
    public class HoldInteraction
    {
        private HoldInteractionConfig config;
        private Vector3 startPosition;
        private float holdProgress;
        private bool isHolding;
        
        public HoldInteraction(HoldInteractionConfig config)
        {
            this.config = config;
        }
        
        public void StartHold(Vector3 playerPos)
        {
            startPosition = playerPos;
            holdProgress = 0f;
            isHolding = true;
            
            // Subscribe to damage events if enabled
            if (config.interruptOnDamage)
            {
                // TODO: Subscribe to PlayerEvents.OnPlayerDamaged when available
            }
        }
        
        public bool UpdateHold(float deltaTime, Vector3 currentPos, bool isKeyPressed)
        {
            if (!isHolding) return false;
            
            // Check release key
            if (config.interruptOnReleaseKey && !isKeyPressed)
            {
                CancelHold("Released key");
                return false;
            }
            
            // Check move distance
            if (config.interruptOnMoveTooFar)
            {
                float dist = Vector3.Distance(startPosition, currentPos);
                if (dist > config.maxMoveDistance)
                {
                    CancelHold($"Moved too far ({dist:F2}m > {config.maxMoveDistance}m)");
                    return false;
                }
            }
            
            // Update progress
            holdProgress += deltaTime / config.holdDuration;
            
            // Check completion
            if (holdProgress >= 1f)
            {
                CompleteHold();
                return false;
            }
            
            return true; // Continue holding
        }
        
        private void CancelHold(string reason)
        {
            Debug.Log($"[HoldInteraction] Cancelled: {reason}");
            isHolding = false;
            Cleanup();
            InteractionEvents.FireHoldCancelled(reason);
        }
        
        private void CompleteHold()
        {
            Debug.Log("[HoldInteraction] Completed!");
            isHolding = false;
            Cleanup();
            InteractionEvents.FireHoldCompleted();
        }
        
        private void Cleanup()
        {
            if (config.interruptOnDamage)
            {
                // TODO: Unsubscribe from PlayerEvents.OnPlayerDamaged when available
            }
        }
        
        public float GetProgress() => holdProgress;
        public bool IsHolding() => isHolding;
    }
}
