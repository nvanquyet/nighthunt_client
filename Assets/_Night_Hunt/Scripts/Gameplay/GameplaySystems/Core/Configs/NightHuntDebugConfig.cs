using UnityEngine;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Development-only debug flags. Assign a single SO asset per project;
    /// leave the field null on the component to disable all debug output.
    /// Create via: Assets → Create → NightHunt/Debug/Debug Config
    /// </summary>
    [CreateAssetMenu(fileName = "NightHuntDebugConfig", menuName = "NightHunt/Debug/Debug Config")]
    public class NightHuntDebugConfig : ScriptableObject
    {
        [Header("System Logging")]
        [Tooltip("Enable debug logs for the stat system.")]
        public bool EnableStatDebugLogs = false;

        [Tooltip("Enable debug logs for the inventory, loot, and drag-drop systems.")]
        public bool EnableInventoryDebugLogs = false;

        [Tooltip("Enable debug logs for the interaction and pickup system.")]
        public bool EnableInteractionDebugLogs = false;

        [Tooltip("Enable debug logs for the weapon, equipment, and attachment systems.")]
        public bool EnableWeaponDebugLogs = false;

        [Tooltip("Enable debug logs for core game bootstrap, GameManager, and SessionState.")]
        public bool EnableCoreDebugLogs = false;

        [Tooltip("Enable debug logs for networking, server manager, and WebSocket service.")]
        public bool EnableNetworkDebugLogs = false;

        [Tooltip("Enable debug logs for match phases and team assignment.")]
        public bool EnableMatchDebugLogs = false;

        [Header("Visual Debug")]
        [Tooltip("Show gizmos in Scene view.")]
        public bool ShowDebugGizmos = false;
    }
}
