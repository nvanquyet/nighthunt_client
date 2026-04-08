using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core
{
    /// <summary>
    /// Static utility for rebuilding slot-to-ItemInstance dictionaries from
    /// a SyncDictionary/IReadOnlyDictionary of (SlotKey → InstanceID) pairs.
    ///
    /// PROBLEM SOLVED:
    ///   EquipmentSystem and WeaponSystem both had near-identical cache-rebuild code:
    ///   _cache.Clear() → foreach kv in _syncDict → GetByID → populate cache
    ///   This violates DRY. Using this helper removes ~40 duplicate lines.
    ///
    /// USAGE in EquipmentSystem:
    ///   SlotCacheHelper.Rebuild(_equippedItems, _inventoryReader, _equipmentCache);
    ///
    /// USAGE in WeaponSystem:
    ///   SlotCacheHelper.Rebuild(_weapons, _inventoryReader, _weaponCache);
    ///
    /// TYPE SAFETY:
    ///   Works with any enum or struct TSlotType as dictionary key.
    ///   The InventoryReader is passed as a delegate to decouple from IInventorySystem.
    /// </summary>
    public static class SlotCacheHelper
    {
        /// <summary>
        /// Rebuild <paramref name="cache"/> from <paramref name="slotMap"/> by resolving each
        /// InstanceID to an <see cref="ItemInstance"/> via <paramref name="instanceResolver"/>.
        ///
        /// Entries whose InstanceID is null/empty or whose ItemInstance cannot be resolved are skipped.
        /// </summary>
        /// <typeparam name="TSlot">Slot key type (enum, struct).</typeparam>
        /// <param name="slotMap">Current slot → InstanceID mapping. Accepts any IEnumerable
        ///   (Dictionary, SyncDictionary, IReadOnlyDictionary, etc.).</param>
        /// <param name="instanceResolver">Function that returns an ItemInstance for a given InstanceID.</param>
        /// <param name="cache">Output cache to populate.</param>
        public static void Rebuild<TSlot>(
            IEnumerable<KeyValuePair<TSlot, string>> slotMap,
            System.Func<string, ItemInstance>        instanceResolver,
            Dictionary<TSlot, ItemInstance>          cache)
        {
            cache.Clear();
            if (slotMap == null || instanceResolver == null) return;

            foreach (var kv in slotMap)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                var inst = instanceResolver(kv.Value);
                if (inst != null)
                    cache[kv.Key] = inst;
            }
        }

        /// <summary>
        /// Overload: rebuild and return a newly allocated dictionary.
        /// Useful when the caller doesn't have an existing cache to reuse.
        /// </summary>
        public static Dictionary<TSlot, ItemInstance> RebuildNew<TSlot>(
            IEnumerable<KeyValuePair<TSlot, string>> slotMap,
            System.Func<string, ItemInstance>        instanceResolver,
            int capacityHint = 4)
        {
            var result = new Dictionary<TSlot, ItemInstance>(capacityHint);
            Rebuild(slotMap, instanceResolver, result);
            return result;
        }

        /// <summary>
        /// Try to get the item in a specific slot, resolving on cache-miss.
        /// Returns null if the slot is empty or the instance is not found.
        /// </summary>
        public static ItemInstance GetOrResolve<TSlot>(
            TSlot slot,
            IReadOnlyDictionary<TSlot, string> slotMap,
            System.Func<string, ItemInstance>  instanceResolver,
            Dictionary<TSlot, ItemInstance>    cache)
        {
            // Try cache first (O(1))
            if (cache.TryGetValue(slot, out var cached) && cached != null)
                return cached;

            // Cache miss: resolve from slotMap
            if (!slotMap.TryGetValue(slot, out var instId) || string.IsNullOrEmpty(instId))
                return null;

            var inst = instanceResolver(instId);
            if (inst != null) cache[slot] = inst;
            return inst;
        }
    }
}
