using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using UnityEngine.InputSystem;

namespace NightHunt.InteractionSystem.Input
{
     public class InputRouter : NetworkBehaviour
     {
          private PlayerInputActions inputActions;
          private IInteractable currentInteractable;
          private IPickupable currentPickupable;
    
          private void Awake()
          {
               inputActions = new PlayerInputActions();
          }
    
          public override void OnStartClient()
          {
               base.OnStartClient();
               if (!IsOwner) return;
        
               inputActions.Gameplay.Interact.performed += OnInteractPerformed;
               inputActions.Gameplay.Interact.canceled += OnInteractCanceled;
               inputActions.Gameplay.Pickup.performed += OnPickupPerformed;
               inputActions.Enable();
          }
    
          public override void OnStopClient()
          {
               base.OnStopClient();
               inputActions?.Disable();
          }
    
          private void OnInteractPerformed(InputAction.CallbackContext ctx)
          {
               if (currentInteractable == null) return;
        
               if (currentInteractable.InteractionType == InteractionType.Hold)
               {
                    currentInteractable.OnInteractStart(LocalConnection);
               }
               else
               {
                    currentInteractable.OnInteract(LocalConnection);
               }
          }
    
          private void OnInteractCanceled(InputAction.CallbackContext ctx)
          {
               currentInteractable?.OnInteractCancel(LocalConnection);
          }
    
          private void OnPickupPerformed(InputAction.CallbackContext ctx)
          {
               if (currentPickupable == null) return;
               // Will connect to PickupHandler
          }
    
          public void SetCurrentInteractable(IInteractable interactable)
          {
               currentInteractable = interactable;
          }
    
          public void SetCurrentPickupable(IPickupable pickupable)
          {
               currentPickupable = pickupable;
          }
     }
}