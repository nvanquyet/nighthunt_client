using System.Collections.Generic;
using UnityEngine;

namespace _Night_Hunt.Scripts.NightHuntInput
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        private Dictionary<int, PlayerInputHandler> playerInputs = new Dictionary<int, PlayerInputHandler>();
        
        [Header("Settings")]
        [SerializeField] private float joystickDeadzone = 0.15f;
        [SerializeField] private float mouseSensitivity = 1f;
        [SerializeField] private bool invertYAxis = false;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Register a player's input handler
        /// Each player gets isolated input
        /// </summary>
        public void RegisterPlayer(int playerId, PlayerInputHandler handler)
        {
            if (playerInputs.ContainsKey(playerId))
            {
                Debug.LogWarning($"[Input] Player {playerId} already registered");
                return;
            }
            
            playerInputs[playerId] = handler;
            Debug.Log($"[Input] Registered player {playerId}");
        }

        public void UnregisterPlayer(int playerId)
        {
            if (playerInputs.Remove(playerId))
            {
                Debug.Log($"[Input] Unregistered player {playerId}");
            }
        }

        public PlayerInputHandler GetPlayerInput(int playerId)
        {
            return playerInputs.TryGetValue(playerId, out var handler) ? handler : null;
        }

        public float GetJoystickDeadzone() => joystickDeadzone;
        public float GetMouseSensitivity() => mouseSensitivity;
        public bool GetInvertYAxis() => invertYAxis;
    }

   
}