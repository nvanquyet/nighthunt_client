using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core.Abstractions
{
    [CreateAssetMenu(
        fileName = "ItemDataRegistry",
        menuName = "NightHunt/InteractionSystem/ItemDataRegistry")]
    public class ItemDataRegistry : ScriptableObject
    {
        [SerializeField] private ItemDataBase[] items = new ItemDataBase[0];

        private Dictionary<string, ItemDataBase> _byId;
        private bool _cacheBuilt;

        public ItemDataBase GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("[ItemDataRegistry] GetById called with null/empty id");
                return null;
            }

            BuildCacheIfNeeded();

            if (_byId.TryGetValue(id, out var item))
                return item;

            Debug.LogWarning($"[ItemDataRegistry] Item not found for id: '{id}'");
            return null;
        }

        private void BuildCacheIfNeeded()
        {
            if (_cacheBuilt)
                return;

            _cacheBuilt = true;
            _byId = new Dictionary<string, ItemDataBase>();

            if (items == null || items.Length == 0)
            {
                Debug.LogWarning("[ItemDataRegistry] No items assigned in registry");
                return;
            }

            int valid = 0;
            int skipped = 0;

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                {
                    skipped++;
                    continue;
                }

                _byId[item.ItemId] = item;
                valid++;
            }

            Debug.Log(
                $"[ItemDataRegistry] Cache built. Valid: {valid}, Skipped: {skipped}"
            );
        }

        public IEnumerable<ItemDataBase> GetAllItems()
        {
            BuildCacheIfNeeded();
            return _byId.Values;
        }

        public static ItemDataRegistry Load()
        {
            var registry = Resources.Load<ItemDataRegistry>("ItemDataRegistry");

            if (registry == null)
            {
                Debug.LogError(
                    "[ItemDataRegistry] Missing Resources/ItemDataRegistry.asset"
                );
                return null;
            }

            return registry;
        }
    }
}
