using System.Collections.Generic;
using UnityEngine;
using NightHunt.Config;

namespace NightHunt.UI
{
    /// <summary>
    /// Generic manager for HUD diagnostic metrics.
    /// Allows enabling/disabling generic metric objects based on GameSettings.
    /// Easily extendable to support other indicators (e.g. RAM, Packet Loss) in the future.
    /// </summary>
    [DisallowMultipleComponent]
    public class PerformanceHUD : MonoBehaviour
    {
        [System.Serializable]
        public struct MetricUIEntry
        {
            [Tooltip("Name of the metric - matches the config toggle name (case-insensitive)")]
            public string metricName;
            
            [Tooltip("The root UI GameObject for this metric")]
            public GameObject targetUIObject;
        }

        private static PerformanceHUD instance;
        public static PerformanceHUD Instance => instance;

        [Header("Metric Configuration")]
        [SerializeField] private List<MetricUIEntry> metricEntries = new List<MetricUIEntry>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Register to settings change event so we update instantly when toggled
            GameSettings.OnSettingsChanged += ApplyHUDPreferences;
            ApplyHUDPreferences();
        }

        private void OnDestroy()
        {
            GameSettings.OnSettingsChanged -= ApplyHUDPreferences;
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Applies the current user preferences to each registered metric UI object.
        /// </summary>
        public void ApplyHUDPreferences()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            foreach (var entry in metricEntries)
            {
                if (entry.targetUIObject == null) continue;

                bool shouldBeActive = true;

                // Match metric name to check its corresponding setting
                if (entry.metricName.Equals("FPS", System.StringComparison.OrdinalIgnoreCase))
                {
                    shouldBeActive = settings.ShowFPS;
                }
                else if (entry.metricName.Equals("Ping", System.StringComparison.OrdinalIgnoreCase))
                {
                    shouldBeActive = settings.ShowPing;
                }
                // Future additions can easily be mapped here:
                // else if (entry.metricName.Equals("RAM", System.StringComparison.OrdinalIgnoreCase))
                // {
                //     shouldBeActive = settings.ShowRAM;
                // }

                if (entry.targetUIObject.activeSelf != shouldBeActive)
                {
                    entry.targetUIObject.SetActive(shouldBeActive);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("NightHunt/Populate Default Metric Entries")]
        private void Editor_PopulateDefaults()
        {
            metricEntries.Clear();

            // Try to find local children representing FPS and Ping
            var fpsChild = transform.Find("FPS") ?? transform.Find("FPSDisplay") ?? transform.Find("FPS Text");
            var pingChild = transform.Find("Ping") ?? transform.Find("PingDisplay") ?? transform.Find("Ping Text");

            metricEntries.Add(new MetricUIEntry 
            { 
                metricName = "FPS", 
                targetUIObject = fpsChild != null ? fpsChild.gameObject : null 
            });

            metricEntries.Add(new MetricUIEntry 
            { 
                metricName = "Ping", 
                targetUIObject = pingChild != null ? pingChild.gameObject : null 
            });

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PerformanceHUD] Default metric entries populated in editor.");
        }
#endif
    }
}
