using System.Collections.Generic;
using System.Linq;
using Michsky.MUIP;
using NightHunt.Common;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightHunt.UI
{
    public class GameModeSelectionView : MonoBehaviour
    {
        [SerializeField] private GameObject modeButtonContainer;
        [SerializeField] private GameObject modeButtonPrefab;
        [SerializeField] private TextMeshProUGUI selectedModeText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private GameObject partyOptionsPanel;
        [SerializeField] private TextMeshProUGUI partyMemberCountText;
        [SerializeField] private Toggle allowFillToggle;
        [SerializeField] private UnityEvent onOpenRequested;
        [SerializeField] private UnityEvent onCloseRequested;

        private string _selectedMode;
        private System.Action<string, bool> _onModeSelected;
        private bool _isPartyQueue;
        private int _partyMemberCount;
        private bool _isOpen;
        private bool _disabledBecauseAttachedToDropdown;

        private void Awake()
        {
            if (GetComponent<CustomDropdown>() != null)
            {
                _disabledBecauseAttachedToDropdown = true;
                enabled = false;
                return;
            }

            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnEnable()
        {
            if (_disabledBecauseAttachedToDropdown || GetComponent<CustomDropdown>() != null)
            {
                _disabledBecauseAttachedToDropdown = true;
                enabled = false;
                return;
            }

            GameModeConfig.OnConfigLoaded += HandleConfigLoaded;
            if (_isOpen) LoadGameModes();
        }

        private void OnDisable()
        {
            GameModeConfig.OnConfigLoaded -= HandleConfigLoaded;
        }

        private void Start()
        {
            if (_isOpen) LoadGameModes();
        }

        public void Show(System.Action<string> onModeSelected)
        {
            if (_disabledBecauseAttachedToDropdown) return;
            _isOpen = true;
            _isPartyQueue = false; _partyMemberCount = 1;
            _onModeSelected = (mode, fill) => onModeSelected?.Invoke(mode);
            UpdatePartyUI(); LoadGameModes();
            onOpenRequested?.Invoke();
        }

        public void ShowForParty(int partyMemberCount, System.Action<string, bool> onModeSelected)
        {
            if (_disabledBecauseAttachedToDropdown) return;
            _isOpen = true;
            _isPartyQueue = true; _partyMemberCount = partyMemberCount;
            _onModeSelected = onModeSelected;
            UpdatePartyUI(); LoadGameModes();
            onOpenRequested?.Invoke();
        }

        private void UpdatePartyUI()
        {
            if (partyOptionsPanel != null) partyOptionsPanel.SetActive(_isPartyQueue);
            if (_isPartyQueue)
            {
                if (partyMemberCountText != null) partyMemberCountText.text = $"Party Size: {_partyMemberCount}";
                if (allowFillToggle != null) allowFillToggle.isOn = true;
            }
        }

        public void Hide() { _isOpen = false; _onModeSelected = null; onCloseRequested?.Invoke(); }

        private void HandleConfigLoaded()
        {
            if (_isOpen) LoadGameModes();
        }

        private void LoadGameModes()
        {
            var modes = GameModeConfig.GetEnabled();
            if (modes != null && modes.Length > 0) DisplayGameModes(modes);
            else DisplayGameModes(GameModeConfig.GetAll());
        }

        private void DisplayGameModes(IEnumerable<GameModeEntry> modes)
        {
            if (modeButtonContainer == null) return;
            foreach (Transform child in modeButtonContainer.transform) Destroy(child.gameObject);
            foreach (var mode in modes)
            {
                if (modeButtonPrefab == null) break;
                var buttonObj = Instantiate(modeButtonPrefab, modeButtonContainer.transform);
                var buttonView = buttonObj.GetComponent<GameModeButtonView>();
                if (buttonView != null)
                {
                    var resp = new GameModeResponse {
                        modeKey = mode.modeKey,
                        displayName = mode.displayName,
                        playersPerTeam = mode.playersPerTeam,
                        active = mode.isEnabled,
                        modeStatus = mode.isEnabled ? "AVAILABLE" : "COMING_SOON"
                    };
                    buttonView.Setup(resp, OnModeButtonClicked);
                }
            }
        }

        private void OnModeButtonClicked(GameModeResponse mode)
        {
            if (mode.modeStatus != "AVAILABLE") return;
            _selectedMode = mode.modeKey;
            if (selectedModeText != null) selectedModeText.text = $"Selected: {mode.displayName}";
        }

        private void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(_selectedMode)) return;
            var mode = GameModeConfig.GetAll().FirstOrDefault(m => m.modeKey == _selectedMode);
            if (!string.IsNullOrEmpty(mode.modeKey) && _isPartyQueue && _partyMemberCount > mode.playersPerTeam) return;
            _onModeSelected?.Invoke(_selectedMode, _isPartyQueue && allowFillToggle != null && allowFillToggle.isOn);
            Hide();
        }

        private void OnCancelClicked() { Hide(); }
    }
}
