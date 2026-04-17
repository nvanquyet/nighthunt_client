using System;
using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Backend;
using NightHunt.Utils;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI
{
    /// <summary>
    /// Modal panel that shows another player's public profile card.
    ///
    /// Opens via <see cref="Show(long, string)"/>, fetches GET /api/profile/{userId},
    /// then populates the UI.
    ///
    /// SETUP (Prefab / Inspector):
    ///   PlayerProfilePanel (this script, starts inactive)
    ///   ├── Backdrop        (Button — fullscreen, click closes)
    ///   └── Panel
    ///       ├── Txt_Username        (TMP_Text)
    ///       ├── Txt_ELO             (TMP_Text)
    ///       ├── Txt_Tier            (TMP_Text)
    ///       ├── Txt_WinLoss         (TMP_Text)   — e.g. "127W / 43L"
    ///       ├── Txt_WinRate         (TMP_Text)   — e.g. "74.7%"
    ///       ├── Img_Character       (Image)       — optional character avatar
    ///       └── Btn_Close           (Button)
    ///
    /// Wire in Inspector on: FriendPanelView, SharedPartyContextMenu, CustomLobbyView.
    ///
    /// SINGLETON PATTERN: One instance lives on the Home scene root.
    /// Call <c>PlayerProfilePanel.Instance?.Show(userId, username)</c> from anywhere.
    /// </summary>
    public class PlayerProfilePanel : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static PlayerProfilePanel Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

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

        [Header("Close button")]
        [SerializeField] private Button btn_Close;

        [Header("Loading indicator (optional)")]
        [SerializeField] private GameObject loadingIndicator;

        // ── Runtime ───────────────────────────────────────────────────────────

        private IBackendClient _backendClient;
        private long           _currentUserId;
        private bool           _loading;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            btn_Close?.onClick.AddListener(Hide);
            backdrop?.onClick.AddListener(Hide);
        }

        private void Start()
        {
            if (_backendClient == null && GameManager.Instance != null)
                _backendClient = GameManager.Instance.BackendClient;

            // Start hidden
            SetRootActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Open the profile panel for the given player.
        /// Shows the panel immediately with a placeholder username, then loads
        /// the full profile async from the backend.
        /// </summary>
        /// <param name="userId">Target player ID.</param>
        /// <param name="fallbackUsername">Display name shown while loading.</param>
        public void Show(long userId, string fallbackUsername = null)
        {
            _currentUserId = userId;
            SetRootActive(true);
            transform.SetAsLastSibling();

            // Show placeholder immediately
            SetPlaceholder(fallbackUsername ?? $"Player {userId}");
            _ = LoadProfileAsync(userId);
        }

        public void Hide()
        {
            SetRootActive(false);
            _currentUserId = 0;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private async Task LoadProfileAsync(long userId)
        {
            if (_loading) return;
            _loading = true;
            SetLoading(true);

            try
            {
                if (_backendClient == null)
                    _backendClient = GameManager.Instance?.BackendClient;

                if (_backendClient == null)
                {
                    ConditionalLogger.LogWarning("PlayerProfilePanel", "BackendClient not available");
                    return;
                }

                string endpoint = string.Format(Constants.API_PROFILE_PUBLIC, userId);
                var result = await _backendClient.GetAsync<ProfileResponse>(endpoint);

                // Guard: panel may have been closed while awaiting
                if (_currentUserId != userId) return;

                if (result.Success && result.Data != null)
                {
                    ConditionalLogger.Log("PlayerProfilePanel", $"Profile loaded: userId={userId} username={result.Data.username}");
                    PopulateProfile(result.Data);
                }
                else
                {
                    ConditionalLogger.LogWarning("PlayerProfilePanel", $"Failed to load profile userId={userId}: {result.Message}");
                    // Keep placeholder visible; don't hide panel
                }
            }
            catch (Exception ex)
            {
                ConditionalLogger.LogError("PlayerProfilePanel", $"LoadProfile exception: {ex.Message}", ex);
            }
            finally
            {
                _loading = false;
                SetLoading(false);
            }
        }

        private void SetPlaceholder(string username)
        {
            if (txt_Username != null) txt_Username.text = username;
            if (txt_ELO     != null) txt_ELO.text      = "ELO: —";
            if (txt_Tier    != null) txt_Tier.text      = "—";
            if (txt_WinLoss != null) txt_WinLoss.text   = "—W / —L";
            if (txt_WinRate != null) txt_WinRate.text   = "—%";
        }

        private void PopulateProfile(ProfileResponse profile)
        {
            if (txt_Username != null)
                txt_Username.text = profile.username;

            if (txt_ELO != null)
                txt_ELO.text = $"ELO: {profile.elo}";

            if (txt_Tier != null)
                txt_Tier.text = profile.tier ?? "Unranked";

            int w = profile.totalWins;
            int l = profile.totalLosses;
            if (txt_WinLoss != null)
                txt_WinLoss.text = $"{w}W / {l}L";

            if (txt_WinRate != null)
            {
                float total = w + l;
                float rate  = total > 0 ? w / total * 100f : 0f;
                txt_WinRate.text = $"{rate:F1}%";
            }
        }

        private void SetLoading(bool on)
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(on);
        }

        private void SetRootActive(bool on)
        {
            if (root != null)
                root.SetActive(on);
            else
                gameObject.SetActive(on);
        }
    }
}
