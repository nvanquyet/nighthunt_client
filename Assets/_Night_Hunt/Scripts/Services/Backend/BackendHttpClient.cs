using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data;
using NightHunt.State;
using NightHunt.Core;
using NightHunt.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.Services.Backend
{
    public class BackendHttpClient : MonoBehaviour, IBackendClient
    {
        [SerializeField] private BackendConfig config;
        
        private string authToken;
        private bool sslCertificateInitialized = false;
        private bool forceLogoutInProgress = false;
        private const string SessionHeader = "X-Session-Id";

        private void Awake()
        {
            // Restore token from SessionState if available (e.g., domain reload)
            if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                authToken = SessionState.Instance.AccessToken;
            }

            // Initialize SSL certificate handling for local development
            if (config != null && config.ignoreSslCertificate)
            {
                InitializeSslCertificateIgnore();
            }
        }

        private void InitializeSslCertificateIgnore()
        {
            if (sslCertificateInitialized) return;
            
            // Unity doesn't have a direct way to ignore SSL certificates
            // This is handled at the UnityWebRequest level
            // For production, make sure ignoreSslCertificate is false
            if (Application.isEditor || Debug.isDebugBuild)
            {
                Debug.LogWarning("SSL certificate validation is disabled. Only use this in development!");
            }
            
            sslCertificateInitialized = true;
        }

        public void SetAuthToken(string token)
        {
            authToken = token;
        }

        public void ClearAuthToken()
        {
            authToken = null;
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

            // Add auth token if available
            if (!string.IsNullOrEmpty(authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            }
            // Attach sessionId for server-side session validation
            if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated && !string.IsNullOrEmpty(SessionState.Instance.SessionId))
            {
                request.SetRequestHeader(SessionHeader, SessionState.Instance.SessionId);
            }

            request.timeout = config.requestTimeoutSeconds;

            // Handle SSL certificate validation for local development
            if (config.ignoreSslCertificate && config.useHttps)
            {
                // UnityWebRequest doesn't directly support ignoring SSL certificates
                // This is a limitation - for production, use proper SSL certificates
                // For local dev with self-signed certs, you may need to configure Unity's certificate handling
                // or use a reverse proxy with valid certificates
            }

            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            try
            {
                // Handle SSL certificate errors for development
                if (request.result == UnityWebRequest.Result.ConnectionError && 
                    config != null && config.ignoreSslCertificate && config.useHttps)
                {
                    // Log warning but continue - in production this should not happen
                    Debug.LogWarning($"SSL connection error (ignored for dev): {request.error}");
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[BackendHttpClient] Response: {jsonResponse}");
                    
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
                            catch
                            {
                                // If parsing fails, try to extract errorCode manually
                                if (jsonResponse.Contains("\"errorCode\""))
                                {
                                    try
                                    {
                                        int errorCodeStart = jsonResponse.IndexOf("\"errorCode\"");
                                        int colonIndex = jsonResponse.IndexOf(':', errorCodeStart);
                                        int quoteStart = jsonResponse.IndexOf('"', colonIndex) + 1;
                                        int quoteEnd = jsonResponse.IndexOf('"', quoteStart);
                                        if (quoteEnd > quoteStart)
                                        {
                                            errorCode = jsonResponse.Substring(quoteStart, quoteEnd - quoteStart);
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore extraction errors
                                    }
                                }
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
                            // Check if success field is set correctly
                            if (apiResult.success)
                            {
                                Debug.Log($"[BackendHttpClient] Success: {apiResult.success}, Data: {apiResult.data}");
                                return apiResult;
                            }
                            else
                            {
                                // Backend returned error in ApiResponse format
                                // Check if this is an auth error (401/403) even though responseCode might not be set correctly
                                if (!string.IsNullOrEmpty(apiResult.errorCode) && 
                                    (apiResult.errorCode == "AUTH_FORCE_LOGOUT" || apiResult.errorCode == "AUTH_SESSION_EXPIRED"))
                                {
                                    HandleAuthError(401, apiResult.errorCode, apiResult.message);
                                }
                                
                                Debug.LogWarning($"[BackendHttpClient] Backend error: {apiResult.message} (ErrorCode: {apiResult.errorCode})");
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
                        Debug.Log($"[BackendHttpClient] Parsed directly as T: {resultData}");
                        return ApiResult<T>.Ok(resultData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[BackendHttpClient] Failed to parse response: {ex.Message}\nResponse: {jsonResponse}");
                        return ApiResult<T>.Error($"Failed to parse response: {ex.Message}");
                    }
                }
                else
                {
                    // Try to parse error response from backend
                    string errorMessage = request.error;
                    string responseText = request.downloadHandler.text;
                    string errorCode = null;

                    Debug.LogWarning($"[BackendHttpClient] Error {request.responseCode}: {errorMessage}\nBody: {responseText}");

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
                        catch
                        {
                            // If parsing fails, try to extract errorCode manually from JSON string
                            if (responseText.Contains("\"errorCode\""))
                            {
                                try
                                {
                                    int errorCodeStart = responseText.IndexOf("\"errorCode\"");
                                    int colonIndex = responseText.IndexOf(':', errorCodeStart);
                                    int quoteStart = responseText.IndexOf('"', colonIndex) + 1;
                                    int quoteEnd = responseText.IndexOf('"', quoteStart);
                                    if (quoteEnd > quoteStart)
                                    {
                                        errorCode = responseText.Substring(quoteStart, quoteEnd - quoteStart);
                                    }
                                }
                                catch
                                {
                                    // Ignore extraction errors
                                }
                            }
                            
                            // Use response text directly for common codes
                            if (request.responseCode == 400 || request.responseCode == 401 || request.responseCode == 403 || request.responseCode == 500)
                            {
                                if (string.IsNullOrEmpty(errorMessage))
                                {
                                    errorMessage = responseText;
                                }
                            }
                        }
                    }
                    
                    // Handle auth errors (force logout, session expired) BEFORE returning error
                    // This ensures popup is shown immediately
                    HandleAuthError(request.responseCode, errorCode, errorMessage);
                    
                    // Map HTTP status codes to user-friendly messages
                    if (request.responseCode == 401)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage) ? "Unauthorized. Please login again." : errorMessage;
                    }
                    else if (request.responseCode == 403)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage) ? "Forbidden. Please re-login or check permissions." : errorMessage;
                    }
                    else if (request.responseCode == 400)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage) ? "Bad request. Please check your input." : errorMessage;
                    }
                    else if (request.responseCode == 500)
                    {
                        errorMessage = string.IsNullOrEmpty(errorMessage) ? "Server error. Please try again later." : errorMessage;
                    }
                    
                    return ApiResult<T>.Error(errorMessage, errorCode);
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        private void HandleAuthError(long statusCode, string errorCode, string message)
        {
            Debug.Log($"[BackendHttpClient] HandleAuthError called - statusCode: {statusCode}, errorCode: {errorCode}, message: {message}");
            
            if (statusCode == 401)
            {
                // Backend returns errorCode as "AUTH_008" for AUTH_FORCE_LOGOUT and "AUTH_007" for AUTH_SESSION_EXPIRED
                if (string.Equals(errorCode, "AUTH_008") || string.Equals(errorCode, "AUTH_FORCE_LOGOUT"))
                {
                    // Check if this is an active session (user A) or a login attempt (user B)
                    // Active session = SessionMonitor is polling (user is already logged in and active)
                    // Login attempt = SessionMonitor is NOT polling (user is trying to login/auto-login)
                    bool isActiveSession = SessionState.Instance != null && 
                                          SessionState.Instance.IsAuthenticated &&
                                          GameManager.Instance != null && 
                                          GameManager.Instance.SessionMonitor != null && 
                                          GameManager.Instance.SessionMonitor.IsPolling;
                    
                    Debug.Log($"[BackendHttpClient] AUTH_FORCE_LOGOUT (AUTH_008) - isActiveSession: {isActiveSession}, IsAuthenticated: {SessionState.Instance?.IsAuthenticated}, IsPolling: {GameManager.Instance?.SessionMonitor?.IsPolling}");
                    
                    if (isActiveSession)
                    {
                        // User A: Already logged in and active, show force logout notice
                        // This is FORCE LOGOUT due to another device logging in
                        if (!forceLogoutInProgress)
                        {
                            Debug.Log("[BackendHttpClient] Starting ForceLogoutCoroutine for active session (AUTH_FORCE_LOGOUT)");
                            StartCoroutine(ForceLogoutCoroutine(
                                title: "Cảnh báo đăng nhập",
                                message: message ?? "Có người khác đã đăng nhập vào tài khoản của bạn từ thiết bị khác. Bạn sẽ bị đăng xuất."
                            ));
                        }
                        else
                        {
                            Debug.LogWarning("[BackendHttpClient] Force logout already in progress, skipping duplicate call");
                        }
                    }
                    else
                    {
                        // User B: Trying to login/auto-login, show login blocked notice
                        // This is LOGIN BLOCKED because account is already logged in elsewhere
                        Debug.Log("[BackendHttpClient] Showing login blocked notice (AUTH_FORCE_LOGOUT during login attempt)");
                        ShowLoginBlockedNotice(message ?? "Tài khoản này đã được đăng nhập ở nơi khác. Vui lòng thử lại sau.");
                    }
                }
                else if (string.Equals(errorCode, "AUTH_007") || string.Equals(errorCode, "AUTH_SESSION_EXPIRED"))
                {
                    // Session expired - handle if user is authenticated (regardless of polling status)
                    // This is SESSION EXPIRED (token/session naturally expired or invalidated)
                    bool isAuthenticated = SessionState.Instance != null && SessionState.Instance.IsAuthenticated;
                    
                    Debug.Log($"[BackendHttpClient] AUTH_SESSION_EXPIRED (AUTH_007) - isAuthenticated: {isAuthenticated}");
                    
                    if (isAuthenticated)
                    {
                        // User was authenticated but session is now invalid
                        // This is due to session/token expiration (not force logout)
                        // Show session expired notice to inform user
                        if (!forceLogoutInProgress)
                        {
                            Debug.Log("[BackendHttpClient] Starting ForceLogoutCoroutine for session expired");
                            StartCoroutine(ForceLogoutCoroutine(
                                title: "Phiên đăng nhập hết hạn",
                                message: message ?? "Phiên đăng nhập của bạn đã hết hạn. Vui lòng đăng nhập lại để tiếp tục."
                            ));
                        }
                        else
                        {
                            Debug.LogWarning("[BackendHttpClient] Force logout already in progress, skipping duplicate call");
                        }
                    }
                }
            }
        }
        
        private void ShowLoginBlockedNotice(string message)
        {
            var noticePopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.NoticePopup : null;
            if (noticePopup != null)
            {
                noticePopup.Show(
                    title: "Đăng nhập thất bại",
                    message: message,
                    onConfirm: () =>
                    {
                        // Just close the popup, user can try again
                    },
                    autoDismissSeconds: 3f // Auto dismiss after 3 seconds
                );
            }
            else
            {
                // Fallback: use toast
                ToastService.Instance?.Show(message, 3f);
            }
        }

        private System.Collections.IEnumerator ForceLogoutCoroutine(string title, string message)
        {
            forceLogoutInProgress = true;

            Debug.Log($"[BackendHttpClient] Force logout triggered - Title: {title}, Message: {message}");

            // Show notice popup with auto dismiss after 2 seconds
            var noticePopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.NoticePopup : null;
            if (noticePopup != null)
            {
                bool popupDismissed = false;
                
                noticePopup.Show(
                    title: title ?? "Cảnh báo",
                    message: message ?? "Phiên đăng nhập đã hết hạn. Bạn sẽ bị đăng xuất.",
                    onConfirm: () =>
                    {
                        popupDismissed = true;
                    },
                    autoDismissSeconds: 2f
                );

                // Wait for popup to be dismissed (either by user clicking OK or auto dismiss)
                while (!popupDismissed && noticePopup.IsShowing())
                {
                    yield return null;
                }
            }
            else
            {
                // Fallback: use toast if notice popup not available
                Debug.LogWarning("[BackendHttpClient] NoticePopup not available, using toast fallback");
                ToastService.Instance?.Show(message ?? "Phiên đăng nhập đã hết hạn. Đang đăng xuất...", 2f);
                yield return new WaitForSeconds(2f);
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
                        Debug.LogWarning($"[BackendHttpClient] Logout call failed during force logout: {task.Exception?.GetBaseException()?.Message}");
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
                ClearAuthToken();
            }
            
            // Clear client-side state
            if (SessionState.Instance != null)
            {
                SessionState.Instance.ClearSession();
            }
            ClearAuthToken();
            
            if (RoomState.Instance != null)
            {
                RoomState.Instance.ClearRoom();
            }
            SceneLoader.LoadLogin();

            forceLogoutInProgress = false;
        }

        private void ShowForceLogoutToast(string message)
        {
            try
            {
                ToastService.Instance.Show(message, 2f);
            }
            catch
            {
                Debug.LogWarning($"[Auth] {message}");
            }
        }
    }
}

