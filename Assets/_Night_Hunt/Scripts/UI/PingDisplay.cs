using System.Collections;
using System.Diagnostics;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Statistic;
using FishNet.Connection;
using NightHunt.Core;
using NightHunt.Services.Backend;
using TMPro;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Ping Display - Tự động chuyển giữa backend ping và headless server ping
    /// - Backend ping: Khi not yet connected FishNet
    /// - Headless ping: Khi connected FishNet
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
        [SerializeField] private bool showBackendPingWhenDisconnected = true;
        [SerializeField] private bool headlessPingEnabled = true; // Uses FishNet TimeManager.RoundTripTime

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
                pingText = ComponentResolver.Find<TextMeshProUGUI>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] TextMeshProUGUI not found")
        .Resolve();
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
                else if (showBackendPingWhenDisconnected || !headlessPingEnabled)
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
            if (networkManager == null) return 0;

            try
            {
                // FishNet provides ping through StatisticsManager
                StatisticsManager statsManager = networkManager.StatisticsManager;
                if (statsManager != null)
                {
                    // Get round trip time (RTT) from client connection
                    ClientManager clientManager = networkManager.ClientManager;
                    if (clientManager != null && clientManager.Connection.IsValid)
                    {
                        // FishNet provides RTT through connection statistics
                        // Get RTT from connection's ping
                        NetworkConnection connection = clientManager.Connection;
                        if (connection != null)
                        {
                            // FishNet stores RTT in connection, access via RoundTripTime property
                            // Note: This may need adjustment based on FishNet version
                            // For now, use a placeholder approach
                            return EstimateRTTFromConnection(connection);
                        }
                    }
                }

                // Fallback: Estimate from network time
                return EstimateRTTFromNetworkTime();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to get headless ping: {ex.Message}");
                return 0;
            }
        }

        private int EstimateRTTFromConnection(NetworkConnection connection)
        {
            // Use FishNet TimeManager RTT (seconds → milliseconds)
            double rtt = networkManager?.TimeManager?.RoundTripTime ?? 0.0;
            return Mathf.RoundToInt((float)rtt * 1000f);
        }

        private int EstimateRTTFromNetworkTime()
        {
            // Use FishNet TimeManager RTT (seconds → milliseconds)
            if (networkManager != null && networkManager.ClientManager != null && networkManager.ClientManager.Started)
            {
                double rtt = networkManager.TimeManager?.RoundTripTime ?? 0.0;
                return Mathf.RoundToInt((float)rtt * 1000f);
            }
            return 0;
        }

        private IEnumerator MeasureBackendPing()
        {
            if (backendClient == null || isMeasuringBackendPing) yield break;

            isMeasuringBackendPing = true;
            var stopwatch = Stopwatch.StartNew();

            // Send a lightweight request to measure ping
            // Using the actuator health endpoint from Spring Boot (with /api prefix)
            string pingEndpoint = "/api/actuator/health"; // GetApiBaseUrl() returns https://localhost:8443 (no /api)
            
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
                pingText = ComponentResolver.Find<TextMeshProUGUI>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] TextMeshProUGUI not found")
        .Resolve();
            }
        }
#endif

    }
}
