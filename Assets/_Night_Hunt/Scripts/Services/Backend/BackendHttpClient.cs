using System;
using System.Text;
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
        [SerializeField] private BackendConfig config;
        public BackendConfig Config => config;

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

        public string GetBaseUrl()
        {
            return config != null ? config.GetApiBaseUrl() : "";
        }

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
            if (config == null)
            {
                Debug.LogError("BackendConfig is not assigned!");
                return ApiResult<T>.Error("Backend configuration not found");
            }

            string baseUrl = config.GetApiBaseUrl();
            string url = baseUrl + endpoint;
            bool traceEndpoint = ShouldTraceEndpoint(endpoint);

            if (traceEndpoint)
            {
                Debug.Log($"[BackendHttpClient][TRACE] Request => {method} {endpoint} (userId={SessionState.Instance?.UserId ?? 0}, auth={(SessionState.Instance != null && SessionState.Instance.IsAuthenticated)})");
            }

            UnityWebRequest request;

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

            request.timeout = config.requestTimeoutSeconds;

            // Attach AcceptAllCertificatesHandler nếu server dùng self-signed cert (mkcert + IP)
            if (config.ShouldBypassSslCertificateValidation())
            {
                request.certificateHandler = new NightHunt.Config.AcceptAllCertificatesHandler();
            }

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            try
            {
                if (traceEndpoint)
                {
                    Debug.Log($"[BackendHttpClient][TRACE] Response <= {method} {endpoint} status={request.responseCode} result={request.result}");
                }

                // SSL errors now handled at request level via CertificateHandler.

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    if (traceEndpoint)
                    {
                        Debug.Log($"[BackendHttpClient][TRACE] Body {endpoint}: {TruncateForLog(jsonResponse, TraceBodyMaxLen)}");
                    }

                    // IMPORTANT: Check responseCode even if result is Success
                    // Backend may return 401/403 with errorCode in response body
                    // Unity may treat 401 as Success in some cases
                    if (request.responseCode == 401 || request.responseCode == 403)
                    {
                        // Parse error response to get errorCode and message
                        string errorMessage = request.error;
                        string errorCode = null;

                        if (!string.IsNullOrEmpty(jsonResponse))
                        {
                            try
                            {
                                var errorResult = JsonUtility.FromJson<ApiResult<object>>(jsonResponse);
                                if (errorResult != null)
                                {
                                    if (!string.IsNullOrEmpty(errorResult.message))
                                    {
                                        errorMessage = errorResult.message;
                                    }

                                    if (!string.IsNullOrEmpty(errorResult.errorCode))
                                    {
                                        errorCode = errorResult.errorCode;
                                    }
                                }
                            }
                            catch (Exception parseEx)
                            {
                                Debug.LogWarning(
                                    $"[BackendHttpClient] Failed to parse 401/403 error response: {parseEx.Message}");
                            }
                        }

                        // Handle auth errors (force logout, session expired) BEFORE returning error
                        HandleAuthError(request.responseCode, errorCode, errorMessage);

                        return ApiResult<T>.Error(errorMessage ?? "Unauthorized", errorCode);
                    }

                    // Try to parse as ApiResult<T> first (backend format: { "success": true/false, "data": {...}, "message": "..." })
                    try
                    {
                        var apiResult = JsonUtility.FromJson<ApiResult<T>>(jsonResponse);
                        if (apiResult != null)
                        {
                            if (traceEndpoint)
                            {
                                bool dataNull = (object)apiResult.data == null;
                                Debug.Log($"[BackendHttpClient][TRACE] Parsed ApiResult<{typeof(T).Name}> success={apiResult.success}, dataNull={dataNull}, errorCode={apiResult.errorCode}, message={apiResult.message}");
                            }

                            // Check if success field is set correctly
                            if (apiResult.success)
                            {
                                return apiResult;
                            }
                            else
                            {
                                // Backend returned error in ApiResponse format
                                // Check if this is an auth error (401/403) even though responseCode might not be set correctly
                                if (!string.IsNullOrEmpty(apiResult.errorCode) &&
                                    (apiResult.errorCode == ErrorCodes.AUTH_FORCE_LOGOUT ||
                                     apiResult.errorCode == ErrorCodes.AUTH_SESSION_EXPIRED))
                                {
                                    HandleAuthError(401, apiResult.errorCode, apiResult.message);
                                }

                                Debug.LogWarning(
                                    $"[BackendHttpClient] Backend error: {apiResult.message} (ErrorCode: {apiResult.errorCode})");
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
                        T resultData = JsonUtility.FromJson<T>(jsonResponse);
                        if (traceEndpoint)
                        {
                            bool dataNull = (object)resultData == null;
                            Debug.Log($"[BackendHttpClient][TRACE] Parsed direct<{typeof(T).Name}> dataNull={dataNull}");
                        }
                        return ApiResult<T>.Ok(resultData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[BackendHttpClient] Failed to parse response: {ex.Message}\nResponse: {jsonResponse}");
                        return ApiResult<T>.Error($"Failed to parse response: {ex.Message}");
                    }
                }
                else
                {
                    // Try to parse error response from backend
                    string errorMessage = request.error;
                    string responseText = request.downloadHandler.text;
                    string errorCode = null;

                    Debug.LogWarning(
                        $"[BackendHttpClient] Error {request.responseCode}: {errorMessage}\nBody: {responseText}");

                    if (traceEndpoint)
                    {
                        Debug.LogWarning($"[BackendHttpClient][TRACE] Error body {endpoint}: {TruncateForLog(responseText, TraceBodyMaxLen)}");
                    }

                    // Try to parse as ApiResult to get message and errorCode from backend
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        try
                        {
                            var errorResult = JsonUtility.FromJson<ApiResult<object>>(responseText);
                            if (errorResult != null)
                            {
                                if (!string.IsNullOrEmpty(errorResult.message))
                                {
                                    errorMessage = errorResult.message;
                                }

                                if (!string.IsNullOrEmpty(errorResult.errorCode))
                                {
                                    errorCode = errorResult.errorCode;
                                }
                            }
                        }
                        catch (Exception parseEx)
                        {
                            Debug.LogWarning(
                                $"[BackendHttpClient] Failed to parse error response body: {parseEx.Message}");
                        }
                    }

                    // Handle auth errors (force logout, session expired) BEFORE returning error
                    // This ensures popup is shown immediately
                    HandleAuthError(request.responseCode, errorCode, errorMessage);

                    // Map HTTP status codes to user-friendly messages
                    if (request.responseCode == 401)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage)
                            ? "Unauthorized. Please login again."
                            : errorMessage;
                    }
                    else if (request.responseCode == 403)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage)
                            ? "Forbidden. Please re-login or check permissions."
                            : errorMessage;
                    }
                    else if (request.responseCode == 400)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage)
                            ? "Bad request. Please check your input."
                            : errorMessage;
                    }
                    else if (request.responseCode == 500)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage)
                            ? "Server error. Please try again later."
                            : errorMessage;
                    }

                    return ApiResult<T>.Error(errorMessage, errorCode);
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        private static bool ShouldTraceEndpoint(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return false;

            return endpoint.StartsWith("/api/friends", StringComparison.OrdinalIgnoreCase)
                   || endpoint.StartsWith("/api/party", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(endpoint, Constants.API_PROFILE_GET, StringComparison.OrdinalIgnoreCase);
        }

        private static string TruncateForLog(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + $" ... (truncated, len={text.Length})";
        }

        private void HandleAuthError(long statusCode, string errorCode, string message)
        {
            Debug.Log(
                $"[BackendHttpClient] HandleAuthError called - statusCode: {statusCode}, errorCode: {errorCode}, message: {message}");

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
                ShowLoginBlockedNotice(message ?? "Tài khoản hoặc thiết bị đã bị khóa.");
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
                                title: "Cảnh báo đăng nhập",
                                message: message ??
                                         "Có người khác đã đăng nhập vào tài khoản của bạn từ thiết bị khác. Bạn sẽ bị đăng xuất."
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
                                                   "Tài khoản này đã được đăng nhập ở nơi khác. Vui lòng thử lại sau.");
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
                                title: "Phiên đăng nhập hết hạn",
                                message: message ??
                                         "Phiên đăng nhập của bạn đã hết hạn. Vui lòng đăng nhập lại để tiếp tục."
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
                    title: "Đăng nhập thất bại",
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
                    title: title ?? "Cảnh báo",
                    message: message ?? "Phiên đăng nhập đã hết hạn. Bạn sẽ bị đăng xuất."
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
            LoginView.Logout();

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