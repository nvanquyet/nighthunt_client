using System.Collections;
using System.Diagnostics;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Connection;
using NightHunt.Core;
using NightHunt.Services.Backend;
using TMPro;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// Ping Display - Tự động chuyển giữa backend ping và headless server ping
    /// - Backend ping: Khi chưa kết nối FishNet
    /// - Headless ping: Khi đã kết nối FishNet
    /// Sử dụng Singleton pattern để dễ access
    /// </summary>
    public class PingDisplay : MonoBehaviour
    {
        private static PingDisplay instance;
        
        public static PingDisplay Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to find in PersistentUICanvas first
                    if (PersistentUICanvas.Instance != null)
                    {
                        instance = PersistentUICanvas.Instance.PingDisplay;
                    }
                    
                    // If still null, find in scene
                    if (instance == null)
                    {
                        instance = FindFirstObjectByType<PingDisplay>();
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI pingText;
        [SerializeField] private float updateInterval = 1f; // Update every second

        [Header("Ping Settings")]
        [Tooltip("If true, show FishNet (gameplay) ping when client is connected to server.")]
        [SerializeField] private bool headlessPingEnabled = true;
        
        [Tooltip("If true, enable backend HTTP ping (used on menus / when not connected to game server).")]
        [SerializeField] private bool backendPingEnabled = false;

        private NetworkManager networkManager;
        private BackendHttpClient backendClient;
        private float lastUpdateTime;
        private int currentPing = 0;
        private bool isMeasuringBackendPing = false;
        private Coroutine backendPingCoroutine;

        private void Awake()
        {
            // Set instance
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                // Duplicate found, destroy this one
                Destroy(gameObject);
                return;
            }

            networkManager = InstanceFinder.NetworkManager;
            
            // Auto-assign references
            if (pingText == null)
            {
                pingText = GetComponent<TextMeshProUGUI>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            // Get backend client from GameManager
            if (GameManager.Instance != null)
            {
                backendClient = GameManager.Instance.BackendClient;
            }

            // Start ping measurement
            StartPingMeasurement();
        }

        private void OnEnable()
        {
            StartPingMeasurement();
        }

        private void OnDisable()
        {
            StopPingMeasurement();
        }

        private void StartPingMeasurement()
        {
            if (backendPingCoroutine != null)
            {
                StopCoroutine(backendPingCoroutine);
            }
            backendPingCoroutine = StartCoroutine(MeasurePingCoroutine());
        }

        private void StopPingMeasurement()
        {
            if (backendPingCoroutine != null)
            {
                StopCoroutine(backendPingCoroutine);
                backendPingCoroutine = null;
            }
        }

        private IEnumerator MeasurePingCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(updateInterval);

                // Check if FishNet is connected
                bool isFishNetConnected = networkManager != null && 
                                        networkManager.ClientManager != null &&
                                        networkManager.ClientManager.Started &&
                                        networkManager.ClientManager.Connection.IsValid;

                if (headlessPingEnabled && isFishNetConnected)
                {
                    // Measure headless server ping
                    currentPing = GetHeadlessServerPing();
                    UpdatePingDisplay("Headless", currentPing);
                }
                else if (backendPingEnabled && backendClient != null)
                {
                    // Measure backend ping
                    yield return StartCoroutine(MeasureBackendPing());
                }
                else
                {
                    // No connection, show "---"
                    UpdatePingDisplay("---", 0);
                }
            }
        }

        private int GetHeadlessServerPing()
        {
            try
            {
                // FishNet exposes RTT via TimeManager.
                // See FishNet built-in component: FishNet.Component.Utility.PingDisplay
                TimeManager tm = InstanceFinder.TimeManager;
                if (tm == null)
                    return 0;

                long ping = tm.RoundTripTime;
                // Match FishNet built-in behavior: subtract tick-rate latency to show "real ping"
                long deduction = (long)(tm.TickDelta * 2000d);
                ping = (long)Mathf.Max(1, ping - deduction);
                return (int)ping;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to get headless ping: {ex.Message}");
                return 0;
            }
        }

        private IEnumerator MeasureBackendPing()
        {
            if (backendClient == null || isMeasuringBackendPing) yield break;

            isMeasuringBackendPing = true;
            var stopwatch = Stopwatch.StartNew();

            // Send a lightweight request to measure ping
            // Using a health check or ping endpoint if available
            string pingEndpoint = "/health"; // Adjust based on your backend API
            
            string baseUrl = backendClient.GetBaseUrl();
            if (string.IsNullOrEmpty(baseUrl))
            {
                UpdatePingDisplay("Backend", 0);
                isMeasuringBackendPing = false;
                yield break;
            }

            UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(
                baseUrl + pingEndpoint
            );
            
            yield return request.SendWebRequest();

            stopwatch.Stop();
            int pingMs = (int)stopwatch.ElapsedMilliseconds;

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                currentPing = pingMs;
                UpdatePingDisplay("Backend", currentPing);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Backend ping measurement failed: {request.error}");
                UpdatePingDisplay("Backend", 0);
            }

            request.Dispose();
            isMeasuringBackendPing = false;
        }

        private void UpdatePingDisplay(string source, int pingMs)
        {
            if (pingText == null) return;

            if (pingMs > 0)
            {
                // Color code based on ping
                string color = pingMs < 50 ? "green" : pingMs < 100 ? "yellow" : "red";
                pingText.text = $"<color={color}>{pingMs} ms</color> ({source})";
            }
            else
            {
                pingText.text = "--- ms";
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (pingText == null)
            {
                pingText = GetComponent<TextMeshProUGUI>();
            }
        }
#endif
    }
}
