using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core.Abstractions
{
  
    /// <summary>
    /// Registry/Collection for resolving ItemDataBase by id at runtime (client + server).
    /// Place an instance under Resources/ with name: ItemDataRegistry
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDataRegistry", menuName = "NightHunt/InteractionSystem/ItemDataRegistry")]
    public class ItemDataRegistry : ScriptableObject
    {
        [SerializeField] private ItemDataBase[] items = new ItemDataBase[0];

        private Dictionary<string, ItemDataBase> _byId;

        public ItemDataBase GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"[ItemDataRegistry] GetById called with null/empty id");
                return null;
            }

            BuildCacheIfNeeded();
            
            Debug.Log($"[ItemDataRegistry] Looking up item with id: '{id}'");
            Debug.Log($"[ItemDataRegistry] Cache contains {(_byId?.Count ?? 0)} items");

            _byId.TryGetValue(id, out var itemData);
            
            if (itemData == null)
            {
                Debug.LogWarning($"[ItemDataRegistry] Item with id '{id}' not found in registry");
            }
            else
            {
                Debug.Log($"[ItemDataRegistry] Found item: '{id}' -> {itemData.DisplayName ?? itemData.name}");
            }
            
            return itemData;
        }

        private void BuildCacheIfNeeded()
        {
            if (_byId != null)
                return;

            Debug.Log($"[ItemDataRegistry] Building cache from {items?.Length ?? 0} items");
            _byId = new Dictionary<string, ItemDataBase>();
            if (items == null)
            {
                Debug.LogWarning("[ItemDataRegistry] Items array is null!");
                return;
            }

            int validCount = 0;
            int nullCount = 0;
            int emptyIdCount = 0;

            foreach (var item in items)
            {
                if (item == null)
                {
                    nullCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.ItemId))
                {
                    emptyIdCount++;
                    Debug.LogWarning($"[ItemDataRegistry] Item '{item.name}' has null/empty ItemId, skipping");
                    continue;
                }

                _byId[item.ItemId] = item;
                validCount++;
                Debug.Log($"[ItemDataRegistry] Cached item: '{item.ItemId}' -> {item.DisplayName ?? item.name}");
            }

            Debug.Log($"[ItemDataRegistry] Cache built: {validCount} valid, {nullCount} null, {emptyIdCount} empty IDs");
        }

        /// <summary>
        /// Get all items (for searching).
        /// </summary>
        public IEnumerable<ItemDataBase> GetAllItems()
        {
            BuildCacheIfNeeded();
            return _byId.Values;
        }

        public static ItemDataRegistry Load()
        {
            Debug.Log("[ItemDataRegistry] Attempting to load ItemDataRegistry from Resources/ItemDataRegistry");
            var registry = Resources.Load<ItemDataRegistry>("ItemDataRegistry");
            
            if (registry == null)
            {
                Debug.LogError("[ItemDataRegistry] Failed to load ItemDataRegistry from Resources/ItemDataRegistry.asset. Please ensure:");
                Debug.LogError("  1. Asset exists at path: Resources/ItemDataRegistry.asset");
                Debug.LogError("  2. Asset name matches exactly: 'ItemDataRegistry' (case-sensitive)");
                Debug.LogError("  3. Asset is a valid ItemDataRegistry ScriptableObject");
            }
            else
            {
                Debug.Log($"[ItemDataRegistry] Successfully loaded ItemDataRegistry asset");
                var itemCount = registry.items != null ? registry.items.Length : 0;
                Debug.Log($"[ItemDataRegistry] Registry contains {itemCount} items in array");
            }
            
            return registry;
        }
    }
}