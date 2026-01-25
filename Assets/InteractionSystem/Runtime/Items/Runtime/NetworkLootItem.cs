using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
     public class NetworkLootItem : NetworkBehaviour, IPickupable
     {
          [SyncVar] private string itemDataId;
          [SyncVar] private int quantity;
    
          private ItemDataBase cachedItemData;
    
          public ItemDataBase ItemData
          {
               get
               {
                    if (cachedItemData == null)
                    {
                         cachedItemData = ItemDatabaseManager.Instance.GetItemData(itemDataId);
                    }
                    return cachedItemData;
               }
          }
    
          public int Quantity => quantity;
          public Vector3 WorldPosition => transform.position;
    
          public void Initialize(string dataId, int qty)
          {
               itemDataId = dataId;
               quantity = qty;
        
               // Spawn visual
               if (ItemData != null && ItemData.worldPrefab != null)
               {
                    Instantiate(ItemData.worldPrefab, transform);
               }
          }
    
          public bool CanPickup(NetworkConnection player)
          {
               // Can always pickup (validation done in PickupHandler)
               return true;
          }
    
          [Server]
          public void OnPickedUp(NetworkConnection player)
          {
               // Despawn this network object
               ServerManager.Despawn(gameObject);
          }
     }
}