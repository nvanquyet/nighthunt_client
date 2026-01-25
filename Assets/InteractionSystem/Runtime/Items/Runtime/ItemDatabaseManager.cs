using System.Collections.Generic;
using System.Linq;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public class ItemDatabaseManager : MonoBehaviour
    {
        public static ItemDatabaseManager Instance { get; private set; }

        [Header("Database")] [SerializeField] private ItemDataBase[] allItems;

        private Dictionary<string, ItemDataBase> itemDatabase;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            itemDatabase = new Dictionary<string, ItemDataBase>();

            // Load all items from Resources or assigned array
            if (allItems == null || allItems.Length == 0)
            {
                allItems = Resources.LoadAll<ItemDataBase>("Items");
            }

            foreach (var item in allItems)
            {
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                {
                    if (itemDatabase.ContainsKey(item.itemId))
                    {
                        Debug.LogError($"Duplicate item ID: {item.itemId}");
                        continue;
                    }

                    itemDatabase[item.itemId] = item;
                }
            }

            Debug.Log($"Loaded {itemDatabase.Count} items into database");
        }

        public ItemDataBase GetItemData(string itemId)
        {
            if (itemDatabase.TryGetValue(itemId, out ItemDataBase data))
            {
                return data;
            }

            Debug.LogError($"Item not found: {itemId}");
            return null;
        }

        public T GetItemData<T>(string itemId) where T : ItemDataBase
        {
            ItemDataBase data = GetItemData(itemId);
            return data as T;
        }

        public ItemDataBase[] GetItemsByCategory(ItemCategory category)
        {
            return itemDatabase.Values.Where(i => i.category == category).ToArray();
        }

        public ItemDataBase[] GetItemsByRarity(ItemRarity rarity)
        {
            return itemDatabase.Values.Where(i => i.rarity == rarity).ToArray();
        }

        public bool ItemExists(string itemId)
        {
            return itemDatabase.ContainsKey(itemId);
        }
    }
}