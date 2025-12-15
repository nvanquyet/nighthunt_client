using NightHunt.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Waiting View - Lobby/Waiting room scene
    /// Shows player movement demo and lobby view (can be toggled)
    /// </summary>
    public class WaitingView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button lobbyToggleButton;
        [SerializeField] private LobbyView lobbyView;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Player")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform spawnPoint;

        private bool isLobbyVisible = false;

        private void Awake()
        {
            if (lobbyToggleButton != null)
                lobbyToggleButton.onClick.AddListener(OnLobbyToggleClicked);

            // Hide lobby view initially (or show it by default - adjust as needed)
            if (lobbyView != null)
            {
                // Option 1: Show by default
                lobbyView.Show();
                isLobbyVisible = true;
                
                // Option 2: Hide by default (uncomment if preferred)
                // lobbyView.Hide();
                // isLobbyVisible = false;
            }
        }

        private void Start()
        {
            // Spawn local player
            SpawnPlayer();

            // Update room info
            UpdateRoomInfo();
        }

        private void SpawnPlayer()
        {
            if (playerPrefab != null && spawnPoint != null)
            {
                GameObject playerObj = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
                // Player movement is handled by the prefab's own components
            }
        }

        private void UpdateRoomInfo()
        {
            var roomState = GameManager.Instance?.RoomState;
            if (roomState != null)
            {
                if (roomCodeText != null)
                    roomCodeText.text = $"Room: {roomState.RoomCode}";
                
                if (statusText != null)
                    statusText.text = $"Status: {roomState.Status}";
            }
        }

        private void OnLobbyToggleClicked()
        {
            if (lobbyView != null)
            {
                isLobbyVisible = !isLobbyVisible;
                if (isLobbyVisible)
                {
                    lobbyView.Show();
                }
                else
                {
                    lobbyView.Hide();
                }
            }
        }

        private void Update()
        {
            // This view just manages UI and lobby view toggle
            // Player movement is handled by the prefab's own components
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (lobbyView == null)
                lobbyView = GetComponentInChildren<LobbyView>();
        }
#endif
    }
}

