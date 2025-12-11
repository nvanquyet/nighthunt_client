using System.Collections;
using NightHunt.Core;
using NightHunt.Services.Backend;
using NightHunt.State;
using UnityEngine;

namespace NightHunt.Services.Auth
{
    /// <summary>
    /// SessionMonitor - Monitors session status and detects force logout
    /// Polls backend periodically to check if session is still valid
    /// </summary>
    public class SessionMonitor : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float checkInterval = 5f; // Check every 5 seconds
        [SerializeField] private bool enablePolling = true;

        private IBackendClient backendClient;
        private Coroutine pollingCoroutine;
        private bool isPolling = false;

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                backendClient = GameManager.Instance.BackendClient;
            }
        }

        private void Start()
        {
            // Start polling if user is already authenticated
            if (enablePolling && SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                StartPolling();
            }
        }

        private void OnEnable()
        {
            if (enablePolling && SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                StartPolling();
            }
        }

        private void OnDisable()
        {
            StopPolling();
        }

        /// <summary>
        /// Start polling for session status
        /// </summary>
        public void StartPolling()
        {
            if (isPolling)
            {
                Debug.Log("[SessionMonitor] Already polling, skipping StartPolling");
                return;
            }

            Debug.Log($"[SessionMonitor] Starting polling with interval {checkInterval}s");
            isPolling = true;
            if (pollingCoroutine != null)
            {
                StopCoroutine(pollingCoroutine);
            }
            pollingCoroutine = StartCoroutine(PollSessionStatus());
        }

        /// <summary>
        /// Stop polling for session status
        /// </summary>
        public void StopPolling()
        {
            isPolling = false;
            if (pollingCoroutine != null)
            {
                StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }
        }

        private IEnumerator PollSessionStatus()
        {
            Debug.Log("[SessionMonitor] PollSessionStatus coroutine started");
            while (isPolling)
            {
                yield return new WaitForSeconds(checkInterval);

                // Only poll if user is authenticated
                if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
                {
                    // Make a lightweight request to check session status
                    // Using a simple endpoint that requires authentication
                    // If session is invalid, BackendHttpClient will handle force logout
                    CheckSessionStatus();
                }
                else
                {
                    Debug.LogWarning($"[SessionMonitor] Skipping poll - SessionState: {SessionState.Instance != null}, IsAuthenticated: {SessionState.Instance?.IsAuthenticated}");
                }
            }
            Debug.Log("[SessionMonitor] PollSessionStatus coroutine stopped");
        }

        private async void CheckSessionStatus()
        {
            // Try to get backendClient if not available
            if (backendClient == null)
            {
                if (GameManager.Instance != null)
                {
                    backendClient = GameManager.Instance.BackendClient;
                }
                if (backendClient == null)
                {
                    Debug.LogError("[SessionMonitor] Cannot check session - backendClient is null and GameManager.Instance is null");
                    return;
                }
            }
            
            if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
            {
                Debug.LogWarning($"[SessionMonitor] Cannot check session - SessionState: {SessionState.Instance != null}, IsAuthenticated: {SessionState.Instance?.IsAuthenticated}");
                return;
            }

            Debug.Log($"[SessionMonitor] Checking session status for user {SessionState.Instance.UserId} (sessionId: {SessionState.Instance.SessionId})");

            // Use a lightweight endpoint to check session validity
            // Any authenticated endpoint will work - the JWT filter will check force logout
            try
            {
                // Use a simple GET request to check session
                // BackendHttpClient will automatically handle AUTH_FORCE_LOGOUT errors
                var result = await backendClient.GetAsync<object>("/auth/check-session");
                
                if (result.Success)
                {
                    Debug.Log($"[SessionMonitor] Session check successful for user {SessionState.Instance.UserId}");
                }
                else
                {
                    // If we get here and result is not success, BackendHttpClient already handled the error
                    // The force logout popup will be shown by BackendHttpClient.HandleAuthError
                    Debug.LogWarning($"[SessionMonitor] Session check failed for user {SessionState.Instance.UserId}: {result.Message} (ErrorCode: {result.ErrorCode})");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SessionMonitor] Session check error for user {SessionState.Instance.UserId}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Check if session monitoring is currently active
        /// </summary>
        public bool IsPolling => isPolling;

        public void SetCheckInterval(float interval)
        {
            checkInterval = interval;
        }

        public void SetEnabled(bool enabled)
        {
            enablePolling = enabled;
            if (enabled)
            {
                StartPolling();
            }
            else
            {
                StopPolling();
            }
        }
    }
}

