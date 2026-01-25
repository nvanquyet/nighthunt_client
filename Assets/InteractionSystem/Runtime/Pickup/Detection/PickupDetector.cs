using FishNet.Object;
using NightHunt.Gameplay.Interaction.Inputs;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Pickup
{
     public class PickupDetector : NetworkBehaviour
     {
          [Header("Detection")]
          [SerializeField] private Camera playerCamera;
          [SerializeField] private float detectionRange = 5f;
          [SerializeField] private LayerMask pickupLayer;
    
          [Header("Settings")]
          [SerializeField] private PickupSettings settings;
    
          private IPickupable currentTarget;
          private InputRouter inputRouter;
    
          private void Awake()
          {
               inputRouter = GetComponent<InputRouter>();
          }
    
          private void Update()
          {
               if (!IsOwner) return;
        
               DetectPickupables();
          }
    
          private void DetectPickupables()
          {
               Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        
               if (Physics.Raycast(ray, out RaycastHit hit, detectionRange, pickupLayer))
               {
                    IPickupable pickupable = hit.collider.GetComponent<IPickupable>();
            
                    if (pickupable != null && pickupable.CanPickup(LocalConnection))
                    {
                         if (currentTarget != pickupable)
                         {
                              OnTargetChanged(pickupable);
                         }
                         return;
                    }
               }
        
               // No valid target
               if (currentTarget != null)
               {
                    OnTargetChanged(null);
               }
          }
    
          private void OnTargetChanged(IPickupable newTarget)
          {
               currentTarget = newTarget;
               inputRouter.SetCurrentPickupable(newTarget);
        
               // Notify highlight system
               PickupHighlighter.Instance?.Highlight(newTarget);
          }
     }
}