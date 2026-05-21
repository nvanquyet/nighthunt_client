using System;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Networking;
using UnityEngine;

namespace NightHunt.State
{
    public class RoomState : SingletonPersistent<RoomState>
    {

        public RoomResponse CurrentRoom { get; private set; }
        public bool IsInRoom => CurrentRoom != null;
        public long RoomId => CurrentRoom?.roomId ?? 0;
        public string RoomCode => CurrentRoom?.roomCode ?? "";
        public string Status => CurrentRoom?.status ?? "";
        public bool IsReady => CurrentRoom?.players?.Find(p => p.userId == (SessionState.Instance?.UserId ?? 0))?.isReady ?? false;
        /// <summary>Number of players currently in the room (0 when not in a room).</summary>
        public int PlayerCount => CurrentRoom?.players?.Count ?? 0;

        // ── Game mode & network session ───────────────────────────────────────
        /// <summary>Whether this session uses Custom_Relay or Ranked_DS.</summary>
        public GameMode CurrentGameMode { get; private set; } = GameMode.None;

        /// <summary>True when the local player is the FishNet Host (Custom_Relay only).</summary>
        public bool IsHostPlayer { get; private set; }

        // Relay info (Custom_Relay)
        public string RelaySessionId { get; private set; }
        public string RelayIp { get; private set; }
        public ushort RelayPort { get; private set; }

        // Dedicated server info (Ranked_DS)
        public string DsIp { get; private set; }
        public ushort DsPort { get; private set; }
        public string DsMapId { get; private set; }

        // Match tracking
        public string CurrentMatchId { get; private set; }

        /// <summary>
        /// Players from the match_ready WS payload (Phase 3).
        /// Populated before DS connects so MatchLoadingOverlay can show all player cards.
        /// May be null if backend doesn't yet include players[] in match_ready.
        /// </summary>
        public System.Collections.Generic.List<NightHunt.Services.Game.GameWebSocketService.MatchReadyPlayerEntry> MatchReadyPlayers { get; private set; }

        // Events
        public event Action<RoomResponse> OnRoomJoined;
        public event Action OnRoomLeft;
        public event Action<RoomResponse> OnRoomStateChanged;



        public void SetRoom(RoomResponse room)
        {
            if (room == null)
            {
                if (IsInRoom)
                    ClearRoom();
                return;
            }

            if (room.roomId <= 0)
            {
                Debug.LogWarning($"[RoomState] Ignoring invalid room payload: roomId={room.roomId}, status={room.status ?? ""}, players={room.players?.Count ?? -1}");
                return;
            }

            // Terminal rooms must never become the active room.
            // If the payload is for our current room, clear local state (room is gone).
            // Otherwise simply discard — stale GET responses for old rooms must not overwrite cleared state.
            bool isTerminal = room.status != null && (
                room.status.Equals("CLOSED",    StringComparison.OrdinalIgnoreCase) ||
                room.status.Equals("FINISHED",  StringComparison.OrdinalIgnoreCase) ||
                room.status.Equals("DISBANDED", StringComparison.OrdinalIgnoreCase));

            if (isTerminal)
            {
                if (IsInRoom && CurrentRoom?.roomId == room.roomId)
                {
                    Debug.Log($"[RoomState] SetRoom: room {room.roomId} is terminal ({room.status}) — clearing local state.");
                    ClearRoom();
                }
                else
                {
                    Debug.Log($"[RoomState] SetRoom: ignoring terminal room payload roomId={room.roomId} status={room.status} (not our current room or already cleared).");
                }
                return;
            }

            bool wasInRoom = IsInRoom;
            bool isNewRoom = !wasInRoom || CurrentRoom?.roomId != room.roomId;
            
            CurrentRoom = room;
            
            // Trigger events
            if (isNewRoom)
            {
                OnRoomJoined?.Invoke(room);
            }
            else
            {
                OnRoomStateChanged?.Invoke(room);
            }
        }

        public void ClearRoom()
        {
            bool wasInRoom = IsInRoom;
            CurrentRoom = null;
            ClearNetworkSession();
            NetworkGameManager.ResetConnectionFlags();
            if (wasInRoom) OnRoomLeft?.Invoke();
        }

        // ── Network session helpers ───────────────────────────────────────────

        /// <summary>Store relay session info when a Custom match is about to start.</summary>
        public void SetRelaySession(string sessionId, string ip, ushort port, bool isHost)
        {
            CurrentGameMode = GameMode.Custom_Relay;
            RelaySessionId = sessionId;
            RelayIp = ip;
            RelayPort = port;
            IsHostPlayer = isHost;
        }

        /// <summary>
        /// Called on match_ready: stores match/map info but NOT DS ip:port.
        /// For Ranked_DS: client must wait for ds_ready before connecting.
        /// For Custom_Relay: mode is already set by game_starting — do NOT overwrite.
        /// </summary>
        public void SetMatchReady(string matchId, string mapId, string gameModeStr = null)
        {
            // Detect relay from game_starting (already set) OR from gameMode string in match_ready.
            bool isCustom = CurrentGameMode == GameMode.Custom_Relay
                || (!string.IsNullOrEmpty(gameModeStr)
                    && gameModeStr.IndexOf("custom", System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isCustom)
            {
                CurrentGameMode = GameMode.Ranked_DS;
                IsHostPlayer    = false;  // DS games have no host player concept
            }
            // else: preserve Custom_Relay + IsHostPlayer set by game_starting

            CurrentMatchId = matchId;
            DsMapId        = mapId;
        }

        /// <summary>
        /// Store players received in the match_ready WS payload (Phase 3).
        /// MatchLoadingOverlay reads this to build player cards before DS connects.
        /// </summary>
        public void SetMatchReadyPlayers(System.Collections.Generic.IEnumerable<NightHunt.Services.Game.GameWebSocketService.MatchReadyPlayerEntry> players)
        {
            MatchReadyPlayers = players != null
                ? new System.Collections.Generic.List<NightHunt.Services.Game.GameWebSocketService.MatchReadyPlayerEntry>(players)
                : null;
        }

        /// <summary>
        /// Called on ds_ready: stores DS ip:port. Safe to connect after this.
        /// </summary>
        public void SetDedicatedServer(string ip, ushort port, string matchId, string mapId = null)
        {
            CurrentGameMode = GameMode.Ranked_DS;
            DsIp            = ip;
            DsPort          = port;
            CurrentMatchId  = matchId;
            DsMapId         = mapId ?? DsMapId;
            IsHostPlayer    = false;
        }

        /// <summary>Clear all network session data (called on match end / leave).</summary>
        public void ClearNetworkSession()
        {
            CurrentGameMode = GameMode.None;
            IsHostPlayer = false;
            RelaySessionId = null;
            RelayIp = null;
            RelayPort = 0;
            DsIp = null;
            DsPort = 0;
            DsMapId = null;
            CurrentMatchId = null;
            MatchReadyPlayers = null;
        }
    }
}


