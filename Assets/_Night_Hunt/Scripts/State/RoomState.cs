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

        // Events
        public event Action<RoomResponse> OnRoomJoined;
        public event Action OnRoomLeft;
        public event Action<RoomResponse> OnRoomStateChanged;



        public void SetRoom(RoomResponse room)
        {
            bool wasInRoom = IsInRoom;
            bool isNewRoom = !wasInRoom || CurrentRoom?.roomId != room?.roomId;
            
            CurrentRoom = room;
            
            // Trigger events
            if (isNewRoom && room != null)
            {
                OnRoomJoined?.Invoke(room);
            }
            else if (room != null)
            {
                OnRoomStateChanged?.Invoke(room);
            }
        }

        public void ClearRoom()
        {
            if (IsInRoom)
            {
                CurrentRoom = null;
                ClearNetworkSession();
                OnRoomLeft?.Invoke();
            }
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

        /// <summary>Store dedicated server info when a Ranked match is found.</summary>
        public void SetDedicatedServer(string ip, ushort port, string matchId, string mapId = null)
        {
            CurrentGameMode = GameMode.Ranked_DS;
            DsIp = ip;
            DsPort = port;
            CurrentMatchId = matchId;
            DsMapId = mapId;
            IsHostPlayer = false;
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
        }
    }
}


