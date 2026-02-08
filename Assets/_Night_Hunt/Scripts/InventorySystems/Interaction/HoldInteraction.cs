// using UnityEngine;
// using NightHunt.Inventory.Core.Events;
//
// namespace NightHunt.Inventory.Interaction
// {
//     /// <summary>
//     /// Manages hold-to-interact mechanic with multiple interrupt conditions.
//     /// </summary>
//     public class HoldInteraction
//     {
//         private HoldInteractionConfig config;
//         private Vector3 startPosition;
//         private float holdProgress;
//         private bool isHolding;
//
//         public HoldInteraction(HoldInteractionConfig config)
//         {
//             this.config = config;
//         }
//
//         /// <summary>
//         /// Starts a hold interaction.
//         /// </summary>
//         public void StartHold(Vector3 playerPos)
//         {
//             startPosition = playerPos;
//             holdProgress = 0f;
//             isHolding = true;
//
//             // Subscribe to damage events if enabled
//             if (config.interruptOnDamage)
//             {
//                 PlayerEvents.OnPlayerDamaged += OnDamageReceived;
//             }
//
//             Debug.Log($"[HoldInteraction] Started (duration: {config.holdDuration}s)");
//         }
//
//         /// <summary>
//         /// Updates the hold interaction.
//         /// Returns true if still holding, false if cancelled or completed.
//         /// </summary>
//         public bool UpdateHold(float deltaTime, Vector3 currentPos, bool isKeyPressed)
//         {
//             if (!isHolding) return false;
//
//             // Check release key
//             if (config.interruptOnReleaseKey && !isKeyPressed)
//             {
//                 CancelHold("Released key");
//                 return false;
//             }
//
//             // Check move distance
//             if (config.interruptOnMoveTooFar)
//             {
//                 float dist = Vector3.Distance(startPosition, currentPos);
//                 if (dist > config.maxMoveDistance)
//                 {
//                     CancelHold($"Moved too far ({dist:F2}m > {config.maxMoveDistance}m)");
//                     return false;
//                 }
//             }
//
//             // Update progress
//             holdProgress += deltaTime / config.holdDuration;
//
//             // Check completion
//             if (holdProgress >= 1f)
//             {
//                 CompleteHold();
//                 return false;
//             }
//
//             return true; // Continue holding
//         }
//
//         /// <summary>
//         /// Cancels the hold interaction.
//         /// </summary>
//         public void Cancel()
//         {
//             CancelHold("Manually cancelled");
//         }
//
//         /// <summary>
//         /// Gets current hold progress (0-1).
//         /// </summary>
//         public float GetProgress() => holdProgress;
//
//         /// <summary>
//         /// Checks if currently holding.
//         /// </summary>
//         public bool IsHolding() => isHolding;
//
//         #region Internal Methods
//
//         private void OnDamageReceived(float damage)
//         {
//             CancelHold($"Took {damage} damage");
//         }
//
//         private void CancelHold(string reason)
//         {
//             Debug.Log($"[HoldInteraction] Cancelled: {reason}");
//             isHolding = false;
//             Cleanup();
//             InteractionEvents.InvokeHoldCancelled(reason);
//         }
//
//         private void CompleteHold()
//         {
//             Debug.Log("[HoldInteraction] Completed!");
//             isHolding = false;
//             Cleanup();
//             InteractionEvents.InvokeHoldCompleted();
//         }
//
//         private void Cleanup()
//         {
//             if (config.interruptOnDamage)
//             {
//                 PlayerEvents.OnPlayerDamaged -= OnDamageReceived;
//             }
//         }
//
//         #endregion
//     }
// }