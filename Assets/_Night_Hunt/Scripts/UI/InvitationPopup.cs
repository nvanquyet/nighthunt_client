using System.Collections;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.Services.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// [DEPRECATED] InvitationPopup — superseded by GameModalWindow.ShowCountdown.
    ///
    /// As of the HomeView cleanup, party_invitation_received WS events are handled
    /// directly by HomeView.HandlePartyInvitationReceived using:
    ///   GameModalWindow.Instance?.ShowCountdown(...)
    ///
    /// This class is kept for reference only. Do NOT place it in the scene.
    /// HomeView no longer references InvitationPopup.
    ///
    /// Original behaviour (for reference):
    ///   • Countdown 30 s, queues back-to-back invitations
    ///   • Accept  → PartyService.AcceptInvitation(invitationId)
    ///   • Decline → PartyService.DeclineInvitation(invitationId)
    /// </summary>
    [System.Obsolete("Replaced by GameModalWindow.ShowCountdown in HomeView. Do not use.")]
    public class InvitationPopup : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("UI References")]
        [SerializeField] private GameObject      panelRoot;
        [SerializeField] private TextMeshProUGUI inviterText;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private Button          btn_Accept;
        [SerializeField] private Button          btn_Decline;

        // ── Runtime ────────────────────────────────────────────────────────────
        private PartyService _partyService;

        // Current invitation being displayed
        private long   _currentInvitationId;
        private float  _countdown;
        private bool   _isShowing;

        // Queue for back-to-back invitations
        private readonly System.Collections.Generic.Queue<GameWebSocketService.PartyInvitationEvent>
            _queue = new();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (btn_Accept  != null) btn_Accept.onClick.AddListener(OnAcceptClicked);
            if (btn_Decline != null) btn_Decline.onClick.AddListener(OnDeclineClicked);
            HidePanel();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                _partyService = GameManager.Instance.PartyService;
        }

        private void Update()
        {
            if (!_isShowing) return;

            _countdown -= Time.unscaledDeltaTime;

            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(Mathf.Max(_countdown, 0f)) + "s";

            if (_countdown <= 0f)
            {
                // Auto-decline silently (server will expire it anyway)
                _ = AutoDecline();
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Show the popup for a party invitation.
        /// If another popup is already showing, queues this one.
        /// Called by HomeView on WS party_invitation_received event.
        /// </summary>
        public void Show(GameWebSocketService.PartyInvitationEvent evt)
        {
            if (_isShowing)
            {
                _queue.Enqueue(evt);
                return;
            }
            DisplayEvent(evt);
        }

        // ── Display ────────────────────────────────────────────────────────────

        private void DisplayEvent(GameWebSocketService.PartyInvitationEvent evt)
        {
            _currentInvitationId = evt.invitationId;
            _countdown           = 30f;  // server-side expiry; no secondsRemaining field on event
            _isShowing           = true;

            if (inviterText   != null)
                inviterText.text = $"Lời mời từ <b>{evt.fromUsername}</b>";

            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(_countdown) + "s";

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        private void HidePanel()
        {
            _isShowing = false;
            if (panelRoot != null) panelRoot.SetActive(false);

            // Show next queued invitation, if any
            if (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                // Small delay so UI doesn't flicker instantly
                StartCoroutine(ShowNextFrame(next));
            }
        }

        private IEnumerator ShowNextFrame(GameWebSocketService.PartyInvitationEvent evt)
        {
            yield return null;
            DisplayEvent(evt);
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private async void OnAcceptClicked()
        {
            long id = _currentInvitationId;
            HidePanel();
            if (_partyService == null) return;
            var result = await _partyService.AcceptInvitation(id);
            if (!result.Success)
                ShowToast("Không thể tham gia party", result.Message ?? "Vui lòng thử lại.");
        }

        private async void OnDeclineClicked()
        {
            long id = _currentInvitationId;
            HidePanel();
            if (_partyService == null) return;
            await _partyService.DeclineInvitation(id);
        }

        private async System.Threading.Tasks.Task AutoDecline()
        {
            long id = _currentInvitationId;
            HidePanel();
            if (_partyService == null) return;
            // Auto-decline — server expires invite anyway, but explicit is cleaner
            await _partyService.DeclineInvitation(id);
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private void ShowToast(string title, string message)
        {
            var toast = PersistentUICanvas.Instance != null
                ? PersistentUICanvas.Instance.ToastService
                : ToastService.Instance;
            toast?.Show(title: title, message: message);
        }
    }
}
