using System;
using UnityEngine;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// Configuration for hold interaction system.
    /// </summary>
    [Serializable]
    public class HoldInteractionConfig
    {
        [Header("Hold Settings")]
        public float holdDuration = 2f;
        public float maxMoveDistance = 1f;
        
        [Header("Interrupt Events")]
        public bool interruptOnDamage = true;
        public bool interruptOnMoveTooFar = true;
        public bool interruptOnReleaseKey = true;
    }
}
