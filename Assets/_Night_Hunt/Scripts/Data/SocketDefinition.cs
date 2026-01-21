using UnityEngine;
using System;

namespace NightHunt.Data
{
    /// <summary>
    /// Socket definition for nested equipment
    /// Defines where attachments can be mounted on items
    /// </summary>
    [Serializable]
    public class SocketDefinition
    {
        public string SocketId; // Unique ID for this socket (e.g., "ScopeSlot", "BarrelSlot")
        public string SocketType; // Type of socket (e.g., "Scope", "Suppressor")
        public string[] AllowedCategories; // Categories that can be attached (e.g., ["Scope", "Sight"])
        public string MountPointName; // Name of Transform in prefab (e.g., "ScopeMount")
        
        // Runtime reference (set when item is equipped)
        [NonSerialized]
        public Transform MountPoint;
    }
}

