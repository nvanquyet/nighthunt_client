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

namespace NightHunt.Services.Party
{
    /// <summary>
    /// Party System Service — manages party creation, invites, matchmaking, and party custom mode.
    ///
    /// API summary (all paths verified against server PartyController):
    ///   POST   /api/party/create                          → create (user becomes host)
    ///   GET    /api/party/current                         → get current party
    ///   POST   /api/party/leave                           → leave (host leave = disbands if solo, else transfers)
    ///   POST   /api/party/disband                         → host explicitly disbands
    ///   POST   /api/party/invite                          → invite {inviteeUserId}
    ///   GET    /api/party/invitations                     → list pending invitations
    ///   POST   /api/party/invitations/{id}/accept         → accept invite
    ///   POST   /api/party/invitations/{id}/decline        → decline invite
    ///   DELETE /api/party/invite/{inviteId}               → cancel a sent invite
    ///   POST   /api/party/kick/{kickedUserId}             → kick member (path param)
    ///   POST   /api/party/queue                           → queue party {gameMode, allowFill}
    ///   POST   /api/party/cancel-queue                    → cancel queue
    ///   POST   /api/party/join-room                       → join custom room with party {roomCode, password}
    ///
    /// NOTE: Transfer-leader endpoint does NOT exist on server yet.
    ///       Feature is not exposed to the client UI for now.
    /// </summary>
    public class PartyService : MonoBehaviour
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
        // PARTY MANAGEMENT
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST /api/party/create — becomes host.</summary>
        public async Task<ApiResult<PartyResponse>> CreateParty()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<PartyResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Creating party...");
            try
            {
                var result = await backendClient.PostAsync<PartyResponse>(Constants.API_PARTY_CREATE);
                if (result.Success)
                    APICache.Invalidate(APICache.KEY_PARTY_STATE);
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to create party");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"CreateParty error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<PartyResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>
        /// GET /api/party/current — get current party info.
        /// Cached 15 s, invalidated by WS party events.
        /// Returns Success=false (not error) if player is not in a party (204 / empty).
        /// </summary>
        public async Task<ApiResult<PartyResponse>> GetParty()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<PartyResponse>.Error("Not authenticated");

            if (APICache.TryGet(APICache.KEY_PARTY_STATE, out PartyResponse cached))
                return ApiResult<PartyResponse>.Ok(cached);

            var result = await backendClient.GetAsync<PartyResponse>(Constants.API_PARTY);
            if (result.Success && result.Data != null)
                APICache.Set(APICache.KEY_PARTY_STATE, result.Data, 15f);

            return result;
        }

