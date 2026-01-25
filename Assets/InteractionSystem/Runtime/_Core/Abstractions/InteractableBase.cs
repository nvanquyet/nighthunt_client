using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core
{
     public abstract class InteractableBase : NetworkBehaviour, IInteractable
     {
          [SerializeField] protected string interactionPrompt = "Press E";
          [SerializeField] protected InteractionType interactionType = InteractionType.Immediate;
          [SerializeField] protected float interactionDistance = 3f;
          [SerializeField] protected float holdDuration = 0f; // For Hold type
    
          protected InteractionProgress currentProgress;
    
          public string InteractionPrompt => interactionPrompt;
          public InteractionType InteractionType => interactionType;
          public float InteractionDistance => interactionDistance;
    
          public virtual bool CanInteract(NetworkConnection player)
          {
               // Base validation: distance, line of sight
               return true;
          }
    
          public abstract void OnInteract(NetworkConnection player);
    
          public virtual void OnInteractStart(NetworkConnection player)
          {
               if (interactionType == InteractionType.Hold)
               {
                    currentProgress = new InteractionProgress
                    {
                         requiredDuration = holdDuration,
                         interactor = player
                    };
               }
          }
    
          public virtual void OnInteractCancel(NetworkConnection player)
          {
               currentProgress = default;
          }
    
          protected virtual void Update()
          {
               if (interactionType == InteractionType.Hold && currentProgress.interactor != null)
               {
                    UpdateHoldProgress();
               }
          }
    
          protected virtual void UpdateHoldProgress()
          {
               currentProgress.currentDuration += Time.deltaTime;
               if (currentProgress.isCompleted)
               {
                    OnInteract(currentProgress.interactor);
                    currentProgress = default;
               }
          }
     }
}