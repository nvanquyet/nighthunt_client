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
        public event Action OnForceLogout;
        public event Action OnSessionExpired;
        public event Action OnWebSocketDisconnected;
        public event Action<string> OnWebSocketError;

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
            // Wait for GameManager to be fully initialized
            yield return new WaitForEndOfFrame();
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
                ws.OnForceLogout += HandleForceLogout;
                ws.OnSessionExpired += HandleSessionExpired;
                ws.OnDisconnected += HandleWebSocketDisconnected;
                ws.OnError += HandleWebSocketError;
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
                ws.OnForceLogout -= HandleForceLogout;
                ws.OnSessionExpired -= HandleSessionExpired;
                ws.OnDisconnected -= HandleWebSocketDisconnected;
                ws.OnError -= HandleWebSocketError;
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
        private void HandleForceLogout() => OnForceLogout?.Invoke();
        private void HandleSessionExpired() => OnSessionExpired?.Invoke();
        private void HandleWebSocketDisconnected() => OnWebSocketDisconnected?.Invoke();
        private void HandleWebSocketError(string error) => OnWebSocketError?.Invoke(error);

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

