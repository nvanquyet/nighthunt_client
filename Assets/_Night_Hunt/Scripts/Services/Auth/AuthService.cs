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
        [SerializeField] private SessionState   sessionState;

        private void Awake()
        {
            // Always use shared BackendClient from GameManager to keep auth token
            if (backendClient == null && GameManager.Instance != null)
                backendClient = GameManager.Instance.BackendClient;
            if (backendClient == null)
            {
#if UNITY_2023_1_OR_NEWER
                backendClient = FindFirstObjectByType<BackendHttpClient>();
#else
                backendClient = FindObjectOfType<BackendHttpClient>();
#endif
            }
            if (sessionState == null)
                sessionState = SessionState.Instance;
        }

        // ─── Register ─────────────────────────────────────────────────────────

        public async Task<ApiResult<AuthResponse>> Register(
            string username, string email, string password, string confirmPassword)
        {
            var request = new RegisterRequest
            {
                username        = username,
                email           = email,
                password        = password,
                confirmPassword = confirmPassword
            };
            var result = await backendClient.PostAsync<AuthResponse>(Constants.API_AUTH_REGISTER, request);
            // After registration user must login explicitly — no session issued.
            return result;
        }

        // ─── Login ────────────────────────────────────────────────────────────

        public async Task<ApiResult<AuthResponse>> Login(string identifier, string password)
        {
            var request = new LoginRequest
            {
                identifier        = identifier,
                password          = password,
                deviceFingerprint = DeviceFingerprint.GetFingerprint()
            };

            var result = await backendClient.PostAsync<AuthResponse>(Constants.API_AUTH_LOGIN, request);

            if (result.Success && result.Data != null)
            {
                ApplyAuthResponse(result.Data);
                
                // SEC-FIX: Store refresh token in encrypted storage
                SecureStorage.SetString(LoadingManager.KEY_REFRESH_TOKEN, result.Data.refreshToken ?? "");
            }
            else
            {
                HandleBanError(result);
            }

            return result;
        }

        // ─── Auto-Login via Refresh Token (Production flow) ───────────────────

        /// <summary>
        /// Called on app startup. Reads the persisted refresh token and exchanges it
        /// for a new access token via POST /auth/refresh-token.
        ///
        /// Flow:
        ///   1. Read refreshToken from PlayerPrefs[KEY_REFRESH_TOKEN].
        ///   2. Call POST /auth/refresh-token.
        ///   3. On success  → ApplyAuthResponse (stores new access + refresh tokens + profile).
        ///   4. On failure  → clear tokens, caller shows Login screen.
        /// </summary>
        public async Task<ApiResult<AuthResponse>> AutoLogin()
        {
            // SEC-FIX: Read refresh token from encrypted storage
            string refreshToken = SecureStorage.GetString(LoadingManager.KEY_REFRESH_TOKEN, "");
            if (string.IsNullOrEmpty(refreshToken))
            {
                return ApiResult<AuthResponse>.Error("No refresh token found");
            }

            var request = new RefreshTokenRequest { refreshToken = refreshToken };
            var result  = await backendClient.PostAsync<AuthResponse>(
                Constants.API_AUTH_REFRESH_TOKEN, request);

            if (result.Success && result.Data != null)
            {
                ApplyAuthResponse(result.Data);
                // SEC-FIX: Persist the rotated refresh token in encrypted storage
                SecureStorage.SetString(LoadingManager.KEY_REFRESH_TOKEN, result.Data.refreshToken ?? "");
            }
            else
            {
                HandleBanError(result);

                bool isForceLogout = result.ErrorCode == ErrorCodes.AUTH_FORCE_LOGOUT;
                if (isForceLogout)
                    Debug.Log("[AuthService] AutoLogin blocked (AUTH_FORCE_LOGOUT) — clearing tokens.");
                else
                    Debug.LogWarning($"[AuthService] AutoLogin failed: {result.ErrorCode} — {result.Message}");

                // Token invalid / expired / revoked → clear everything
                SecureStorage.DeleteKey(LoadingManager.KEY_REFRESH_TOKEN);
                PlayerPrefs.DeleteKey(LoadingManager.KEY_REMEMBER_ME);
                PlayerPrefs.Save();
                sessionState.ClearSession();

                DisconnectWebSocket();
            }

            return result;
        }

        // ─── Logout ───────────────────────────────────────────────────────────

        public async Task<ApiResult> Logout()
        {
            if (sessionState != null && sessionState.IsAuthenticated)
            {
                try
                {
                    await backendClient.PostAsync<object>(Constants.API_AUTH_LOGOUT, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AuthService] Logout backend call failed: {ex.Message}");
                }
            }

            // Always clear local state regardless of backend result
            sessionState.ClearSession();
            DisconnectWebSocket();
            return ApiResult.Ok();
        }

        // ─── Change Password ──────────────────────────────────────────────────

        public async Task<ApiResult> ChangePassword(
            string oldPassword, string newPassword, string confirmNewPassword)
        {
            var request = new ChangePasswordRequest
            {
                oldPassword        = oldPassword,
                newPassword        = newPassword,
                confirmNewPassword = confirmNewPassword
            };
            var result = await backendClient.PostAsync<object>(Constants.API_AUTH_CHANGE_PASSWORD, request);
            if (result.Success)
                sessionState.ClearSession();
            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Applies an AuthResponse to SessionState and syncs SelectedCharacterId to PlayerPrefs.
        /// Shared by Login and AutoLogin so logic is not duplicated.
        /// </summary>
        private void ApplyAuthResponse(AuthResponse data)
        {
            // SEC-FIX: Email removed from SetSession (not stored client-side)
            sessionState.SetSession(
                data.accessToken,
                data.sessionId,
                data.userId,
                data.username,
                data.selectedCharacterId);

            Debug.Log($"[AuthService] Session applied — userId={data.userId} " +
                      $"selectedCharacterId={data.selectedCharacterId ?? "null"}");

            // Connect to Game WebSocket
            if (GameManager.Instance?.GameWebSocket != null)
            {
                GameManager.Instance.GameWebSocket.Connect().ContinueWith(t =>
                {
                    if (t.IsFaulted || (t.IsCompleted && !t.Result))
                        Debug.LogWarning("[AuthService] WebSocket connect failed after auth.");
                });
            }
        }

        private void DisconnectWebSocket()
        {
            if (GameManager.Instance?.GameWebSocket != null)
                GameManager.Instance.GameWebSocket.Disconnect(disableReconnect: true);
        }

        private void HandleBanError(ApiResult<AuthResponse> result)
        {
            if (result == null || string.IsNullOrEmpty(result.ErrorCode)) return;

            bool isBanError = result.ErrorCode == ErrorCodes.AUTH_ACCOUNT_BANNED
                           || result.ErrorCode == ErrorCodes.AUTH_IP_BANNED
                           || result.ErrorCode == ErrorCodes.AUTH_DEVICE_BANNED;
            if (!isBanError) return;

            sessionState.ClearSession();
            DisconnectWebSocket();
            ToastService.Instance?.Show("Tài khoản bị khóa", result.Message ?? "Tài khoản hoặc thiết bị đã bị khóa.");
        }
    }
}
