using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Interaction.Implementations
{
    /// <summary>
    /// Interactable player corpse (one-way loot - can only take, not put items in).
    /// </summary>
    [RequireComponent(typeof(PlayerCorpseLoot))]
    public class CorpseInteractable : InteractableBase
    {
        private PlayerCorpseLoot corpseLoot;
        private NetworkLootContainer lootContainer;

        protected override void Awake()
        {
            base.Awake();
            // Use ComponentFinder to search in hierarchy (components might be in child)
            corpseLoot = ComponentFinder.FindComponentInHierarchy<PlayerCorpseLoot>(gameObject, includeInactive: false);
            lootContainer = ComponentFinder.FindComponentInHierarchy<NetworkLootContainer>(gameObject, includeInactive: false);

            // Set interaction type to Container
            interactionType = InteractionType.Container;
        }

        public override void Interact(GameObject interactor)
        {
            if (lootContainer == null)
                return;

            // Corpse container is always open - just fire event to show UI
            // No need to RequestOpenContainer since it's already opened
            var networkObject = interactor.GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                // Fire event to show UI (container is already opened)
                InventoryEvents.InvokeLootContainerOpened(lootContainer);
            }
        }

        public override string GetInteractionText()
        {
            return "Press E to loot corpse";
        }
    }
}
