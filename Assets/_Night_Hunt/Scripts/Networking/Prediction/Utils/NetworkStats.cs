using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Utils
{
    /// <summary>
    /// Network statistics cho prediction system.
    /// Track RTT, packet loss, reconciliation frequency, v.v.
    /// </summary>
    public class NetworkStats : MonoBehaviour
    {
        private static NetworkStats _instance;
        public static NetworkStats Instance => _instance;

        [Header("Stats Settings")]
        [SerializeField] private int maxSamples = 100;
        [SerializeField] private float updateInterval = 1f;

        private readonly Queue<float> _rttSamples = new Queue<float>();
        private readonly Queue<int> _packetLossSamples = new Queue<int>();
        private readonly Queue<int> _reconciliationSamples = new Queue<int>();

        private float _lastUpdateTime;
        private int _totalPacketsSent;
        private int _totalPacketsReceived;
        private int _totalReconciliations;

        /// <summary>
        /// Round-trip time trung bình (milliseconds).
        /// </summary>
        public float AverageRTT { get; private set; }

        /// <summary>
        /// Packet loss rate (0-1).
        /// </summary>
        public float PacketLossRate { get; private set; }

        /// <summary>
        /// Reconciliation frequency (reconciliations per second).
        /// </summary>
        public float ReconciliationFrequency { get; private set; }

        /// <summary>
        /// RTT hiện tại (milliseconds).
        /// </summary>
        public float CurrentRTT { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Time.time - _lastUpdateTime >= updateInterval)
            {
                UpdateStats();
                _lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Update statistics.
        /// </summary>
        private void UpdateStats()
        {
            // Calculate average RTT
            if (_rttSamples.Count > 0)
            {
                float sum = 0f;
                foreach (var rtt in _rttSamples)
                {
                    sum += rtt;
                }
                AverageRTT = sum / _rttSamples.Count;
            }

            // Calculate packet loss rate
            if (_totalPacketsSent > 0)
            {
                int lost = _totalPacketsSent - _totalPacketsReceived;
                PacketLossRate = (float)lost / _totalPacketsSent;
            }

            // Calculate reconciliation frequency
            ReconciliationFrequency = _totalReconciliations / updateInterval;
            _totalReconciliations = 0;
        }

        /// <summary>
        /// Record RTT sample.
        /// </summary>
        /// <param name="rtt">RTT in milliseconds</param>
        public void RecordRTT(float rtt)
        {
            CurrentRTT = rtt;
            _rttSamples.Enqueue(rtt);

            if (_rttSamples.Count > maxSamples)
            {
                _rttSamples.Dequeue();
            }
        }

        /// <summary>
        /// Record packet sent.
        /// </summary>
        public void RecordPacketSent()
        {
            _totalPacketsSent++;
        }

        /// <summary>
        /// Record packet received.
        /// </summary>
        public void RecordPacketReceived()
        {
            _totalPacketsReceived++;
        }

        /// <summary>
        /// Record reconciliation event.
        /// </summary>
        public void RecordReconciliation()
        {
            _totalReconciliations++;
        }

        /// <summary>
        /// Reset all statistics.
        /// </summary>
        public void Reset()
        {
            _rttSamples.Clear();
            _packetLossSamples.Clear();
            _reconciliationSamples.Clear();
            _totalPacketsSent = 0;
            _totalPacketsReceived = 0;
            _totalReconciliations = 0;
            AverageRTT = 0f;
            PacketLossRate = 0f;
            ReconciliationFrequency = 0f;
            CurrentRTT = 0f;
        }

        /// <summary>
        /// Get statistics as formatted string.
        /// </summary>
        /// <returns>Formatted stats string</returns>
        public string GetStatsString()
        {
            return $"RTT: {AverageRTT:F1}ms | Loss: {PacketLossRate * 100f:F1}% | Recon: {ReconciliationFrequency:F1}/s";
        }
    }
}

