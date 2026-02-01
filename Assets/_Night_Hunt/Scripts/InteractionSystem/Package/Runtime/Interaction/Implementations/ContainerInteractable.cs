using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Interaction.Implementations
{
    /// <summary>
    /// Interactable container (chest, crate, etc.) that opens loot UI.
    /// 
    /// RESPONSIBILITIES:
    /// - Implements IInteractable interface (REQUIRED for interaction system)
    /// - Provides Interact() method to open container when player interacts
    /// - Provides GetInteractionText() for prompt display (used by InteractionDetector)
    /// - Provides CanInteract() for validation
    /// 
    /// NOTE: This component does NOT detect interactions - that's InteractionDetector's job.
    /// InteractionDetector uses raycast to find this component and shows prompt.
    /// When player presses E, InteractionDetector calls this component's Interact() method.
    /// 
    /// Interaction type can be configured in prefab or set via SetInteractionType().
    /// </summary>
    [RequireComponent(typeof(NetworkLootContainer))]
    public class ContainerInteractable : InteractableBase
    {
        private NetworkLootContainer lootContainer;

        protected override void Awake()
        {
            base.Awake();
            // Use ComponentFinder to search in hierarchy (component might be in child)
            lootContainer = ComponentFinder.FindComponentInHierarchy<NetworkLootContainer>(gameObject, includeInactive: false);
            if (lootContainer == null)
            {
                Debug.LogError("[ContainerInteractable] NetworkLootContainer component not found in hierarchy!");
            }

            // Default to Container if not set in Inspector
            // Will be overridden by SetInteractionType() when spawned from LootContainerPoint
            if (interactionType == InteractionType.Immediate)
            {
                interactionType = InteractionType.Container;
            }
        }

        /// <summary>
        /// Set interaction type (called when spawned from LootContainerPoint).
        /// Uses config from point, falls back to prefab settings if not called.
        /// </summary>
        public void SetInteractionType(InteractionType type, float holdTime = 0f)
        {
            interactionType = type;
            requiredHoldTime = holdTime;
            
            // Update interaction text based on type
            if (type == InteractionType.Hold)
            {
                interactionText = $"Hold E to open";
            }
            else if (type == InteractionType.Container)
            {
                interactionText = $"Press E to open";
            }
            else
            {
                interactionText = $"Press E to interact";
            }
        }

        /// <summary>
        /// Get interaction type. If container is already opened, return Immediate (no need to hold).
        /// </summary>
        public override InteractionType GetInteractionType()
        {
            // If container is already opened, use Immediate interaction (just reopen inventory UI)
            if (lootContainer != null && lootContainer.IsOpened())
            {
                return InteractionType.Immediate;
            }
            
            // Otherwise, use configured interaction type (Hold or Immediate)
            return base.GetInteractionType();
        }

        public override void Interact(GameObject interactor)
        {
            Debug.Log($"[ContainerInteractable] ===== Interact called =====");
            Debug.Log($"[ContainerInteractable] interactor: {interactor?.name ?? "null"}");
            Debug.Log($"[ContainerInteractable] lootContainer: {lootContainer != null}, IsOpened: {lootContainer?.IsOpened() ?? false}");
            
            if (lootContainer == null)
            {
                Debug.LogError("[ContainerInteractable] Interact: lootContainer is null! Cannot open container.");
                return;
            }

            if (lootContainer.IsLocked())
            {
                Debug.LogWarning("[ContainerInteractable] Interact: Container is locked! Cannot open.");
                return;
            }

            // Check if container is already opened
            if (lootContainer.IsOpened())
            {
                Debug.Log($"[ContainerInteractable] Container is already opened, firing event to show UI instead of opening again");
                
                // Fire event for UI layer to handle (InventoryPanel will listen and show UI)
                // This avoids direct dependency on Gameplay layer
                InventoryEvents.InvokeLootContainerOpened(lootContainer);
                Debug.Log($"[ContainerInteractable] Fired InventoryEvents.InvokeLootContainerOpened event - UI layer will handle showing the container");
                return;
            }

            // Container is not opened, open it normally
            // Find NetworkObject in interactor or its hierarchy
            Debug.Log($"[ContainerInteractable] Container not opened, opening container...");
            Debug.Log($"[ContainerInteractable] Searching for NetworkObject in interactor hierarchy...");
            var networkObject = ComponentFinder.FindComponentInHierarchy<FishNet.Object.NetworkObject>(interactor, includeInactive: false);
            Debug.Log($"[ContainerInteractable] NetworkObject found: {networkObject != null}, IsOwner: {networkObject?.IsOwner ?? false}, Owner: {networkObject?.Owner?.ClientId ?? -1}");

            if (networkObject != null && networkObject.IsOwner)
            {
                Debug.Log($"[ContainerInteractable] Calling RequestOpenContainer for owner: {networkObject.Owner?.ClientId ?? -1}");
                lootContainer.RequestOpenContainer(networkObject.Owner);
                Debug.Log($"[ContainerInteractable] RequestOpenContainer called successfully");
            }
            else
            {
                Debug.LogWarning($"[ContainerInteractable] Cannot open container - NetworkObject is null or not owner! NetworkObject={networkObject != null}, IsOwner={networkObject?.IsOwner ?? false}");
            }
        }

        public override string GetInteractionText()
        {
            if (lootContainer != null && lootContainer.IsLocked())
            {
                return "Locked";
            }
            
            // If container is already opened, show different text
            if (lootContainer != null && lootContainer.IsOpened())
            {
                return "Press E to view";
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
