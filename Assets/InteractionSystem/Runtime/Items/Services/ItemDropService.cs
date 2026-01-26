using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public class ItemDropService : NetworkBehaviour
    {
        public static ItemDropService Instance { get; private set; }

        [Header("Drop Settings")] [SerializeField]
        private GameObject lootItemPrefab;

        [SerializeField] private float dropForce = 5f;
        [SerializeField] private float dropRadius = 1f;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        [Server]
        public void DropItem(ItemInstance item, Vector3 position, Vector3 direction)
        {
            // Spawn loot object
            GameObject lootObj = Instantiate(lootItemPrefab, position, Quaternion.identity);
            ServerManager.Spawn(lootObj);

            NetworkLootItem lootItem = lootObj.GetComponent<NetworkLootItem>();
            lootItem.Initialize(item.itemDataId, item.quantity);

            // Apply physics
            Rigidbody rb = lootObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(direction * dropForce, ForceMode.Impulse);
            }
        }

        [Server]
        public void DropItemsInRadius(ItemInstance[] items, Vector3 center)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * dropRadius;
                randomOffset.y = Mathf.Abs(randomOffset.y);

                Vector3 position = center + randomOffset;
                Vector3 direction = randomOffset.normalized;

                DropItem(items[i], position, direction);
            }
        }

        [Server]
        public GameObject SpawnCorpse(Vector3 position)
        {
            // Spawn corpse prefab
            // Will be implemented in PlayerCorpseLoot integration
            return null;
        }
    }
}