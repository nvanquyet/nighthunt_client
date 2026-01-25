using System.Collections.Generic;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Pickup
{
     public class AutoPickupTrigger : NetworkBehaviour
     {
          [SerializeField] private SphereCollider triggerCollider;
          [SerializeField] private PickupSettings settings;
    
          private PickupHandler pickupHandler;
          private HashSet<IPickupable> detectedItems = new HashSet<IPickupable>();
    
          private void Awake()
          {
               pickupHandler = GetComponent<PickupHandler>();
        
               if (triggerCollider == null)
               {
                    triggerCollider = gameObject.AddComponent<SphereCollider>();
                    triggerCollider.isTrigger = true;
               }
          }
    
          private void Start()
          {
               UpdateTriggerRadius();
          }
    
          private void OnTriggerEnter(Collider other)
          {
               if (!IsOwner) return;
               if (!settings.autoPickupEnabled) return;
        
               IPickupable pickupable = other.GetComponent<IPickupable>();
               if (pickupable == null) return;
        
               if (settings.ShouldAutoPickup(pickupable.ItemData))
               {
                    detectedItems.Add(pickupable);
                    TryAutoPickup(pickupable);
               }
          }
    
          private void OnTriggerExit(Collider other)
          {
               IPickupable pickupable = other.GetComponent<IPickupable>();
               if (pickupable != null)
               {
                    detectedItems.Remove(pickupable);
               }
          }
    
          private void TryAutoPickup(IPickupable pickupable)
          {
               if (!pickupable.CanPickup(LocalConnection)) return;
        
               pickupHandler.RequestAutoPickup(pickupable);
          }
    
          public void UpdateTriggerRadius()
          {
               if (triggerCollider != null)
               {
                    triggerCollider.radius = settings.autoPickupRadius;
               }
          }
    
          private void OnValidate()
          {
               UpdateTriggerRadius();
          }
     }
}