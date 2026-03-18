using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.State;
using NightHunt.UI;
using NightHunt.Utils;
using UnityEngine;

namespace NightHunt.Services.Friend
{
    /// <summary>
    /// Friend System Service — manages friend list, friend requests, and blocked users.
    ///
    /// API summary (all paths verified against server FriendController):
    ///   GET    /api/friends                            → List&lt;FriendResponse&gt;
    ///   DELETE /api/friends/{friendUserId}             → remove friend
    ///   POST   /api/friends/requests                   → send request {username} or {userId}
    ///   GET    /api/friends/requests/incoming          → List&lt;FriendRequestResponse&gt;
    ///   GET    /api/friends/requests/outgoing          → List&lt;FriendRequestResponse&gt;
    ///   POST   /api/friends/requests/{id}/accept       → accept
    ///   POST   /api/friends/requests/{id}/decline      → decline (server uses "decline" not "reject")
    ///   DELETE /api/friends/requests/{id}              → cancel outgoing (no /cancel suffix)
    ///   GET    /api/friends/blocked                    → List&lt;Long&gt; (just user IDs)
    ///   POST   /api/friends/block/{userId}             → block (path param)
    ///   DELETE /api/friends/block/{userId}             → unblock (path param)
    /// </summary>
    public class FriendService : MonoBehaviour
    {
        [SerializeField] private IBackendClient backendClient;
        [SerializeField] private SessionState   sessionState;

        private void Awake()
        {
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

        // ══════════════════════════════════════════════════════════════════════
        // FRIEND LIST
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/friends — List of friends with online status.
        /// Cached 30 s, invalidated by WS friend events.
        /// </summary>
        public async Task<ApiResult<List<FriendResponse>>> GetFriends()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<List<FriendResponse>>.Error("Not authenticated");

            if (APICache.TryGet(APICache.KEY_FRIENDS_LIST, out List<FriendResponse> cached))
                return ApiResult<List<FriendResponse>>.Ok(cached);

            var result = await backendClient.GetAsync<List<FriendResponse>>(Constants.API_FRIENDS);
            if (result.Success && result.Data != null)
                APICache.Set(APICache.KEY_FRIENDS_LIST, result.Data, 30f);

            return result;
        }

