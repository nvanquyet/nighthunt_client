using UnityEngine;

namespace NightHunt.Config
{
    /// <summary>
    /// Configuration for multi-instance support (ParrelSync)
    /// In Editor: Enable to support multiple instances for testing
    /// In Build: Disable to use standard PlayerPrefs keys
    /// </summary>
    [CreateAssetMenu(fileName = "InstanceConfig", menuName = "NightHunt/Config/Instance Config")]
    public class InstanceConfig : ScriptableObject
    {
        [Header("Multi-Instance Support")]
        [Tooltip("Enable instance-specific PlayerPrefs keys (for ParrelSync testing). Disable in production builds.")]
        [SerializeField] private bool enableMultiInstanceSupport = true;
         
        [Tooltip("Auto-detect: Enable in Editor, Disable in Build")]
        [SerializeField] private bool autoDetect = true;

        [Header("Focus Behavior")]
        [Tooltip("Run in background when losing focus (Editor only). In Build, app will pause when losing focus.")]
        [SerializeField] private bool runInBackground = false;
        
        [Tooltip("Auto-detect: Disable in Editor (for testing), Enable in Build (normal behavior)")]
        [SerializeField] private bool autoDetectFocusBehavior = true;
        
        [Tooltip("Refresh data when regaining focus (recommended: true)")]
        [SerializeField] private bool refreshOnFocusReturn = true;

        /// <summary>
        /// Check if multi-instance support should be enabled
        /// </summary>
        public bool IsMultiInstanceEnabled()
        {
            if (autoDetect)
            {
                // Auto-enable in Editor, disable in Build
                return Application.isEditor;
            }
            return enableMultiInstanceSupport;
        }

        /// <summary>
        /// Get instance ID (0 if multi-instance is disabled)
        /// </summary>
        public int GetInstanceId()
        {
            if (!IsMultiInstanceEnabled())
            {
                return 0; // Always use main instance keys in production
            }
            return Utils.InstanceHelper.GetInstanceId();
        }

        /// <summary>
        /// Get instance-specific key (returns baseKey if multi-instance is disabled)
        /// </summary>
        public string GetInstanceKey(string baseKey)
        {
            if (!IsMultiInstanceEnabled())
            {
                return baseKey; // Use standard keys in production
            }
            return Utils.InstanceHelper.GetInstanceKey(baseKey);
        }

        /// <summary>
        /// Check if app should run in background when losing focus
        /// </summary>
        public bool ShouldRunInBackground()
        {
            if (autoDetectFocusBehavior)
            {
                // In Editor: Run in background (for testing with multiple instances)
                // In Build: Don't run in background (normal mobile/desktop behavior)
                return Application.isEditor;
            }
            return runInBackground;
        }

        /// <summary>
        /// Check if should refresh data when regaining focus
        /// </summary>
        public bool ShouldRefreshOnFocusReturn()
        {
            return refreshOnFocusReturn;
        }
    }
}

