using _Night_Hunt.Scripts.Gameplay.Systems.Inventory.Components;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Interaction;
using UnityEngine;

namespace NightHunt.InteractionSystem.Loot
{
   public class LootTransferHandler : NetworkBehaviour
{
    public static LootTransferHandler Instance { get; private set; }
    
    private ContainerInteractable currentContainer;
    private GridInventoryComponent playerInventory;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    public void Initialize(ContainerInteractable container, GridInventoryComponent inventory)
    {
        currentContainer = container;
        playerInventory = inventory;
    }
    
    public void TransferItem(ItemInstance item, LootItemSlotUI.SlotType fromType)
    {
        if (fromType == LootItemSlotUI.SlotType.Container)
        {
            // Container → Player
            TransferContainerToPlayer(item);
        }
        else
        {
            // Player → Container
            TransferPlayerToContainer(item);
        }
    }
    
    private void TransferContainerToPlayer(ItemInstance item)
    {
        if (currentContainer == null || playerInventory == null) return;
        
        // Request server transfer
        ServerTransferToPlayer(item.instanceId);
    }
    
    private void TransferPlayerToContainer(ItemInstance item)
    {
        if (currentContainer == null || playerInventory == null) return;
        
        // Request server transfer
        ServerTransferToContainer(item.instanceId);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ServerTransferToPlayer(string itemId)
    {
        // Validation
        if (!currentContainer.TransferItemToPlayer(LocalConnection, itemId))
        {
            TargetShowError(LocalConnection, "Transfer failed");
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ServerTransferToContainer(string itemId)
    {
        // TODO: Implement player → container transfer
    }
    
    [TargetRpc]
    private void TargetShowError(NetworkConnection conn, string message)
    {
        Debug.LogError(message);
    }
}
}