        /// <summary>
        /// DELETE /api/friends/{friendUserId}
        /// </summary>
        public async Task<ApiResult> RemoveFriend(long friendUserId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Removing friend...");
            try
            {
                string endpoint = string.Format(Constants.API_FRIENDS_REMOVE, friendUserId);
                var result = await backendClient.DeleteAsync<object>(endpoint);
                if (result.Success)
                {
                    APICache.Invalidate(APICache.KEY_FRIENDS_LIST);
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to remove friend");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"RemoveFriend error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // FRIEND REQUESTS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/friends/requests — send by username (primary flow: search friend by name).
        /// Server AddFriendRequest accepts { username } OR { userId }.
        /// </summary>
        public async Task<ApiResult<FriendRequestResponse>> SendFriendRequest(string username)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<FriendRequestResponse>.Error("Not authenticated");

            if (string.IsNullOrWhiteSpace(username))
                return ApiResult<FriendRequestResponse>.Error("Username cannot be empty");

            LoadingOverlay.Show("Sending friend request...");
            try
            {
                var body   = new SendFriendRequestRequest { username = username.Trim() };
                var result = await backendClient.PostAsync<FriendRequestResponse>(Constants.API_FRIENDS_SEND_REQUEST, body);
                if (result.Success)
                    APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_OUTGOING);
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to send request");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"SendFriendRequest error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<FriendRequestResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>
        /// POST /api/friends/requests — send by userId (alternative when userId is known).
        /// </summary>
        public async Task<ApiResult<FriendRequestResponse>> SendFriendRequest(long userId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<FriendRequestResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Sending friend request...");
            try
            {
                var body   = new SendFriendRequestRequest { userId = userId };
                var result = await backendClient.PostAsync<FriendRequestResponse>(Constants.API_FRIENDS_SEND_REQUEST, body);
                if (result.Success)
                    APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_OUTGOING);
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to send request");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"SendFriendRequest error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<FriendRequestResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>
        /// GET /api/friends/requests/incoming + /outgoing — combined into PendingRequestsResponse.
        /// </summary>
        public async Task<ApiResult<PendingRequestsResponse>> GetPendingRequests()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<PendingRequestsResponse>.Error("Not authenticated");

            var inResult  = await backendClient.GetAsync<List<FriendRequestResponse>>(Constants.API_FRIENDS_INCOMING_REQUESTS);
            var outResult = await backendClient.GetAsync<List<FriendRequestResponse>>(Constants.API_FRIENDS_OUTGOING_REQUESTS);

            var response = new PendingRequestsResponse
            {
                received = inResult.Success  ? inResult.Data  : new List<FriendRequestResponse>(),
                sent     = outResult.Success ? outResult.Data : new List<FriendRequestResponse>()
            };
            return ApiResult<PendingRequestsResponse>.Ok(response);
        }

        /// <summary>
        /// POST /api/friends/requests/{requestId}/accept
        /// </summary>
        public async Task<ApiResult<FriendResponse>> AcceptRequest(long requestId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<FriendResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Accepting friend request...");
            try
            {
                string endpoint = string.Format(Constants.API_FRIENDS_ACCEPT_REQUEST, requestId);
                var result = await backendClient.PostAsync<FriendResponse>(endpoint);
                if (result.Success)
                    APICache.InvalidateFriends();
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to accept request");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"AcceptRequest error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<FriendResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>
        /// POST /api/friends/requests/{requestId}/decline
        /// Server uses "decline" — maps to requestStatus "DECLINED".
        /// </summary>
        public async Task<ApiResult> DeclineRequest(long requestId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Declining request...");
            try
            {
                string endpoint = string.Format(Constants.API_FRIENDS_DECLINE_REQUEST, requestId);
                var result = await backendClient.PostAsync<object>(endpoint);
                if (result.Success)
                {
                    APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_INCOMING);
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to decline request");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"DeclineRequest error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>
        /// DELETE /api/friends/requests/{requestId} — cancel an outgoing request.
        /// No /cancel suffix — pure DELETE on the request resource.
        /// </summary>
        public async Task<ApiResult> CancelFriendRequest(long requestId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Cancelling request...");
            try
            {
                string endpoint = string.Format(Constants.API_FRIENDS_CANCEL_REQUEST, requestId);
                var result = await backendClient.DeleteAsync<object>(endpoint);
                if (result.Success)
                {
                    APICache.Invalidate(APICache.KEY_FRIENDS_REQUESTS_OUTGOING);
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to cancel request");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"CancelFriendRequest error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // BLOCKED USERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/friends/blocked — server returns List&lt;Long&gt; (user IDs only, no username).
        /// Builds BlockedUserResponse list with userId populated; username will be empty.
        /// </summary>
        public async Task<ApiResult<List<BlockedUserResponse>>> GetBlockedUsers()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<List<BlockedUserResponse>>.Error("Not authenticated");

            // Server: ApiResponse<List<Long>> — deserialize as a wrapper list
            var rawResult = await backendClient.GetAsync<BlockedUserIdListWrapper>(Constants.API_FRIENDS_BLOCKED);
            if (!rawResult.Success)
                return ApiResult<List<BlockedUserResponse>>.Error(rawResult.Message);

            var list = new List<BlockedUserResponse>();
            if (rawResult.Data?.ids != null)
            {
                foreach (long id in rawResult.Data.ids)
                    list.Add(new BlockedUserResponse { blockedUserId = id });
            }
            return ApiResult<List<BlockedUserResponse>>.Ok(list);
        }

        /// <summary>
        /// POST /api/friends/block/{targetUserId} — path param, no request body.
        /// </summary>
        public async Task<ApiResult> BlockUser(long targetUserId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Blocking user...");
            try
            {
                string endpoint = string.Format(Constants.API_FRIENDS_BLOCK, targetUserId);
                var result = await backendClient.PostAsync<object>(endpoint);
                if (result.Success)
                {
                    APICache.Invalidate(APICache.KEY_FRIENDS_LIST, APICache.KEY_FRIENDS_BLOCKED);
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to block user");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"BlockUser error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>
        /// DELETE /api/friends/block/{targetUserId} — path param.
        /// </summary>
        public async Task<ApiResult> UnblockUser(long targetUserId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Unblocking user...");
            try
            {
                string endpoint = string.Format(Constants.API_FRIENDS_UNBLOCK, targetUserId);
                var result = await backendClient.DeleteAsync<object>(endpoint);
                if (result.Success)
                {
                    APICache.Invalidate(APICache.KEY_FRIENDS_BLOCKED);
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to unblock user");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("FriendService", $"UnblockUser error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        // ── Helper DTO for blocked list deserialization ────────────────────────
        // Server returns ApiResponse<List<Long>> — Unity JsonUtility can't deserialize
        // a bare JSON array as the root "data" field, so we wrap it here.
        [Serializable]
        private class BlockedUserIdListWrapper
        {
            public List<long> ids;
        }
    }
}
