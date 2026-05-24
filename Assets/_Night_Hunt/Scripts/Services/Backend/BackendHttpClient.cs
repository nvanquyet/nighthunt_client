using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Data;
using NightHunt.State;
using NightHunt.Core;
using NightHunt.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace NightHunt.Services.Backend
{
    public class BackendHttpClient : MonoBehaviour, IBackendClient
    {
        // SEC-4: Use atomic int to prevent double-trigger of force logout coroutine
        private int _forceLogoutFlag = 0;
        private const string SessionHeader = "X-Session-Id";
        private const int TraceBodyMaxLen = 1200;

        private void Awake()
        {
        }

        // ARCH-1: Token single source of truth is SessionState.AccessToken
        // These methods are kept for interface compatibility but are no-ops
        public void SetAuthToken(string token)
        {
        }

        public void ClearAuthToken()
        {
        }

        public string GetBaseUrl() => BackendConfig.GetApiBaseUrl();

        public async Task<ApiResult<T>> GetAsync<T>(string endpoint)
        {
            return await SendRequestAsync<T>(UnityWebRequest.kHttpVerbGET, endpoint, null);
        }

        public async Task<ApiResult<T>> PostAsync<T>(string endpoint, object data = null)
        {
            return await SendRequestAsync<T>(UnityWebRequest.kHttpVerbPOST, endpoint, data);
        }

        public async Task<ApiResult<T>> PutAsync<T>(string endpoint, object data = null)
        {
            return await SendRequestAsync<T>(UnityWebRequest.kHttpVerbPUT, endpoint, data);
        }

        public async Task<ApiResult<T>> DeleteAsync<T>(string endpoint)
        {
            return await SendRequestAsync<T>(UnityWebRequest.kHttpVerbDELETE, endpoint, null);
        }

        private async Task<ApiResult<T>> SendRequestAsync<T>(string method, string endpoint, object data)
        {
            string baseUrl = BackendConfig.GetApiBaseUrl();
            string url = baseUrl + endpoint;
            bool traceEndpoint = ShouldTraceEndpoint(endpoint);

            if (traceEndpoint)
            {
                Debug.Log($"[FLOW][HTTP] Request => {method} {endpoint} (userId={SessionState.Instance?.UserId ?? 0}, auth={(SessionState.Instance != null && SessionState.Instance.IsAuthenticated)})");
            }

            int maxRetries = 3;
            int attempt = 0;
            string lastError = null;
            string lastErrorCode = null;

            while (attempt < maxRetries)
            {
                attempt++;
                UnityWebRequest request = null;

                if (method == UnityWebRequest.kHttpVerbGET || method == UnityWebRequest.kHttpVerbDELETE)
                {
                    request = UnityWebRequest.Get(url);
                    if (method == UnityWebRequest.kHttpVerbDELETE)
                    {
                        request.method = UnityWebRequest.kHttpVerbDELETE;
                    }
                }
                else
                {
                    string jsonData = data != null ? JsonUtility.ToJson(data) : "{}";
                    if (attempt == 1 && traceEndpoint)
                    {
                        Debug.Log($"[FLOW][HTTP] Payload {endpoint}: {TruncateForLog(RedactForLog(jsonData), TraceBodyMaxLen)}");
                    }
                    request = new UnityWebRequest(url, method)
                    {
                        uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData)),
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                // ARCH-1: Read token directly from SessionState (single source of truth)
                string token = SessionState.Instance?.AccessToken;
                if (!string.IsNullOrEmpty(token))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {token}");
                }

                // Attach sessionId for server-side session validation
                if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated &&
                    !string.IsNullOrEmpty(SessionState.Instance.SessionId))
                {
                    request.SetRequestHeader(SessionHeader, SessionState.Instance.SessionId);
                }

                request.timeout = BackendConfig.RequestTimeoutSeconds;

                // Attach AcceptAllCertificatesHandler if using self-signed certs
                if (BackendConfig.ShouldBypassSslCertificateValidation())
                {
                    request.certificateHandler = new NightHunt.Config.AcceptAllCertificatesHandler();
                }

                if (traceEndpoint && attempt > 1)
                {
                    Debug.Log($"[FLOW][HTTP] Sending {method} {endpoint} (Attempt {attempt}/{maxRetries})");
                }

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                bool isSuccess = request.result == UnityWebRequest.Result.Success;
                long responseCode = request.responseCode;
                string errorMsg = request.error;
                string responseText = "";

                if (request.downloadHandler != null)
                {
                    try
                    {
                        responseText = request.downloadHandler.text;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BackendHttpClient] Failed to read response text: {ex.Message}");
                    }
                }

                if (traceEndpoint)
                {
                    Debug.Log($"[FLOW][HTTP] Response <= {method} {endpoint} status={responseCode} result={request.result} (Attempt {attempt})");
                }

                // Determine if this is a transient connection error
                bool isTransientError = request.result == UnityWebRequest.Result.ConnectionError ||
                                       (request.result == UnityWebRequest.Result.ProtocolError && (responseCode == 502 || responseCode == 503 || responseCode == 504)) ||
                                       (!string.IsNullOrEmpty(errorMsg) && (errorMsg.Contains("Curl error 55") || errorMsg.Contains("Curl error 56") || errorMsg.Contains("reset") || errorMsg.Contains("timeout")));

                if (!isSuccess && isTransientError && attempt < maxRetries)
                {
                    Debug.LogWarning($"[BackendHttpClient] Transient error on {method} {endpoint}: {errorMsg} (Code: {responseCode}). Retrying in {attempt * 500}ms... (Attempt {attempt}/{maxRetries})");
                    lastError = errorMsg;
                    request.Dispose();
                    await Task.Delay(attempt * 500);
                    continue;
                }

                try
                {
                    if (isSuccess)
                    {
                        if (traceEndpoint)
                        {
                            Debug.Log($"[FLOW][HTTP] Body {endpoint}: {TruncateForLog(RedactForLog(responseText), TraceBodyMaxLen)}");
                        }

                        // IMPORTANT: Check responseCode even if result is Success
                        if (responseCode == 401 || responseCode == 403)
                        {
                            string errorCode = null;
                            if (!string.IsNullOrEmpty(responseText))
                            {
                                try
                                {
                                    var errorResult = JsonUtility.FromJson<ApiResult<object>>(responseText);
                                    if (errorResult != null)
                                    {
                                        if (!string.IsNullOrEmpty(errorResult.message))
                                        {
                                            errorMsg = errorResult.message;
                                        }
                                        if (!string.IsNullOrEmpty(errorResult.errorCode))
                                        {
                                            errorCode = errorResult.errorCode;
                                        }
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    Debug.LogWarning($"[BackendHttpClient] Failed to parse 401/403 error response: {parseEx.Message}");
                                }
                            }

                            HandleAuthError(responseCode, errorCode, errorMsg);
                            request.Dispose();
                            return ApiResult<T>.Error(errorMsg ?? "Unauthorized", errorCode);
                        }

                        // Try to parse as ApiResult<T> first
                        try
                        {
                            var apiResult = JsonUtility.FromJson<ApiResult<T>>(responseText);
                            if (apiResult != null)
                            {
                                if (traceEndpoint)
                                {
                                    bool dataNull = (object)apiResult.data == null;
                                    Debug.Log($"[FLOW][HTTP] Parsed ApiResult<{typeof(T).Name}> success={apiResult.success}, dataNull={dataNull}, errorCode={apiResult.errorCode}, message={apiResult.message}");
                                }

                                if (apiResult.success)
                                {
                                    request.Dispose();
                                    return apiResult;
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(apiResult.errorCode) &&
                                        (apiResult.errorCode == ErrorCodes.AUTH_FORCE_LOGOUT ||
                                         apiResult.errorCode == ErrorCodes.AUTH_SESSION_EXPIRED))
                                    {
                                        HandleAuthError(401, apiResult.errorCode, apiResult.message);
                                    }

                                    Debug.LogWarning($"[BackendHttpClient] Backend error: {apiResult.message} (ErrorCode: {apiResult.errorCode})");
                                    request.Dispose();
                                    return ApiResult<T>.Error(apiResult.message ?? "Request failed", apiResult.errorCode);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[BackendHttpClient] Failed to parse as ApiResult: {ex.Message}");
                        }

                        // If not ApiResult format, try to parse directly as T
                        try
                        {
                            T resultData = JsonUtility.FromJson<T>(responseText);
                            if (traceEndpoint)
                            {
                                bool dataNull = (object)resultData == null;
                                Debug.Log($"[FLOW][HTTP] Parsed direct<{typeof(T).Name}> dataNull={dataNull}");
                            }
                            request.Dispose();
                            return ApiResult<T>.Ok(resultData);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[BackendHttpClient] Failed to parse response: {ex.Message}\nResponse: {responseText}");
                            request.Dispose();
                            return ApiResult<T>.Error($"Failed to parse response: {ex.Message}");
                        }
                    }
                    else
                    {
                        string errorCode = null;
                        Debug.LogWarning($"[BackendHttpClient] Error {responseCode}: {errorMsg}\nBody: {responseText}");

                        if (traceEndpoint)
                        {
                            Debug.LogWarning($"[FLOW][HTTP] Error body {endpoint}: {TruncateForLog(RedactForLog(responseText), TraceBodyMaxLen)}");
                        }

                        if (!string.IsNullOrEmpty(responseText))
                        {
                            try
                            {
                                var errorResult = JsonUtility.FromJson<ApiResult<object>>(responseText);
                                if (errorResult != null)
                                {
                                    if (!string.IsNullOrEmpty(errorResult.message))
                                    {
                                        errorMsg = errorResult.message;
                                    }

                                    if (!string.IsNullOrEmpty(errorResult.errorCode))
                                    {
                                        errorCode = errorResult.errorCode;
                                    }
                                }
                            }
                            catch (Exception parseEx)
                            {
                                Debug.LogWarning($"[BackendHttpClient] Failed to parse error response body: {parseEx.Message}");
                            }
                        }

                        HandleAuthError(responseCode, errorCode, errorMsg);

                        if (responseCode == 401)
                        {
                            errorMsg = string.IsNullOrEmpty(errorMsg) ? "Unauthorized. Please login again." : errorMsg;
                        }
                        else if (responseCode == 403)
                        {
                            errorMsg = string.IsNullOrEmpty(errorMsg) ? "Forbidden. Please re-login or check permissions." : errorMsg;
                        }
                        else if (responseCode == 400)
                        {
                            errorMsg = string.IsNullOrEmpty(errorMsg) ? "Bad request. Please check your input." : errorMsg;
                        }
                        else if (responseCode == 500)
                        {
                            errorMsg = string.IsNullOrEmpty(errorMsg) ? "Server error. Please try again later." : errorMsg;
                        }

                        lastError = errorMsg;
                        lastErrorCode = errorCode;
                        request.Dispose();
                        // Exit the loop and return the error on non-transient failures
                        break;
                    }
                }
                finally
                {
                    if (request != null)
                    {
                        request.Dispose();
                    }
                }
            }

            return ApiResult<T>.Error(lastError ?? "HTTP request failed", lastErrorCode);
        }

        private static bool ShouldTraceEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return false;

            return endpoint.StartsWith("/api/friends", StringComparison.OrdinalIgnoreCase)
                   || endpoint.StartsWith("/api/party", StringComparison.OrdinalIgnoreCase)
                   || endpoint.StartsWith("/api/rooms", StringComparison.OrdinalIgnoreCase)
                   || endpoint.StartsWith("/api/matchmaking", StringComparison.OrdinalIgnoreCase)
                   || endpoint.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(endpoint, Constants.API_PROFILE_GET, StringComparison.OrdinalIgnoreCase);
        }

        private static string RedactForLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(
                text,
                "(\"(?:accessToken|refreshToken|token|joinToken|lobbyToken|password|sessionId)\"\\s*:\\s*\")[^\"]*(\")",
                "$1<redacted>$2",
                RegexOptions.IgnoreCase);
        }

        private static string TruncateForLog(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + $" ... (truncated, len={text.Length})";
        }

        private void HandleAuthError(long statusCode, string errorCode, string message)
        {
            bool authOrThrottle =
                statusCode == 401 ||
                statusCode == 403 ||
                statusCode == 429 ||
                string.Equals(errorCode, ErrorCodes.RATE_LIMIT_EXCEEDED) ||
                (!string.IsNullOrEmpty(errorCode) && errorCode.StartsWith("AUTH_", StringComparison.OrdinalIgnoreCase));

            if (!authOrThrottle)
                return;

            Debug.Log(
                $"[FLOW][HTTP] HandleAuthError statusCode={statusCode} errorCode={errorCode ?? "null"} message={message ?? "null"}");

            // Check for ban errors first
            if (string.Equals(errorCode, ErrorCodes.AUTH_ACCOUNT_BANNED) ||
                string.Equals(errorCode, ErrorCodes.AUTH_IP_BANNED) ||
                string.Equals(errorCode, ErrorCodes.AUTH_DEVICE_BANNED))
            {
                // Account/IP/Device banned - force logout
                Debug.LogError($"[BackendHttpClient] Account/IP/Device Banned: {message}");

                // Clear session
                if (SessionState.Instance != null)
                {
                    SessionState.Instance.ClearSession();
                }

                // Disconnect GameWebSocket on ban
                if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
                {
                    GameManager.Instance.GameWebSocket.Disconnect();
                }

                // Show ban notification (can be enhanced with UI popup)
                ShowLoginBlockedNotice(message ?? "Your account or device has been locked.");
                return;
            }

            // Check for rate limit errors
            if (statusCode == 429 || string.Equals(errorCode, ErrorCodes.RATE_LIMIT_EXCEEDED))
            {
                Debug.LogWarning($"[BackendHttpClient] Rate Limit Exceeded: {message}");
                // Rate limit exceeded - don't logout, just show warning
                // UI can handle this separately
                return;
            }

            if (statusCode == 401)
            {
                // Backend returns AUTH_FORCE_LOGOUT or AUTH_SESSION_EXPIRED error codes
                if (string.Equals(errorCode, ErrorCodes.AUTH_FORCE_LOGOUT))
                {
                    // Check if this is an active session (user A) or a login attempt (user B)
                    // Active session = GameWebSocket is connected (user is already logged in and active)
                    // Login attempt = GameWebSocket is NOT connected (user is trying to login/auto-login)
                    bool isActiveSession = SessionState.Instance != null &&
                                           SessionState.Instance.IsAuthenticated &&
                                           GameManager.Instance != null &&
                                           GameManager.Instance.GameWebSocket != null &&
                                           GameManager.Instance.GameWebSocket.IsWsConnected;

                    Debug.Log(
                        $"[BackendHttpClient] AUTH_FORCE_LOGOUT (AUTH_008) - isActiveSession: {isActiveSession}, IsAuthenticated: {SessionState.Instance?.IsAuthenticated}, IsWebSocketConnected: {GameManager.Instance?.GameWebSocket?.IsWsConnected}");

                    if (isActiveSession)
                    {
                        // User A: Already logged in and active, show force logout notice
                        // This is FORCE LOGOUT due to another device logging in
                        if (Interlocked.CompareExchange(ref _forceLogoutFlag, 1, 0) == 0)
                        {
                            Debug.Log(
                                "[BackendHttpClient] Starting ForceLogoutCoroutine for active session (AUTH_FORCE_LOGOUT)");
                            StartCoroutine(ForceLogoutCoroutine(
                                title: "Login Warning",
                                message: message ??
                                         "Your account was signed in from another device. You will be logged out."
                            ));
                        }
                        else
                        {
                            Debug.LogWarning(
                                "[BackendHttpClient] Force logout already in progress, skipping duplicate call");
                        }
                    }
                    else
                    {
                        // User B: Trying to login/auto-login
                        // Check if this is an auto-login attempt (has saved session) vs new login
                        bool isAutoLoginAttempt = SessionState.Instance != null &&
                                                  SessionState.Instance.IsAuthenticated &&
                                                  !string.IsNullOrEmpty(SessionState.Instance.SessionId);

                        if (isAutoLoginAttempt)
                        {
                            // This is auto-login - session might be stale (user closed app and reopened)
                            // Don't show "login blocked" - just silently fail auto-login and let user login manually
                            Debug.Log(
                                "[BackendHttpClient] AUTH_FORCE_LOGOUT during auto-login - session might be stale, allowing manual login");
                            // Don't show login blocked notice for auto-login failures
                            // The auto-login will fail and user can login manually
                        }
                        else
                        {
                            // This is a new login attempt - show login blocked notice
                            Debug.Log(
                                "[BackendHttpClient] Showing login blocked notice (AUTH_FORCE_LOGOUT during new login attempt)");
                            ShowLoginBlockedNotice(message ??
                                                   "This account is already signed in elsewhere. Please try again later.");
                        }
                    }
                }
                else if (string.Equals(errorCode, ErrorCodes.AUTH_SESSION_EXPIRED))
                {
                    // Session expired - handle if user is authenticated (regardless of polling status)
                    // This is SESSION EXPIRED (token/session naturally expired or invalidated)
                    bool isAuthenticated = SessionState.Instance != null && SessionState.Instance.IsAuthenticated;

                    Debug.Log(
                        $"[BackendHttpClient] AUTH_SESSION_EXPIRED (AUTH_007) - isAuthenticated: {isAuthenticated}");

                    if (isAuthenticated)
                    {
                        // User was authenticated but session is now invalid
                        // This is due to session/token expiration (not force logout)
                        // Show session expired notice to inform user
                        if (Interlocked.CompareExchange(ref _forceLogoutFlag, 1, 0) == 0)
                        {
                            Debug.Log("[BackendHttpClient] Starting ForceLogoutCoroutine for session expired");
                            StartCoroutine(ForceLogoutCoroutine(
                                title: "Session expired",
                                message: message ??
                                         "Your session has expired. Please log in again to continue."
                            ));
                        }
                        else
                        {
                            Debug.LogWarning(
                                "[BackendHttpClient] Force logout already in progress, skipping duplicate call");
                        }
                    }
                }
            }
        }

        private void ShowLoginBlockedNotice(string message)
        {
            var noticePopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.ToastService : null;
            if (noticePopup != null)
            {
                noticePopup.Show(
                    title: "Login Failed",
                    message: message
                );
            }
        }

        private System.Collections.IEnumerator ForceLogoutCoroutine(string title, string message)
        {
            // _forceLogoutFlag is already set to 1 by the caller (Interlocked.CompareExchange)

            Debug.Log($"[BackendHttpClient] Force logout triggered - Title: {title}, Message: {message}");

            // Show notice popup with auto dismiss after 2 seconds
            var noticePopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.ToastService : null;
            if (noticePopup != null)
            {
                noticePopup.Show(
                    title: title ?? "Warning",
                    message: message ?? "Your session has expired. You will be logged out."
                );
            }

            // Perform logout after popup dismissed
            // Call backend logout endpoint to clear session and force logout flag
            // Wait for logout to complete (with timeout) to ensure backend state is cleared
            bool logoutCompleted = false;
            if (GameManager.Instance != null && GameManager.Instance.AuthService != null)
            {
                GameManager.Instance.AuthService.Logout().ContinueWith(task =>
                {
                    logoutCompleted = true;
                    if (task.IsFaulted)
                    {
                        Debug.LogWarning(
                            $"[BackendHttpClient] Logout call failed during force logout: {task.Exception?.GetBaseException()?.Message}");
                    }
                });

                // Wait for logout to complete (max 3 seconds)
                float timeout = 3f;
                float elapsed = 0f;
                while (!logoutCompleted && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // Fallback: clear client-side session if AuthService not available
                if (SessionState.Instance != null)
                {
                    SessionState.Instance.ClearSession();
                }
            }

            // Clear client-side state
            if (SessionState.Instance != null)
            {
                SessionState.Instance.ClearSession();
            }

            if (RoomState.Instance != null)
            {
                RoomState.Instance.ClearRoom();
            }

            // Logout và quay về Login panel (không load scene, dùng UINavigator)
            SessionTerminationFlow.ShowAndLogout(
                title ?? "Warning",
                message ?? "Your session has expired. Please log in again.");

            Interlocked.Exchange(ref _forceLogoutFlag, 0);
        }

        private void ShowForceLogoutToast(string message)
        {
            try
            {
                ToastService.Instance.Show("Logout", message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Auth] {message} (toast unavailable: {ex.Message})");
            }
        }
    }
}
