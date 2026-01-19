using UnityEngine;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Character;

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
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float floatSpeed = 1f;
        [SerializeField] private float floatAmount = 0.5f;

        private ItemConfigData itemConfig;
        private Vector3 startPosition;
        private float floatOffset;

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

            // Load config
            itemConfig = GameConfigLoader.Instance?.GetItemConfig(itemId);
            if (itemConfig != null)
            {
                // Update display name, etc.
            }

            startPosition = transform.position;
            floatOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsSpawned || IsPickedUp) return;

            // Visual effects
            UpdateVisuals();

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

        private void UpdateVisuals()
        {
            // Rotate
            if (itemModel != null)
            {
                itemModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // Float up and down
            float yOffset = Mathf.Sin((Time.time + floatOffset) * floatSpeed) * floatAmount;
            transform.position = startPosition + Vector3.up * yOffset;
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
                var characterStats = collider.GetComponent<CharacterStats>();
                if (characterStats != null)
                {
                    // Try to add item to inventory
                    var inventory = collider.GetComponent<Inventory.InventorySystem>();
                    if (inventory != null)
                    {
                        if (inventory.AddItem(itemId, 1))
                        {
                            PickupItem(collider.gameObject);
                            break;
                        }
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

