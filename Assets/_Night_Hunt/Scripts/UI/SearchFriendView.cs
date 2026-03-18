using System.Collections;
using NightHunt.Core;
using NightHunt.Services.Friend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Search-for-new-friends sub-panel embedded in the More Friends tab.
    /// Self-contained: owns its own search input, button, and result feedback.
    ///
    /// Flow: player types an exact username → taps Send → service posts a friend
    /// request. No "search results list" — the server only accepts an exact match.
    ///
    /// SETUP (Prefab / child hierarchy under MoreFriendsPanel):
    ///   SearchFriendView (this script)
    ///   ├── UsernameInput     (TMP_InputField — type username here)
    ///   ├── SendButton        (Button — send friend request)
    ///   └── FeedbackText      (TMP — success / error message, auto-hides)
    /// </summary>
    public class SearchFriendView : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TMP_InputField usernameInput;

        [Header("Action")]
        [SerializeField] private Button sendButton;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI feedbackText;

        [Tooltip("How long (s) the feedback message stays visible before fading.")]
        [SerializeField] private float feedbackDuration = 3f;

        // ── Runtime ──────────────────────────────────────────────────────────

        private FriendService _friendService;
        private Coroutine     _hideFeedbackCoroutine;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClicked);

            if (feedbackText != null)
                feedbackText.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                _friendService = GameManager.Instance.FriendService;
        }

        // ── Private ──────────────────────────────────────────────────────────

        private async void OnSendClicked()
        {
            if (_friendService == null) return;

            string username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(username))
            {
                ShowFeedback("Please enter a username.", success: false);
                return;
            }

            if (sendButton != null) sendButton.interactable = false;

            var result = await _friendService.SendFriendRequest(username);

            if (sendButton != null) sendButton.interactable = true;

            if (result.Success)
            {
                if (usernameInput != null) usernameInput.text = string.Empty;
                ShowFeedback($"Friend request sent to {username}!", success: true);
            }
            else
            {
                ShowFeedback(result.Message ?? "Failed to send request.", success: false);
            }
        }

        private void ShowFeedback(string message, bool success)
        {
            if (feedbackText == null) return;

            feedbackText.text  = message;
            feedbackText.color = success
                ? new Color(0.2f, 0.9f, 0.2f)
                : new Color(1f,   0.3f, 0.3f);
            feedbackText.gameObject.SetActive(true);

            if (_hideFeedbackCoroutine != null) StopCoroutine(_hideFeedbackCoroutine);
            _hideFeedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay());
        }

        private IEnumerator HideFeedbackAfterDelay()
        {
            yield return new WaitForSeconds(feedbackDuration);
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        }
    }
}
