using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Loot;

namespace NightHunt.InteractionSystem.Interaction.Implementations
{
    /// <summary>
    /// Interactable player corpse (one-way loot - can only take, not put items in).
    /// </summary>
    [RequireComponent(typeof(PlayerCorpseLoot))]
    public class CorpseInteractable : InteractableBase
    {
        private PlayerCorpseLoot corpseLoot;
        private LootContainer lootContainer;

        protected override void Awake()
        {
            base.Awake();
            corpseLoot = GetComponent<PlayerCorpseLoot>();
            lootContainer = GetComponent<LootContainer>();

            // Set interaction type to Container
            interactionType = InteractionType.Container;
        }

        public override void Interact(GameObject interactor)
        {
            if (lootContainer == null)
                return;

            // Request to open container (one-way transfer only)
            var networkObject = interactor.GetComponent<FishNet.Object.NetworkObject>();
            if (networkObject != null && networkObject.IsOwner)
            {
                lootContainer.RequestOpenContainer(networkObject.Owner);
            }
        }

        public override string GetInteractionText()
        {
            return "Press E to loot corpse";
        }
    }
}
