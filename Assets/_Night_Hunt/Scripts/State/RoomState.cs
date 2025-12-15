using System;
using NightHunt.Data.DTOs;
using UnityEngine;

namespace NightHunt.State
{
    public class RoomState : MonoBehaviour
    {
        public static RoomState Instance { get; private set; }

        public RoomResponse CurrentRoom { get; private set; }
        public bool IsInRoom => CurrentRoom != null;
        public long RoomId => CurrentRoom?.roomId ?? 0;
        public string RoomCode => CurrentRoom?.roomCode ?? "";
        public string Status => CurrentRoom?.status ?? "";
        public bool IsReady => CurrentRoom?.players?.Find(p => p.userId == (SessionState.Instance?.UserId ?? 0))?.isReady ?? false;

        // Events
        public event Action<RoomResponse> OnRoomJoined;
        public event Action OnRoomLeft;
        public event Action<RoomResponse> OnRoomStateChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

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
                OnRoomLeft?.Invoke();
            }
        }
    }

}


