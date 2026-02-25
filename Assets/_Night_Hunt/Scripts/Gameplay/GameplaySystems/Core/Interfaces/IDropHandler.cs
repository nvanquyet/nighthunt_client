using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Contract for spawning items as world pickups.
    ///
    /// DESIGN (SRP / DIP):
    ///   - InventorySystem.DropItemServer depends on this interface, not on WorldDropManager.
    ///   - Swap implementation in tests or extend without touching InventorySystem.
    /// </summary>
    public interface IDropHandler
    {
        /// <summary>
        /// Spawn a single world-pickup prefab from existing <paramref name="data"/> at
        /// <paramref name="position"/> / <paramref name="rotation"/>.
        /// Server-side only.
        /// </summary>
        void SpawnPickup(ItemInstanceData data, Vector3 position, Quaternion rotation);

        /// <summary>
        /// Roll <paramref name="table"/> and scatter the results around <paramref name="centerPosition"/>.
        /// Server-side only.
        /// </summary>
        void SpawnPickupsFromTable(SpawnTable table, Vector3 centerPosition,
            float spreadRadius = 1.5f);
    }
}
