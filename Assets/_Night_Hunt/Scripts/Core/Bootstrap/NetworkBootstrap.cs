using NightHunt.Core;
using NightHunt.Services.Room;
using UnityEngine;

namespace _Night_Hunt.Core.Bootstrap
{
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private RoomService roomService;

        private void Awake()
        {
            // Try to get from GameManager first
            if (GameManager.Instance != null)
            {
                if (roomService == null)
                {
                    roomService = GameManager.Instance.RoomService;
                }
            }

            // Fallback: Find in scene
            if (roomService == null)
            {
#if UNITY_2023_1_OR_NEWER
                roomService = FindFirstObjectByType<RoomService>();
#else
                roomService = FindObjectOfType<RoomService>();
#endif
            }
        }

        // Headless flow disabled; keep stub to avoid null references
        public async void ConnectToRoom()
        {
            Debug.Log("Headless disabled - ConnectToRoom noop");
            await System.Threading.Tasks.Task.CompletedTask;
        }

        public async void Reconnect()
        {
            Debug.Log("Headless disabled - Reconnect noop");
            await System.Threading.Tasks.Task.CompletedTask;
        }

        public void Disconnect()
        {
            Debug.Log("Headless disabled - Disconnect noop");
        }

        public bool IsConnected()
        {
            Debug.Log("Headless disabled - IsConnected always returns false");
            return false;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // No-op as headless is disabled
        }
    }
}