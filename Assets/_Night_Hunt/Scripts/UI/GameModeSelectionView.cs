using System.Collections.Generic;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Game Mode Selection View — displays available game modes with status badges.
    /// 
    /// Features:
    /// - Display 2v2, 3v3, 4v4, 5v5 modes
    /// - Show ACTIVE / COMING_SOON / DISABLED status
    /// - Lock disabled modes
    /// - Navigate to matchmaking or custom lobby after selection
    /// </summary>
    public class GameModeSelectionView : MonoBehaviour
    {
        // ── UI References ─────────────────────────────────────────────────────
        [Header("Game Mode Buttons")]
        [SerializeField] private GameObject modeButtonContainer;
        [SerializeField] private GameObject modeButtonPrefab;

        [Header("Selection")]
        [SerializeField] private TextMeshProUGUI selectedModeText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Party Options")]
        [SerializeField] private GameObject partyOptionsPanel;
        [SerializeField] private TextMeshProUGUI partyMemberCountText;
        [SerializeField] private Toggle allowFillToggle;
        [SerializeField] private TextMeshProUGUI allowFillTooltip;

        // ── Animation Callbacks (wire in Inspector) ───────────────────────────
        [Header("Animation Callbacks")]
        [Tooltip("Fires to open this dialog (wire to your animation Open call).")]
        [SerializeField] private UnityEvent onOpenRequested;
        [Tooltip("Fires to close this dialog (wire to your animation Close call).")]
        [SerializeField] private UnityEvent onCloseRequested;

        // ── Services ──────────────────────────────────────────────────────────
        private IBackendClient _backendClient;

        // ── State ─────────────────────────────────────────────────────────────
        private List<GameModeResponse> _gameModes;
        private string _selectedMode;
        private System.Action<string, bool> _onModeSelected; // gameMode, allowFill
        private bool _isPartyQueue;
        private int _partyMemberCount;

        // ──────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (GameManager.Instance != null)
            {
                _backendClient = GameManager.Instance.BackendClient;
            }

            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void Start()
        {
            LoadGameModes();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Show the mode selection dialog for solo queue.
        /// Visibility is handled by the animation system — fires <c>onOpenRequested</c>.
        /// </summary>
        public void Show(System.Action<string> onModeSelected)
        {
            _isPartyQueue = false;
            _partyMemberCount = 1;
            _onModeSelected = (mode, allowFill) => onModeSelected?.Invoke(mode);
            UpdatePartyUI();
            LoadGameModes();
            onOpenRequested?.Invoke();
        }

        /// <summary>
        /// Show the mode selection dialog for party queue.
        /// Visibility is handled by the animation system — fires <c>onOpenRequested</c>.
        /// </summary>
        public void ShowForParty(int partyMemberCount, System.Action<string, bool> onModeSelected)
        {
            _isPartyQueue = true;
            _partyMemberCount = partyMemberCount;
            _onModeSelected = onModeSelected;
            UpdatePartyUI();
            LoadGameModes();
            onOpenRequested?.Invoke();
        }

        /// <summary>
        /// Update party-specific UI elements.
        /// </summary>
        private void UpdatePartyUI()
        {
            if (partyOptionsPanel != null)
            {
                partyOptionsPanel.SetActive(_isPartyQueue);
            }

            if (_isPartyQueue)
            {
                if (partyMemberCountText != null)
                {
                    partyMemberCountText.text = $"Party Size: {_partyMemberCount}";
                }

                if (allowFillToggle != null)
                {
                    allowFillToggle.isOn = true; // Default to allow fill
                }

                if (allowFillTooltip != null)
                {
                    allowFillTooltip.text = "Allow solo players to fill empty slots in your team";
                }
            }
        }

        /// <summary>
        /// Hide the mode selection dialog.
        /// Visibility is handled by the animation system — fires <c>onCloseRequested</c>.
        /// </summary>
        public void Hide()
        {
            _onModeSelected = null;
            onCloseRequested?.Invoke();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Game Mode Loading

        private async void LoadGameModes()
        {
            if (_backendClient == null) return;

            const string endpoint = "/api/game-modes";
            var result = await _backendClient.GetAsync<List<GameModeResponse>>(endpoint);

            if (result.Success && result.Data != null)
            {
                _gameModes = result.Data;
                DisplayGameModes(_gameModes);
            }
            else
            {
                Debug.LogError($"[GameModeSelection] Failed to load game modes: {result.Message}");
                // Fallback: Show default modes
                CreateDefaultModes();
            }
        }

        private void CreateDefaultModes()
        {
            _gameModes = new List<GameModeResponse>
            {
                new GameModeResponse { modeKey = "2v2", displayName = "2v2", totalPlayers = 4, playersPerTeam = 2, active = true,  modeStatus = "AVAILABLE" },
                new GameModeResponse { modeKey = "3v3", displayName = "3v3", totalPlayers = 6, playersPerTeam = 3, active = false, modeStatus = "COMING_SOON" },
                new GameModeResponse { modeKey = "4v4", displayName = "4v4", totalPlayers = 8, playersPerTeam = 4, active = false, modeStatus = "COMING_SOON" },
                new GameModeResponse { modeKey = "5v5", displayName = "5v5", totalPlayers = 10, playersPerTeam = 5, active = false, modeStatus = "COMING_SOON" }
            };
            DisplayGameModes(_gameModes);
        }

        private void DisplayGameModes(List<GameModeResponse> modes)
        {
            // Clear existing buttons
            foreach (Transform child in modeButtonContainer.transform)
            {
                Destroy(child.gameObject);
            }

            // Create new buttons
            foreach (var mode in modes)
            {
                var buttonObj = Instantiate(modeButtonPrefab, modeButtonContainer.transform);
                var buttonView = buttonObj.GetComponent<GameModeButtonView>();
                if (buttonView != null)
                {
                    buttonView.Setup(mode, OnModeButtonClicked);
                }
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Mode Selection

        private void OnModeButtonClicked(GameModeResponse mode)
        {
            if (!mode.active || mode.modeStatus != "AVAILABLE")
            {
                Debug.Log($"[GameModeSelection] Mode {mode.modeKey} is not available (status: {mode.modeStatus})");
                return;
            }

            _selectedMode = mode.modeKey;
            if (selectedModeText != null)
            {
                selectedModeText.text = $"Selected: {mode.displayName}";
            }
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(_selectedMode))
            {
                Debug.LogWarning("[GameModeSelection] No mode selected");
                return;
            }

            // Validate party size vs mode capacity
            if (_isPartyQueue)
            {
                var selectedModeData = _gameModes?.Find(m => m.modeKey == _selectedMode);
                if (selectedModeData != null && _partyMemberCount > selectedModeData.playersPerTeam)
                {
                    Debug.LogWarning($"[GameModeSelection] Party size ({_partyMemberCount}) exceeds mode capacity ({selectedModeData.playersPerTeam})");
                    return;
                }
            }

            bool allowFill = _isPartyQueue && allowFillToggle != null && allowFillToggle.isOn;
            _onModeSelected?.Invoke(_selectedMode, allowFill);
            Hide();
        }

        private void OnCancelClicked()
        {
            Hide();
        }

        #endregion
    }
}
