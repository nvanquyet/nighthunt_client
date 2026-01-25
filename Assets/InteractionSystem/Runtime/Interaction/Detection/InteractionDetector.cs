using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Input;
using UnityEngine;

namespace NightHunt.InteractionSystem.Interaction
{
     public class InteractionDetector : NetworkBehaviour
     {
          [Header("Detection")]
          [SerializeField] private Camera playerCamera;
          [SerializeField] private float detectionRange = 5f;
          [SerializeField] private LayerMask interactableLayer;
    
          private IInteractable currentTarget;
          private InputRouter inputRouter;
    
          private void Awake()
          {
               inputRouter = GetComponent<InputRouter>();
          }
    
          private void Update()
          {
               if (!IsOwner) return;
        
               DetectInteractables();
          }
    
          private void DetectInteractables()
          {
               Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
               if (Physics.Raycast(ray, out RaycastHit hit, detectionRange, interactableLayer))
               {
                    IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            
                    if (interactable != null)
                    {
                         float distance = Vector3.Distance(playerCamera.transform.position, hit.point);
                
                         if (distance <= interactable.InteractionDistance && interactable.CanInteract(LocalConnection))
                         {
                              if (currentTarget != interactable)
                              {
                                   OnTargetChanged(interactable);
                              }
                              return;
                         }
                    }
               }
        
               if (currentTarget != null)
               {
                    OnTargetChanged(null);
               }
          }
    
          private void OnTargetChanged(IInteractable newTarget)
          {
               currentTarget = newTarget;
               inputRouter.SetCurrentInteractable(newTarget);
        
               // Notify UI
               InteractionUIController.Instance?.ShowPrompt(newTarget);
          }
     }
}