        /// <summary>
        /// POST /api/party/leave — leave party.
        /// If host leaves and there are members, server transfers host to next member by joinOrder.
        /// If host leaves and party is empty, party is disbanded.
        /// </summary>
        public async Task<ApiResult> LeaveParty()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Leaving party...");
            try
            {
                var result = await backendClient.PostAsync<object>(Constants.API_PARTY_LEAVE);
                if (result.Success)
                {
                    APICache.InvalidateParty();
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to leave party");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"LeaveParty error: {ex.Message}", ex);
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
        /// POST /api/party/disband — host explicitly disbands the whole party.
        /// All members are removed and WS party_disbanded event is broadcast.
        /// </summary>
        public async Task<ApiResult> DisbandParty()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Disbanding party...");
            try
            {
                var result = await backendClient.PostAsync<object>(Constants.API_PARTY_DISBAND);
                if (result.Success)
                {
                    APICache.InvalidateParty();
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to disband party");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"DisbandParty error: {ex.Message}", ex);
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
        // PARTY INVITES
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/party/invite — invite a friend (host only).
        /// Invitation expires in 30 s (server-enforced).
        /// </summary>
        public async Task<ApiResult<PartyInviteResponse>> InviteToParty(long inviteeUserId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<PartyInviteResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Sending invite...");
            try
            {
                // Field name: inviteeUserId (server InviteToPartyRequest)
                var body   = new InviteToPartyRequest { inviteeUserId = inviteeUserId };
                var result = await backendClient.PostAsync<PartyInviteResponse>(Constants.API_PARTY_INVITE, body);
                if (result.Success)
                    APICache.Invalidate(APICache.KEY_PARTY_INVITATIONS);
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to send invite");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"InviteToParty error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<PartyInviteResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>GET /api/party/invitations — pending invitations for current user.</summary>
        public async Task<ApiResult<List<PartyInviteResponse>>> GetPendingInvitations()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<List<PartyInviteResponse>>.Error("Not authenticated");

            return await backendClient.GetAsync<List<PartyInviteResponse>>(Constants.API_PARTY_INVITATIONS);
        }

        /// <summary>POST /api/party/invitations/{invitationId}/accept</summary>
        public async Task<ApiResult<PartyResponse>> AcceptInvitation(long invitationId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<PartyResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Joining party...");
            try
            {
                string endpoint = string.Format(Constants.API_PARTY_ACCEPT_INVITATION, invitationId);
                var result = await backendClient.PostAsync<PartyResponse>(endpoint);
                if (result.Success)
                    APICache.InvalidateParty();
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to join party");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"AcceptInvitation error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<PartyResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>POST /api/party/invitations/{invitationId}/decline</summary>
        public async Task<ApiResult> DeclineInvitation(long invitationId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Declining invitation...");
            try
            {
                string endpoint = string.Format(Constants.API_PARTY_DECLINE_INVITATION, invitationId);
                var result = await backendClient.PostAsync<object>(endpoint);
                if (result.Success)
                {
                    APICache.Invalidate(APICache.KEY_PARTY_INVITATIONS);
                    return ApiResult.Ok();
                }
                LoadingOverlay.ShowError(result.Message ?? "Failed to decline invitation");
                return ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"DeclineInvitation error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>DELETE /api/party/invite/{inviteId} — cancel a sent invite (host).</summary>
        public async Task<ApiResult> CancelInvite(long inviteId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            string endpoint = string.Format(Constants.API_PARTY_CANCEL_INVITE, inviteId);
            var result = await backendClient.DeleteAsync<object>(endpoint);
            if (result.Success) APICache.Invalidate(APICache.KEY_PARTY_INVITATIONS);
            return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
        }

        // ══════════════════════════════════════════════════════════════════════
        // PARTY ACTIONS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/party/kick/{kickedUserId} — path param, host only.
        /// </summary>
        public async Task<ApiResult> KickMember(long kickedUserId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Kicking member...");
            try
            {
                // Server: POST /party/kick/{kickedUserId} — path param, no body
                string endpoint = string.Format(Constants.API_PARTY_KICK, kickedUserId);
                var result = await backendClient.PostAsync<object>(endpoint);
                if (result.Success)
                    APICache.Invalidate(APICache.KEY_PARTY_STATE);
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to kick member");
                return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"KickMember error: {ex.Message}", ex);
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
        /// POST /api/party/transfer-leader — transfer host role to another member (current host only).
        /// </summary>
        public async Task<ApiResult<PartyResponse>> TransferLeader(long newLeaderId)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<PartyResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Transferring leader...");
            try
            {
                var body   = new { newLeaderId };
                var result = await backendClient.PostAsync<PartyResponse>(Constants.API_PARTY_TRANSFER_LEADER, body);
                if (result.Success)
                    APICache.Invalidate(APICache.KEY_PARTY_STATE);
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to transfer leader");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"TransferLeader error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<PartyResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(500);
                LoadingOverlay.Hide();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PARTY MATCHMAKING
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/party/queue — queue party for ranked (host only).
        /// allowFill=true → fill empty slots with solo players.
        /// allowFill=false → only match against full party of same size.
        /// </summary>
        public async Task<ApiResult> QueueParty(string gameMode, bool allowFill = true, string mapId = null)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Joining queue...");
            try
            {
                var body   = new PartyRankedQueueRequest { gameMode = gameMode, allowFill = allowFill, mapId = mapId };
                var result = await backendClient.PostAsync<object>(Constants.API_PARTY_RANKED_QUEUE, body);
                if (result.Success)
                    APICache.InvalidateParty();
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to join queue");
                return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"QueueParty error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }

        /// <summary>POST /api/party/cancel-queue — any party member can cancel.</summary>
        public async Task<ApiResult> CancelQueue()
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult.Error("Not authenticated");

            LoadingOverlay.Show("Leaving queue...");
            try
            {
                var result = await backendClient.PostAsync<object>(Constants.API_PARTY_RANKED_CANCEL);
                if (result.Success)
                    APICache.InvalidateParty();
                else
                    LoadingOverlay.ShowError(result.Message ?? "Failed to cancel queue");
                return result.Success ? ApiResult.Ok() : ApiResult.Error(result.Message);
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"CancelQueue error: {ex.Message}", ex);
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
        // PARTY CUSTOM LOBBY
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/party/join-room — host brings whole party into party custom mode.
        /// All members are automatically added to the room.
        /// </summary>
        public async Task<ApiResult<RoomResponse>> JoinRoomWithParty(string roomCode, string password = null)
        {
            if (sessionState == null || !sessionState.IsAuthenticated)
                return ApiResult<RoomResponse>.Error("Not authenticated");

            LoadingOverlay.Show("Joining room with party...");
            try
            {
                var body   = new JoinRoomWithPartyRequest { roomCode = roomCode, password = password };
                var result = await backendClient.PostAsync<RoomResponse>(Constants.API_PARTY_JOIN_ROOM, body);
                if (!result.Success)
                    LoadingOverlay.ShowError(result.Message ?? "Failed to join room");
                return result;
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PartyService", $"JoinRoomWithParty error: {ex.Message}", ex);
                LoadingOverlay.ShowError("Network error");
                return ApiResult<RoomResponse>.Error(ex.Message);
            }
            finally
            {
                await Task.Delay(1000);
                LoadingOverlay.Hide();
            }
        }
    }
}
