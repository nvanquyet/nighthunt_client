using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Serializable data structure for network sync and persistence.
    /// </summary>
    [Serializable]
    public struct ItemInstanceData
    {
        public string InstanceId;
        public string ItemId;
        public int StackSize;
        public float CurrentDurability;
        public int CurrentAmmo;
        public string[] AttachedItemIds;
    }
}
