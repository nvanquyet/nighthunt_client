using System;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.State;
using NightHunt.Core;
using NightHunt.Utils;
using NightHunt.UI;
using UnityEngine;

namespace NightHunt.Services.Auth
{
    public class AuthService : MonoBehaviour
    {
        [SerializeField] private IBackendClient backendClient;
        [SerializeField] private SessionState sessionState;

        private void Awake()
        {
            // Always use shared BackendClient from GameManager to keep auth token
            if (backendClient == null && GameManager.Instance != null)
            {
                backendClient = GameManager.Instance.BackendClient;
            }
            if (backendClient == null)
            {
#if UNITY_2023_1_OR_NEWER
                backendClient = FindFirstObjectByType<BackendHttpClient>();
#else
                backendClient = FindObjectOfType<BackendHttpClient>();
#endif
            }
            if (sessionState == null)
            {
                sessionState = SessionState.Instance;
            }
        }

        public async Task<ApiResult<AuthResponse>> Register(string username, string email, string password, string confirmPassword)
        {
            var request = new RegisterRequest
            {
                username = username,
                email = email,
                password = password,
                confirmPassword = confirmPassword
            };

            var result = await backendClient.PostAsync<AuthResponse>(Constants.API_AUTH_REGISTER, request);
            
            // Note: After successful registration, we do NOT auto-login
            // User must manually login after seeing success message
            // This is intentional for security and user experience
            
            return result;
        }

        public async Task<ApiResult<AuthResponse>> Login(string identifier, string password)
        {
            var request = new LoginRequest
            {
                identifier = identifier,
                password = password,
                deviceFingerprint = DeviceFingerprint.GetFingerprint()
            };

            var result = await backendClient.PostAsync<AuthResponse>(Constants.API_AUTH_LOGIN, request);
            
            if (result.Success && result.Data != null)
            {
                sessionState.SetSession(
                    result.Data.accessToken,
                    result.Data.sessionId,
                    result.Data.userId,
                    result.Data.username,
                    result.Data.email
                );

                // Connect to Game WebSocket (replaces polling)
                if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
                {
                    GameManager.Instance.GameWebSocket.Connect().ContinueWith(t => {
                        if (t.IsFaulted || (t.IsCompleted && !t.Result))
                            Debug.LogWarning("[AuthService] WebSocket connect failed after login");
                    });
                }
            }
            else
            {
                // Handle ban errors - force logout and show message
                HandleBanError(result);
            }

            return result;
        }

        public async Task<ApiResult<AuthResponse>> AutoLogin()
        {
            if (!sessionState.IsAuthenticated)
            {
                return ApiResult<AuthResponse>.Error("No saved session found");
            }

            var request = new AutoLoginRequest
            {
                accessToken = sessionState.AccessToken,
                sessionId = sessionState.SessionId,
                deviceFingerprint = DeviceFingerprint.GetFingerprint()
            };

            var result = await backendClient.PostAsync<AuthResponse>(Constants.API_AUTH_AUTO_LOGIN, request);
            
            if (result.Success && result.Data != null)
            {
                sessionState.SetSession(
                    result.Data.accessToken,
                    result.Data.sessionId,
                    result.Data.userId,
                    result.Data.username,
                    result.Data.email
                );

                // Connect to Game WebSocket (replaces polling)
                if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
                {
                    GameManager.Instance.GameWebSocket.Connect().ContinueWith(t => {
                        if (t.IsFaulted || (t.IsCompleted && !t.Result))
                            Debug.LogWarning("[AuthService] WebSocket connect failed after login");
                    });
                }
            }
            else
            {
                // Handle ban errors - force logout and show message
                HandleBanError(result);
                
                // Check if auto-login failed due to AUTH_FORCE_LOGOUT (stale session)
                // This happens when user closes app and reopens - old session still exists in backend
                bool isForceLogout = result.ErrorCode == ErrorCodes.AUTH_FORCE_LOGOUT;
                
                if (isForceLogout)
                {
                    // This is likely a stale session (user closed app and reopened)
                    // Silently clear session and allow user to login manually
                    Debug.Log("[AuthService] Auto-login failed due to AUTH_FORCE_LOGOUT - likely stale session, clearing and allowing manual login");
                }
                else
                {
                    // Other errors (ban, session expired, etc.) - log for debugging
                    Debug.LogWarning($"[AuthService] Auto-login failed: {result.ErrorCode} - {result.Message}");
                }
                
                // Auto-login failed, clear session immediately
                sessionState.ClearSession();
                
                // Disconnect Game WebSocket
                if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
                {
                    GameManager.Instance.GameWebSocket.Disconnect();
                }
            }

            return result;
        }

        public async Task<ApiResult> ChangePassword(string oldPassword, string newPassword, string confirmNewPassword)
        {
            var request = new ChangePasswordRequest
            {
                oldPassword = oldPassword,
                newPassword = newPassword,
                confirmNewPassword = confirmNewPassword
            };

            var result = await backendClient.PostAsync<object>(Constants.API_AUTH_CHANGE_PASSWORD, request);
            
            if (result.Success)
            {
                // Password changed successfully, session will be invalidated
                sessionState.ClearSession();
            }

            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        public async Task<ApiResult> Logout()
        {
            // Call backend logout endpoint to clear session and force logout flag
            // Even if backend call fails, we still clear client-side session
            bool backendLogoutSuccess = false;
            if (sessionState != null && sessionState.IsAuthenticated)
            {
                try
                {
                    var result = await backendClient.PostAsync<object>(Constants.API_AUTH_LOGOUT, null);
                    backendLogoutSuccess = result.Success;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AuthService] Logout backend call failed: {ex.Message}");
                }
            }
            
            // Clear client-side session regardless of backend result
            sessionState.ClearSession();

            // Disconnect Game WebSocket on logout
            if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
            {
                GameManager.Instance.GameWebSocket.Disconnect(disableReconnect: true);
            }
            
            return ApiResult.Ok();
        }
        
        /// <summary>
        /// Handle ban errors - force logout and show notification
        /// </summary>
        private void HandleBanError(ApiResult<AuthResponse> result)
        {
            if (result == null || string.IsNullOrEmpty(result.ErrorCode))
            {
                return;
            }
            
            // Check if it's a ban error
            bool isBanError = result.ErrorCode == ErrorCodes.AUTH_ACCOUNT_BANNED ||
                             result.ErrorCode == ErrorCodes.AUTH_IP_BANNED ||
                             result.ErrorCode == ErrorCodes.AUTH_DEVICE_BANNED;
            
            if (isBanError)
            {
                // Force logout
                sessionState.ClearSession();

                // Disconnect Game WebSocket
                if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
                {
                    GameManager.Instance.GameWebSocket.Disconnect();
                }
                
                // Show ban notification
                ShowBanNotification(result.Message ?? "Tài khoản hoặc thiết bị đã bị khóa.");
            }
        }
        
        /// <summary>
        /// Show ban notification to user
        /// </summary>
        private void ShowBanNotification(string message)
        {
            Debug.LogError($"[AuthService] Account/Device Banned: {message}");
            var notif = UINotificationService.Instance;
            if (notif != null)
                notif.Notice("Tài khoản bị khóa", message);
        }
    }
}

