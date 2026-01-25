using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Loot;
using UnityEngine;

namespace NightHunt.InteractionSystem.Interaction
{
    public class ContainerInteractable : InteractableBase
    {
        [Header("Container")] [SerializeField] private LootContainer lootContainer;
        [SerializeField] private bool requireKey;
        [SerializeField] private string requiredKeyId;

        [Header("Visual")] [SerializeField] private Animator containerAnimator;
        [SerializeField] private string openAnimTrigger = "Open";

        [SyncVar(OnChange = nameof(OnOpenStateChanged))]
        private bool isOpened;

        private readonly HashSet<NetworkConnection> currentViewers = new HashSet<NetworkConnection>();

        private void Start()
        {
            interactionType = InteractionType.Container;
            interactionPrompt = lootContainer.IsLocked ? "Locked" : "Open";
        }

        public override bool CanInteract(NetworkConnection player)
        {
            if (lootContainer.IsLocked)
            {
                if (requireKey)
                {
                    // Check if player has key
                    // TODO: Implement key check via inventory
                    return false;
                }

                return false;
            }

            return base.CanInteract(player);
        }

        public override void OnInteract(NetworkConnection player)
        {
            if (!IsServer) return;

            ServerOpenContainer(player);
        }

        [Server]
        private void ServerOpenContainer(NetworkConnection player)
        {
            if (!lootContainer.CanLoot(player)) return;

            currentViewers.Add(player);

            if (!isOpened)
            {
                isOpened = true;
            }

            // Send loot data to client
            TargetOpenLootUI(player, lootContainer.ContainerItems.ToArray());
        }

        [TargetRpc]
        private void TargetOpenLootUI(NetworkConnection conn, ItemInstance[] items)
        {
            LootContainerUI.Instance?.Open(this, items);
        }

        [Server]
        public void OnPlayerClosedUI(NetworkConnection player)
        {
            currentViewers.Remove(player);

            // If no one viewing, close container
            if (currentViewers.Count == 0)
            {
                isOpened = false;
            }
        }

        private void OnOpenStateChanged(bool oldValue, bool newValue, bool asServer)
        {
            if (containerAnimator != null)
            {
                if (newValue)
                {
                    containerAnimator.SetTrigger(openAnimTrigger);
                }
            }
        }

        [Server]
        public bool TransferItemToPlayer(NetworkConnection player, string itemId)
        {
            // Server validation
            if (!currentViewers.Contains(player)) return false;

            if (lootContainer.TryRemoveItem(itemId))
            {
                // TODO: Add to player inventory via InventoryManager

                // Sync to all viewers
                ObserversSyncContainer(lootContainer.ContainerItems.ToArray());
                return true;
            }

            return false;
        }

        [ObserversRpc]
        private void ObserversSyncContainer(ItemInstance[] items)
        {
            LootContainerUI.Instance?.UpdateItems(items);
        }
    }
}