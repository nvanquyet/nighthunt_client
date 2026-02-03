using System;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Requirement for unlocking an item.
    /// </summary>
    [Serializable]
    public class ItemRequirement
    {
        public RequirementType Type;
        public string RequirementId;
        public int RequiredAmount;
    }
}
