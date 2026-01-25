using System;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Interaction
{
     public class HoldInteractionHandler : MonoBehaviour
     {
          [Header("Settings")]
          [SerializeField] private float updateInterval = 0.05f; // 20Hz
    
          private InteractionProgress currentProgress;
          private IInteractable currentInteractable;
          private bool isHolding;
          private float lastUpdateTime;
    
          public event Action<float> OnProgressUpdated; // 0-1
          public event Action OnHoldCompleted;
          public event Action OnHoldCanceled;
    
          public void StartHold(IInteractable interactable, float requiredDuration)
          {
               if (interactable == null) return;
        
               currentInteractable = interactable;
               isHolding = true;
        
               currentProgress = new InteractionProgress
               {
                    requiredDuration = requiredDuration,
                    currentDuration = 0f,
                    isCompleted = false
               };
        
               lastUpdateTime = Time.time;
          }
    
          public void CancelHold()
          {
               if (!isHolding) return;
        
               isHolding = false;
               currentProgress = default;
               OnHoldCanceled?.Invoke();
          }
    
          private void Update()
          {
               if (!isHolding) return;
        
               currentProgress.currentDuration += Time.deltaTime;
        
               // Update UI at fixed interval
               if (Time.time - lastUpdateTime >= updateInterval)
               {
                    OnProgressUpdated?.Invoke(currentProgress.Progress);
                    lastUpdateTime = Time.time;
               }
        
               // Check completion
               if (currentProgress.currentDuration >= currentProgress.requiredDuration)
               {
                    CompleteHold();
               }
          }
    
          private void CompleteHold()
          {
               isHolding = false;
               currentProgress.isCompleted = true;
        
               OnHoldCompleted?.Invoke();
               OnProgressUpdated?.Invoke(1f);
          }
    
          public float GetProgress() => currentProgress.Progress;
          public bool IsHolding => isHolding;
     }
}