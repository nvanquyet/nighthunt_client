using System;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.State;
using NightHunt.Utils;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI
{
    public class PlayerProfilePanel : MonoBehaviour
    {
        public static PlayerProfilePanel Instance { get; private set; }

        [Header("Root (set active/inactive on show/hide)")]
        [SerializeField] private GameObject root;

        [Header("Backdrop — click-outside dismiss")]
        [SerializeField] private Button backdrop;

        [Header("Profile fields")]
        [SerializeField] private TMP_Text txt_Username;
        [SerializeField] private TMP_Text txt_ELO;
        [SerializeField] private TMP_Text txt_Tier;
        [SerializeField] private TMP_Text txt_WinLoss;
        [SerializeField] private TMP_Text txt_WinRate;
        [SerializeField] private Image    img_Character;

        [Header("Close button")]
        [SerializeField] private Button btn_Close;

        [Header("Account actions")]
        [SerializeField] private Button btn_ChangePassword;

        [Header("Loading indicator (optional)")]
        [SerializeField] private GameObject loadingIndicator;

        private IBackendClient _backendClient;
        private long           _currentUserId;
        private bool           _loading;
        private int            _loadVersion;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            WireButtons();
        }

        private void Start()
        {
            if (_backendClient == null && GameManager.Instance != null)
                _backendClient = GameManager.Instance.BackendClient;
            SetRootActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(long userId, string fallbackUsername = null)
        {
            EnsureRuntimeWiring();
            _currentUserId = userId;
            _loadVersion++;
            SetRootActive(true);
            UpdateAccountActionVisibility();
            if (root != null) root.transform.SetAsLastSibling();
            else transform.SetAsLastSibling();
            SetPlaceholder(fallbackUsername ?? $"Player {userId}");
            _ = LoadProfileAsync(userId, _loadVersion);
        }

        public void Hide()
        {
            _loadVersion++;
            _loading = false;
            SetLoading(false);
            SetRootActive(false);
            _currentUserId = 0;
        }

        private async Task LoadProfileAsync(long userId, int requestVersion)
        {
            _loading = true;
            SetLoading(true);
            try
            {
                if (_backendClient == null) _backendClient = GameManager.Instance?.BackendClient;
                if (_backendClient == null) return;
                string endpoint = string.Format(Constants.API_PROFILE_PUBLIC, userId);
                var result = await _backendClient.GetAsync<ProfileResponse>(endpoint);
                if (requestVersion != _loadVersion || _currentUserId != userId) return;
                if (result.Success && result.Data != null) PopulateProfile(result.Data);
                else Debug.LogWarning($"[PlayerProfilePanel] Failed to load public profile userId={userId} endpoint={endpoint} success={result.Success} message={result.Message ?? "null"}");
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                if (requestVersion == _loadVersion)
                {
                    _loading = false;
                    SetLoading(false);
                }
            }
        }

        private void SetPlaceholder(string username)
        {
            if (txt_Username != null) txt_Username.text = username;
            if (txt_ELO != null) txt_ELO.text = "ELO: --";
            if (txt_Tier != null) txt_Tier.text = "Tier: Unranked";
            if (txt_WinLoss != null) txt_WinLoss.text = "Record: --W / --L";
            if (txt_WinRate != null) txt_WinRate.text = "Win Rate: --%";
            if (img_Character != null) img_Character.sprite = null;
        }

        private void PopulateProfile(ProfileResponse profile)
        {
            if (txt_Username != null) txt_Username.text = profile.username;
            if (txt_ELO != null) txt_ELO.text = $"ELO: {profile.elo}";
            if (txt_Tier != null) txt_Tier.text = $"Tier: {profile.tier ?? "Unranked"}";
            int w = profile.totalWins;
            int l = profile.totalLosses;
            if (txt_WinLoss != null) txt_WinLoss.text = $"Record: {w}W / {l}L";
            if (txt_WinRate != null)
            {
                float total = w + l;
                float rate = total > 0 ? (float)w / total * 100f : 0f;
                txt_WinRate.text = $"Win Rate: {rate:F1}%";
            }
            if (img_Character != null && !string.IsNullOrEmpty(profile.selectedCharacterId))
            {
                var def = NightHunt.Gameplay.Character.Data.CharacterDatabase.Instance?.GetById(profile.selectedCharacterId);
                if (def != null) img_Character.sprite = def.Thumbnail;
            }

            UpdateAccountActionVisibility();
        }

        private void SetRootActive(bool on) { if (root != null) root.SetActive(on); else gameObject.SetActive(on); }

        public void EnsureRuntimeWiring()
        {
            WireButtons();
        }

        private void WireButtons()
        {
            if (btn_Close != null) { btn_Close.onClick.RemoveAllListeners(); btn_Close.onClick.AddListener(Hide); }
            if (btn_ChangePassword != null)
            {
                btn_ChangePassword.onClick.RemoveAllListeners();
                btn_ChangePassword.onClick.AddListener(OpenChangePasswordPopup);
            }
            if (backdrop != null) { backdrop.onClick.RemoveAllListeners(); backdrop.onClick.AddListener(Hide); }
        }

        private void UpdateAccountActionVisibility()
        {
            if (btn_ChangePassword == null)
                return;

            bool isOwnProfile = SessionState.Instance != null
                && _currentUserId != 0
                && SessionState.Instance.UserId == _currentUserId;

            var actionContainer = btn_ChangePassword.transform.parent;
            if (actionContainer != null && actionContainer != transform)
                actionContainer.gameObject.SetActive(isOwnProfile);
            btn_ChangePassword.gameObject.SetActive(isOwnProfile);
        }

        private void OpenChangePasswordPopup()
        {
            if (SessionState.Instance == null || SessionState.Instance.UserId != _currentUserId)
                return;

            if (GameManager.Instance?.AuthService == null)
            {
                Debug.LogWarning("[PlayerProfilePanel] AuthService not available for change password.");
                return;
            }

            ChangePasswordPopup.Show(async (oldPassword, newPassword, confirmNewPassword) =>
            {
                var result = await GameManager.Instance.AuthService.ChangePassword(oldPassword, newPassword, confirmNewPassword);
                if (result != null && result.Success)
                {
                    Hide();
                }
                return result;
            });
        }

        private void SetLoading(bool on) { if (loadingIndicator != null) loadingIndicator.SetActive(on); }
    }
}
