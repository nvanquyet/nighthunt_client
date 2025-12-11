using System;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.State;
using NightHunt.Core;
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
            
            if (result.Success && result.Data != null)
            {
                sessionState.SetSession(
                    result.Data.accessToken,
                    result.Data.sessionId,
                    result.Data.userId,
                    result.Data.username,
                    result.Data.email
                );
                backendClient.SetAuthToken(result.Data.accessToken);
                
                // Start session monitoring after successful registration
                if (GameManager.Instance != null && GameManager.Instance.SessionMonitor != null)
                {
                    GameManager.Instance.SessionMonitor.StartPolling();
                }
            }

            return result;
        }

        public async Task<ApiResult<AuthResponse>> Login(string identifier, string password)
        {
            var request = new LoginRequest
            {
                identifier = identifier,
                password = password
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
                backendClient.SetAuthToken(result.Data.accessToken);
                
                // Start session monitoring after successful login
                if (GameManager.Instance != null && GameManager.Instance.SessionMonitor != null)
                {
                    GameManager.Instance.SessionMonitor.StartPolling();
                }
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
                sessionId = sessionState.SessionId
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
                backendClient.SetAuthToken(result.Data.accessToken);
                
                // Start session monitoring after successful auto-login
                if (GameManager.Instance != null && GameManager.Instance.SessionMonitor != null)
                {
                    GameManager.Instance.SessionMonitor.StartPolling();
                }
            }
            else
            {
                // Auto-login failed, clear session immediately
                // This ensures HandleAuthError can correctly identify this as a login attempt (not an active session)
                // Note: HandleAuthError is called BEFORE this, but clearing here ensures state is correct for any retry
                sessionState.ClearSession();
                backendClient.ClearAuthToken();
                
                // Stop session monitoring
                if (GameManager.Instance != null && GameManager.Instance.SessionMonitor != null)
                {
                    GameManager.Instance.SessionMonitor.StopPolling();
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
                backendClient.ClearAuthToken();
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
            backendClient.ClearAuthToken();
            
            // Stop session monitoring on logout
            if (GameManager.Instance != null && GameManager.Instance.SessionMonitor != null)
            {
                GameManager.Instance.SessionMonitor.StopPolling();
            }
            
            return ApiResult.Ok();
        }
    }
}

