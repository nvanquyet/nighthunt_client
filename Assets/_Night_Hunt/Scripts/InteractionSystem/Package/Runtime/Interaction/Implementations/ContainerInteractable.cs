using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Loot;

namespace NightHunt.InteractionSystem.Interaction.Implementations
{
    /// <summary>
    /// Interactable container (chest, crate, etc.) that opens loot UI.
    /// </summary>
    [RequireComponent(typeof(LootContainer))]
    public class ContainerInteractable : InteractableBase
    {
        private LootContainer lootContainer;

        protected override void Awake()
        {
            base.Awake();
            lootContainer = GetComponent<LootContainer>();
            if (lootContainer == null)
            {
                Debug.LogError("[ContainerInteractable] LootContainer component not found!");
            }

            // Set interaction type to Container
            interactionType = InteractionType.Container;
        }

        public override void Interact(GameObject interactor)
        {
            if (lootContainer == null)
                return;

            if (lootContainer.IsLocked())
            {
                Debug.LogWarning("[ContainerInteractable] Container is locked!");
                return;
            }

            // Request to open container
            var networkObject = interactor.GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                lootContainer.RequestOpenContainer(networkObject.Owner);
            }
        }

        public override string GetInteractionText()
        {
            if (lootContainer != null && lootContainer.IsLocked())
            {
                return "Locked";
            }
            return base.GetInteractionText();
        }

        public override bool CanInteract(GameObject interactor)
        {
            if (!base.CanInteract(interactor))
                return false;

            if (lootContainer == null)
                return false;

            return !lootContainer.IsLocked();
        }
    }
}
