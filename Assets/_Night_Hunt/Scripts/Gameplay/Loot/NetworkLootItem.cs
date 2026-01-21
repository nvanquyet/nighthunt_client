using UnityEngine;
using FishNet.Object;
using NightHunt.Data;

namespace NightHunt.Gameplay.Loot
{
    /// <summary>
    /// Network loot item component
    /// Handles loot items that can be picked up by players
    /// Supports both test items (set in Inspector) and runtime-spawned items
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NetworkLootItem : NetworkBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private string itemId; // Field Inspector để test với 1 item cụ thể
        [SerializeField] private int quantity = 1;
        [SerializeField] private bool isTestItem = true; // Flag: nếu true, dùng itemId từ Inspector; nếu false, server sẽ set runtime

        [Header("Visual")]
        [SerializeField] private GameObject itemModel;
        [SerializeField] private ParticleSystem pickupEffect;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float floatSpeed = 1f;
        [SerializeField] private float floatAmount = 0.5f;

        private Vector3 startPosition;
        private float floatOffset;
        private bool isLooted = false;

        public string ItemId => itemId;
        public int Quantity => quantity;
        public bool IsTestItem => isTestItem;
        public bool IsLooted => isLooted;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Set layer and tag for raycast detection
            gameObject.layer = LayerMask.NameToLayer("InteractableLoot");
            if (gameObject.tag != "Loot")
            {
                gameObject.tag = "Loot";
            }

            startPosition = transform.position;
            floatOffset = Random.Range(0f, Mathf.PI * 2f);

            // If not test item, server should have set itemId already
            if (!isTestItem && string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning($"[NetworkLootItem] Non-test item has no itemId set! ObjectId: {ObjectId}");
            }
        }

        private void Update()
        {
            if (!IsSpawned || isLooted) return;

            UpdateVisuals();
        }

        /// <summary>
        /// Server: Initialize loot item with itemId and quantity
        /// Called when spawning loot from config
        /// </summary>
        [Server]
        public void Initialize(string newItemId, int newQuantity)
        {
            if (isTestItem)
            {
                Debug.LogWarning($"[NetworkLootItem] Initialize called on test item. Ignoring. Use Inspector to set itemId.");
                return;
            }

            itemId = newItemId;
            quantity = newQuantity;
            isLooted = false;

            // Validate item exists in config
            var config = GameConfigLoader.Instance?.GetItemConfig(newItemId);
            if (config == null)
            {
                Debug.LogWarning($"[NetworkLootItem] Item config not found: {newItemId}");
            }
        }

        /// <summary>
        /// Server: Mark as looted
        /// </summary>
        [Server]
        public void MarkAsLooted()
        {
            if (isLooted) return;

            isLooted = true;
            RpcPlayPickupEffect();

            // Despawn after delay
            Invoke(nameof(DespawnItem), 0.5f);
        }

        /// <summary>
        /// Server: Update quantity (for partial pickup)
        /// </summary>
        [Server]
        public void UpdateQuantity(int newQuantity)
        {
            if (newQuantity <= 0)
            {
                MarkAsLooted();
            }
            else
            {
                quantity = newQuantity;
            }
        }

        /// <summary>
        /// Update visual effects (rotation, floating)
        /// </summary>
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
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}

