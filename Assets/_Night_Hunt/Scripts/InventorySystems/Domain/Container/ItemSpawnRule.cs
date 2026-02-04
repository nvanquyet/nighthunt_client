using System;
using NightHunt.Inventory.Core.Data;
using UnityEngine;

namespace NightHunt.Inventory.Domain.Container
{
    /// <summary>
    /// Defines a fixed item spawn rule.
    /// </summary>
    [Serializable]
    public class ItemSpawnRule
    {
        public ItemDefinition Item;
        public int Quantity = 1;

        [Range(0f, 100f)] [Tooltip("Durability percentage (0-100)")]
        public float DurabilityPercent = 100f;
    }
}