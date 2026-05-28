using System;
using System.Collections;
using System.Collections.Generic;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.State;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Core
{
    /// <summary>
    /// Centralized Event Bus for all game events
    /// Subscribe/Unsubscribe in one place, easier to manage and debug
    /// </summary>
    public class GameEventBus : SingletonPersistent<GameEventBus>
    {

        // WebSocket Events (from GameWebSocketService)
        public event Action<RoomResponse> OnRoomUpdated;
        public event Action<GameWebSocketService.PlayerJoinedEvent> OnPlayerJoined;
        public event Action<GameWebSocketService.PlayerLeftEvent> OnPlayerLeft;
        public event Action<GameWebSocketService.PlayerReadyEvent> OnPlayerReady;
        public event Action<GameWebSocketService.TeamChangedEvent> OnTeamChanged;
        public event Action<GameWebSocketService.RoomStatusChangedEvent> OnRoomStatusChanged;
        public event Action<GameWebSocketService.SwapRequestEvent> OnSwapRequest;
        public event Action<GameWebSocketService.SwapRequestStatusEvent> OnSwapRequestStatus;
        public event Action<GameWebSocketService.GameStartingEvent> OnGameStarting;
        public event Action<GameWebSocketService.RoomDisbandedEvent> OnRoomDisbanded;
        public event Action<GameWebSocketService.YouWereKickedEvent> OnYouWereKicked;
        public event Action<GameWebSocketService.MatchPresenceNoticeEvent> OnMatchPresenceNotice;
        public event Action OnForceLogout;
        public event Action OnSessionExpired;
        public event Action OnWebSocketConnected;
        public event Action OnWebSocketDisconnected;
        public event Action OnWebSocketReconnectFailed;
        public event Action<string> OnWebSocketError;
        public event Action<GameWebSocketService.MatchFoundEvent> OnMatchFound;
        public event Action<GameWebSocketService.MatchReadyEvent> OnMatchReady;
        public event Action<GameWebSocketService.DsReadyEvent> OnDsReady;
        public event Action<GameWebSocketService.MatchCancelledEvent> OnMatchCancelled;
        public event Action<GameWebSocketService.MatchEndedWsEvent> OnMatchEnded;
        public event Action<GameWebSocketService.FriendStatusChangedEvent> OnFriendStatusChanged;
        public event Action<GameWebSocketService.FriendRequestEvent> OnFriendRequestReceived;
        public event Action<GameWebSocketService.FriendRequestAcceptedEvent> OnFriendRequestAccepted;
        public event Action<GameWebSocketService.FriendRequestDeclinedEvent> OnFriendRequestDeclined;
        public event Action<GameWebSocketService.FriendRequestCancelledEvent> OnFriendRequestCancelled;
        public event Action<GameWebSocketService.FriendRemovedEvent> OnFriendRemoved;
        public event Action<GameWebSocketService.PartyInvitationEvent> OnPartyInvitationReceived;
        public event Action<GameWebSocketService.PartyInvitationResponseEvent> OnPartyInvitationDeclined;
        public event Action<GameWebSocketService.PartyInvitationResponseEvent> OnPartyInvitationCancelled;
        public event Action<GameWebSocketService.PartyInvitationResponseEvent> OnPartyInvitationExpired;
        public event Action<GameWebSocketService.PartyMemberJoinedEvent> OnPartyMemberJoined;
        public event Action<GameWebSocketService.PartyMemberLeftEvent> OnPartyMemberLeft;
        public event Action<GameWebSocketService.PartyMemberKickedEvent> OnPartyMemberKicked;
        public event Action<GameWebSocketService.PartyDisbandedEvent> OnPartyDisbanded;
        public event Action<GameWebSocketService.PartyHostChangedEvent> OnPartyHostChanged;
        public event Action<GameWebSocketService.PartyStatusChangedEvent> OnPartyStatusChanged;

        // Application Lifecycle Events
        public event Action OnAppFocusLost;
        public event Action OnAppFocusGained;
        public event Action OnAppPaused;
        public event Action OnAppResumed;

        // Room State Events
        public event Action<RoomResponse> OnRoomJoined;
        public event Action OnRoomLeft;
        public event Action<RoomResponse> OnRoomStateChanged;

        // Session State Events
        public event Action OnSessionStarted;
        public event Action OnSessionEnded;
        public event Action OnUserLoggedIn;
        public event Action OnUserLoggedOut;

        protected override void OnSingletonAwake()
        {
            // Delay subscription to ensure GameManager is initialized first
            StartCoroutine(DelayedSubscribe());
        }

        private System.Collections.IEnumerator DelayedSubscribe()
        {
            // Wait until GameManager and GameWebSocket are both ready (up to 15 seconds)
            float timeout = 15f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
                    break;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SubscribeToServices();
        }

        protected override void OnDestroy()
        {
            UnsubscribeFromServices();
            base.OnDestroy();
        }

        /// <summary>
        /// Subscribe to all service events
        /// </summary>
        private void SubscribeToServices()
        {
            // Subscribe to GameWebSocketService events
            if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
            {
                var ws = GameManager.Instance.GameWebSocket;
                ws.OnRoomUpdated += HandleRoomUpdated;
                ws.OnPlayerJoined += HandlePlayerJoined;
                ws.OnPlayerLeft += HandlePlayerLeft;
                ws.OnPlayerReady += HandlePlayerReady;
                ws.OnTeamChanged += HandleTeamChanged;
                ws.OnRoomStatusChanged += HandleRoomStatusChanged;
                ws.OnSwapRequest += HandleSwapRequest;
                ws.OnSwapRequestStatus += HandleSwapRequestStatus;
                ws.OnGameStarting += HandleGameStarting;
                ws.OnRoomDisbanded += HandleRoomDisbanded;
                ws.OnYouWereKicked += HandleYouWereKicked;
                ws.OnMatchPresenceNotice += HandleMatchPresenceNotice;
                ws.OnForceLogout += HandleForceLogout;
                ws.OnSessionExpired += HandleSessionExpired;
                ws.OnConnected += HandleWebSocketConnected;
                ws.OnDisconnected += HandleWebSocketDisconnected;
                ws.OnReconnectFailed += HandleWebSocketReconnectFailed;
                ws.OnError += HandleWebSocketError;
                ws.OnMatchFound += HandleMatchFound;
                ws.OnMatchReady += HandleMatchReady;
                ws.OnDsReady += HandleDsReady;
                ws.OnMatchCancelled += HandleMatchCancelled;
                ws.OnMatchEnded += HandleMatchEnded;
                ws.OnFriendStatusChanged += HandleFriendStatusChanged;
                ws.OnFriendRequestReceived += HandleFriendRequestReceived;
                ws.OnFriendRequestAccepted += HandleFriendRequestAccepted;
                ws.OnFriendRequestDeclined += HandleFriendRequestDeclined;
                ws.OnFriendRequestCancelled += HandleFriendRequestCancelled;
                ws.OnFriendRemoved += HandleFriendRemoved;
                ws.OnPartyInvitationReceived += HandlePartyInvitationReceived;
                ws.OnPartyInvitationDeclined += HandlePartyInvitationDeclined;
                ws.OnPartyInvitationCancelled += HandlePartyInvitationCancelled;
                ws.OnPartyInvitationExpired += HandlePartyInvitationExpired;
                ws.OnPartyMemberJoined += HandlePartyMemberJoined;
                ws.OnPartyMemberLeft += HandlePartyMemberLeft;
                ws.OnPartyMemberKicked += HandlePartyMemberKicked;
                ws.OnPartyDisbanded += HandlePartyDisbanded;
                ws.OnPartyHostChanged += HandlePartyHostChanged;
                ws.OnPartyStatusChanged += HandlePartyStatusChanged;
            }

            // Subscribe to GameManager lifecycle events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnAppFocusLost += HandleAppFocusLost;
                GameManager.Instance.OnAppFocusGained += HandleAppFocusGained;
                GameManager.Instance.OnAppPaused += HandleAppPaused;
                GameManager.Instance.OnAppResumed += HandleAppResumed;
            }

            // Subscribe to RoomState changes
            if (RoomState.Instance != null)
            {
                RoomState.Instance.OnRoomJoined += HandleRoomJoined;
                RoomState.Instance.OnRoomLeft += HandleRoomLeft;
                RoomState.Instance.OnRoomStateChanged += HandleRoomStateChanged;
            }

            // Subscribe to SessionState changes
            if (SessionState.Instance != null)
            {
                SessionState.Instance.OnSessionStarted += HandleSessionStarted;
                SessionState.Instance.OnSessionEnded += HandleSessionEnded;
                SessionState.Instance.OnUserLoggedIn += HandleUserLoggedIn;
                SessionState.Instance.OnUserLoggedOut += HandleUserLoggedOut;
            }
        }

        /// <summary>
        /// Unsubscribe from all service events
        /// </summary>
        private void UnsubscribeFromServices()
        {
            if (GameManager.Instance != null && GameManager.Instance.GameWebSocket != null)
            {
                var ws = GameManager.Instance.GameWebSocket;
                ws.OnRoomUpdated -= HandleRoomUpdated;
                ws.OnPlayerJoined -= HandlePlayerJoined;
                ws.OnPlayerLeft -= HandlePlayerLeft;
                ws.OnPlayerReady -= HandlePlayerReady;
                ws.OnTeamChanged -= HandleTeamChanged;
                ws.OnRoomStatusChanged -= HandleRoomStatusChanged;
                ws.OnSwapRequest -= HandleSwapRequest;
                ws.OnSwapRequestStatus -= HandleSwapRequestStatus;
                ws.OnGameStarting -= HandleGameStarting;
                ws.OnRoomDisbanded -= HandleRoomDisbanded;
                ws.OnYouWereKicked -= HandleYouWereKicked;
                ws.OnMatchPresenceNotice -= HandleMatchPresenceNotice;
                ws.OnForceLogout -= HandleForceLogout;
                ws.OnSessionExpired -= HandleSessionExpired;
                ws.OnConnected -= HandleWebSocketConnected;
                ws.OnDisconnected -= HandleWebSocketDisconnected;
                ws.OnReconnectFailed -= HandleWebSocketReconnectFailed;
                ws.OnError -= HandleWebSocketError;
                ws.OnMatchFound -= HandleMatchFound;
                ws.OnMatchReady -= HandleMatchReady;
                ws.OnDsReady -= HandleDsReady;
                ws.OnMatchCancelled -= HandleMatchCancelled;
                ws.OnMatchEnded -= HandleMatchEnded;
                ws.OnFriendStatusChanged -= HandleFriendStatusChanged;
                ws.OnFriendRequestReceived -= HandleFriendRequestReceived;
                ws.OnFriendRequestAccepted -= HandleFriendRequestAccepted;
                ws.OnFriendRequestDeclined -= HandleFriendRequestDeclined;
                ws.OnFriendRequestCancelled -= HandleFriendRequestCancelled;
                ws.OnFriendRemoved -= HandleFriendRemoved;
                ws.OnPartyInvitationReceived -= HandlePartyInvitationReceived;
                ws.OnPartyInvitationDeclined -= HandlePartyInvitationDeclined;
                ws.OnPartyInvitationCancelled -= HandlePartyInvitationCancelled;
                ws.OnPartyInvitationExpired -= HandlePartyInvitationExpired;
                ws.OnPartyMemberJoined -= HandlePartyMemberJoined;
                ws.OnPartyMemberLeft -= HandlePartyMemberLeft;
                ws.OnPartyMemberKicked -= HandlePartyMemberKicked;
                ws.OnPartyDisbanded -= HandlePartyDisbanded;
                ws.OnPartyHostChanged -= HandlePartyHostChanged;
                ws.OnPartyStatusChanged -= HandlePartyStatusChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnAppFocusLost -= HandleAppFocusLost;
                GameManager.Instance.OnAppFocusGained -= HandleAppFocusGained;
                GameManager.Instance.OnAppPaused -= HandleAppPaused;
                GameManager.Instance.OnAppResumed -= HandleAppResumed;
            }

            if (RoomState.Instance != null)
            {
                RoomState.Instance.OnRoomJoined -= HandleRoomJoined;
                RoomState.Instance.OnRoomLeft -= HandleRoomLeft;
                RoomState.Instance.OnRoomStateChanged -= HandleRoomStateChanged;
            }

            if (SessionState.Instance != null)
            {
                SessionState.Instance.OnSessionStarted -= HandleSessionStarted;
                SessionState.Instance.OnSessionEnded -= HandleSessionEnded;
                SessionState.Instance.OnUserLoggedIn -= HandleUserLoggedIn;
                SessionState.Instance.OnUserLoggedOut -= HandleUserLoggedOut;
            }
        }

        // WebSocket Event Handlers
        private void HandleRoomUpdated(RoomResponse room) => OnRoomUpdated?.Invoke(room);
        private void HandlePlayerJoined(GameWebSocketService.PlayerJoinedEvent evt) => OnPlayerJoined?.Invoke(evt);
        private void HandlePlayerLeft(GameWebSocketService.PlayerLeftEvent evt) => OnPlayerLeft?.Invoke(evt);
        private void HandlePlayerReady(GameWebSocketService.PlayerReadyEvent evt) => OnPlayerReady?.Invoke(evt);
        private void HandleTeamChanged(GameWebSocketService.TeamChangedEvent evt) => OnTeamChanged?.Invoke(evt);
        private void HandleRoomStatusChanged(GameWebSocketService.RoomStatusChangedEvent evt) => OnRoomStatusChanged?.Invoke(evt);
        private void HandleSwapRequest(GameWebSocketService.SwapRequestEvent evt) => OnSwapRequest?.Invoke(evt);
        private void HandleSwapRequestStatus(GameWebSocketService.SwapRequestStatusEvent evt) => OnSwapRequestStatus?.Invoke(evt);
        private void HandleGameStarting(GameWebSocketService.GameStartingEvent evt) => OnGameStarting?.Invoke(evt);
        private void HandleRoomDisbanded(GameWebSocketService.RoomDisbandedEvent evt) => OnRoomDisbanded?.Invoke(evt);
        private void HandleYouWereKicked(GameWebSocketService.YouWereKickedEvent evt) => OnYouWereKicked?.Invoke(evt);
        private void HandleMatchPresenceNotice(GameWebSocketService.MatchPresenceNoticeEvent evt) => OnMatchPresenceNotice?.Invoke(evt);
        private void HandleForceLogout() => OnForceLogout?.Invoke();
        private void HandleSessionExpired() => OnSessionExpired?.Invoke();
        private void HandleWebSocketConnected() => OnWebSocketConnected?.Invoke();
        private void HandleWebSocketDisconnected() => OnWebSocketDisconnected?.Invoke();
        private void HandleWebSocketReconnectFailed() => OnWebSocketReconnectFailed?.Invoke();
        private void HandleWebSocketError(string error) => OnWebSocketError?.Invoke(error);
        private void HandleMatchFound(GameWebSocketService.MatchFoundEvent evt) => OnMatchFound?.Invoke(evt);
        private void HandleMatchReady(GameWebSocketService.MatchReadyEvent evt) => OnMatchReady?.Invoke(evt);
        private void HandleDsReady(GameWebSocketService.DsReadyEvent evt) => OnDsReady?.Invoke(evt);
        private void HandleMatchCancelled(GameWebSocketService.MatchCancelledEvent evt) => OnMatchCancelled?.Invoke(evt);
        private void HandleMatchEnded(GameWebSocketService.MatchEndedWsEvent evt) => OnMatchEnded?.Invoke(evt);
        private void HandleFriendStatusChanged(GameWebSocketService.FriendStatusChangedEvent evt) => OnFriendStatusChanged?.Invoke(evt);
        private void HandleFriendRequestReceived(GameWebSocketService.FriendRequestEvent evt) => OnFriendRequestReceived?.Invoke(evt);
        private void HandleFriendRequestAccepted(GameWebSocketService.FriendRequestAcceptedEvent evt) => OnFriendRequestAccepted?.Invoke(evt);
        private void HandleFriendRequestDeclined(GameWebSocketService.FriendRequestDeclinedEvent evt) => OnFriendRequestDeclined?.Invoke(evt);
        private void HandleFriendRequestCancelled(GameWebSocketService.FriendRequestCancelledEvent evt) => OnFriendRequestCancelled?.Invoke(evt);
        private void HandleFriendRemoved(GameWebSocketService.FriendRemovedEvent evt) => OnFriendRemoved?.Invoke(evt);
        private void HandlePartyInvitationReceived(GameWebSocketService.PartyInvitationEvent evt) => OnPartyInvitationReceived?.Invoke(evt);
        private void HandlePartyInvitationDeclined(GameWebSocketService.PartyInvitationResponseEvent evt) => OnPartyInvitationDeclined?.Invoke(evt);
        private void HandlePartyInvitationCancelled(GameWebSocketService.PartyInvitationResponseEvent evt) => OnPartyInvitationCancelled?.Invoke(evt);
        private void HandlePartyInvitationExpired(GameWebSocketService.PartyInvitationResponseEvent evt) => OnPartyInvitationExpired?.Invoke(evt);
        private void HandlePartyMemberJoined(GameWebSocketService.PartyMemberJoinedEvent evt)
        {
            OnPartyMemberJoined?.Invoke(evt);
            NightHunt.State.PartyState.Instance?.InvalidateAndScheduleRefresh();
        }

        private void HandlePartyMemberLeft(GameWebSocketService.PartyMemberLeftEvent evt)
        {
            OnPartyMemberLeft?.Invoke(evt);
            long selfId = NightHunt.State.SessionState.Instance?.UserId ?? 0L;
            if (evt.userId == selfId)
                NightHunt.State.PartyState.Instance?.ClearParty();
            else
                NightHunt.State.PartyState.Instance?.InvalidateAndScheduleRefresh();
        }

        private void HandlePartyMemberKicked(GameWebSocketService.PartyMemberKickedEvent evt)
        {
            OnPartyMemberKicked?.Invoke(evt);
            long selfId = NightHunt.State.SessionState.Instance?.UserId ?? 0L;
            if (evt.kickedUserId == selfId)
                NightHunt.State.PartyState.Instance?.ClearParty();
            else
                NightHunt.State.PartyState.Instance?.InvalidateAndScheduleRefresh();
        }

        private void HandlePartyDisbanded(GameWebSocketService.PartyDisbandedEvent evt)
        {
            OnPartyDisbanded?.Invoke(evt);
            NightHunt.State.PartyState.Instance?.ClearParty();
        }

        private void HandlePartyHostChanged(GameWebSocketService.PartyHostChangedEvent evt)
        {
            OnPartyHostChanged?.Invoke(evt);
            NightHunt.State.PartyState.Instance?.InvalidateAndScheduleRefresh();
        }

        private void HandlePartyStatusChanged(GameWebSocketService.PartyStatusChangedEvent evt)
        {
            OnPartyStatusChanged?.Invoke(evt);
            NightHunt.State.PartyState.Instance?.InvalidateAndScheduleRefresh();
        }

        // Lifecycle Event Handlers
        private void HandleAppFocusLost() => OnAppFocusLost?.Invoke();
        private void HandleAppFocusGained() => OnAppFocusGained?.Invoke();
        private void HandleAppPaused() => OnAppPaused?.Invoke();
        private void HandleAppResumed() => OnAppResumed?.Invoke();

        // RoomState Event Handlers
        private void HandleRoomJoined(RoomResponse room) => OnRoomJoined?.Invoke(room);
        private void HandleRoomLeft() => OnRoomLeft?.Invoke();
        private void HandleRoomStateChanged(RoomResponse room) => OnRoomStateChanged?.Invoke(room);

        // SessionState Event Handlers
        private void HandleSessionStarted() => OnSessionStarted?.Invoke();
        private void HandleSessionEnded() => OnSessionEnded?.Invoke();
        private void HandleUserLoggedIn() => OnUserLoggedIn?.Invoke();
        private void HandleUserLoggedOut() => OnUserLoggedOut?.Invoke();
    }
}

