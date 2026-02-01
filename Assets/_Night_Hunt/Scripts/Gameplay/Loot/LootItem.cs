using UnityEngine;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.Loot
{
    /// <summary>
    /// Loot item that can be picked up by players
    /// </summary>
    public class LootItem : NetworkBehaviour
    {
        [Header("Item Settings")]
        [SerializeField] private string itemId;
        [SerializeField] private string rarity = "Common";
        [SerializeField] private float pickupRadius = 2f;
        [SerializeField] private bool autoPickup = false;

        [Header("Visual")]
        [SerializeField] private GameObject itemModel;
        [SerializeField] private ParticleSystem pickupEffect;
        
        [Header("VFX (for future implementation)")]
        [SerializeField] private GameObject highlightVFX;
        [SerializeField] private GameObject targetVFX;

        private ItemConfigData itemConfig;

        public string ItemId => itemId;
        public string Rarity => rarity;
        public bool IsPickedUp { get; private set; }
        public bool IsLooted => IsPickedUp;

        /// <summary>
        /// Set looted state (called by LootSync)
        /// </summary>
        public void SetLooted(bool looted)
        {
            IsPickedUp = looted;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // TODO: Load item data from ItemDataRegistry instead of GameConfigLoader
            // For now, item config is not loaded
            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            if (registry != null)
            {
                var itemData = registry.GetById(itemId);
                if (itemData != null)
                {
                    // Use itemData directly - no need for itemConfig conversion
                }
            }
        }

        private void Update()
        {
            if (!IsSpawned || IsPickedUp) return;

            // Check for pickup
            if (IsServer)
            {
                CheckForPickup();
            }
        }

        /// <summary>
        /// Initialize loot item
        /// </summary>
        public void Initialize(string id, string itemRarity)
        {
            itemId = id;
            rarity = itemRarity;
            IsPickedUp = false;
        }

        /// <summary>
        /// Show VFX effects (highlight, target indicator, etc.)
        /// Can be called when player targets this item or for other visual feedback.
        /// </summary>
        public void ShowVFX(bool showHighlight = true, bool showTarget = false)
        {
            if (highlightVFX != null)
            {
                highlightVFX.SetActive(showHighlight);
            }

            if (targetVFX != null)
            {
                targetVFX.SetActive(showTarget);
            }
        }

        /// <summary>
        /// Hide all VFX effects
        /// </summary>
        public void HideVFX()
        {
            ShowVFX(false, false);
        }

        /// <summary>
        /// Server: Check if player is in pickup range
        /// </summary>
        [Server]
        private void CheckForPickup()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRadius);
            
            foreach (var collider in colliders)
            {
                // Use ComponentFinder to search in hierarchy (including children)
                var characterStats = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(collider.gameObject, includeInactive: false);
                if (characterStats != null)
                {
                    // Try to add item to inventory - use ComponentFinder to search in hierarchy
                    var inventory = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<InventoryService>(collider.gameObject, includeInactive: false);
                    if (inventory != null)
                    {
                        // TODO: Integrate with package-based pickup flow instead of direct add
                    }
                }
            }
        }

        /// <summary>
        /// Server: Pick up item
        /// </summary>
        [Server]
        private void PickupItem(GameObject player)
        {
            IsPickedUp = true;
            
            // Notify listeners
            OnLooted?.Invoke();

            // Visual effect
            RpcPlayPickupEffect();

            // Despawn after delay
            Invoke(nameof(DespawnItem), 0.5f);
        }

        /// <summary>
        /// Event fired when loot is picked up
        /// </summary>
        public System.Action OnLooted;

        /// <summary>
        /// Server: Despawn item
        /// </summary>
        [Server]
        private void DespawnItem()
        {
            if (IsSpawned)
            {
                Despawn();
            }
        }

        /// <summary>
        /// Client: Play pickup effect
        /// </summary>
        [ObserversRpc]
        private void RpcPlayPickupEffect()
        {
            if (pickupEffect != null)
            {
                pickupEffect.Play();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}

