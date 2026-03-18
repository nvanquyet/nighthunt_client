using System;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Shared context-menu for friend rows in FriendPanelView.
    ///
    /// One instance lives as a DIRECT CHILD of the FriendPanel root with
    /// <c>transform.SetAsLastSibling()</c> called on every <see cref="Show"/> —
    /// guarantees it renders on top of every friend row item.
    ///
    /// Mirrors <see cref="SharedPartyContextMenu"/> in architecture.
    ///
    /// SETUP (Prefab — place as last child of FriendPanel root):
    ///   FriendContextMenu (this script)
    ///   ├── Backdrop   (Button — fullscreen transparent, closes on click)
    ///   └── Panel      (RectTransform — the visible popup)
    ///       ├── Btn_InviteToParty  (conditional: visible only when canInvite)
    ///       └── Btn_RemoveFriend
    /// </summary>
    public class SharedFriendContextMenu : MonoBehaviour
    {
        [Header("Panel (RectTransform — positioned near the clicked row)")]
        [SerializeField] private RectTransform panel;

        [Header("Backdrop (fullscreen transparent button — click-outside dismiss)")]
        [SerializeField] private Button backdrop;

        [Header("Action Buttons")]
        [Tooltip("Shown only when iAmPartyHost AND friend is online. Hidden otherwise.")]
        [SerializeField] private Button btn_InviteToParty;
        [SerializeField] private Button btn_RemoveFriend;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Action _onInvite;
        private Action _onRemove;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (backdrop          != null) backdrop.onClick.AddListener(Hide);
            if (btn_InviteToParty != null) btn_InviteToParty.onClick.AddListener(OnInvite);
            if (btn_RemoveFriend  != null) btn_RemoveFriend.onClick.AddListener(OnRemove);
            Hide();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Show the context menu anchored near <paramref name="anchor"/>.
        /// </summary>
        /// <param name="anchor">RectTransform of the clicked friend row.</param>
        /// <param name="canInvite">Show the Invite to Party button.</param>
        /// <param name="onInvite">Called when Invite is pressed.</param>
        /// <param name="onRemove">Called when Remove Friend is pressed.</param>
        public void Show(RectTransform anchor, bool canInvite, Action onInvite, Action onRemove)
        {
            _onInvite = onInvite;
            _onRemove = onRemove;

            if (btn_InviteToParty != null)
                btn_InviteToParty.gameObject.SetActive(canInvite);

            RepositionNear(anchor);
            transform.SetAsLastSibling();

            if (backdrop != null) backdrop.gameObject.SetActive(true);
            if (panel    != null) panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (panel    != null) panel.gameObject.SetActive(false);
            _onInvite = null;
            _onRemove = null;
        }

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        // ── Private ──────────────────────────────────────────────────────────

        private void RepositionNear(RectTransform anchor)
        {
            if (panel == null || anchor == null) return;
            var parentRT = panel.parent as RectTransform;
            if (parentRT == null) return;

            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners); // 0=BL 1=TL 2=TR 3=BR

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                RectTransformUtility.WorldToScreenPoint(null, corners[1]), // top-left of row
                null,
                out Vector2 local);

            panel.anchoredPosition = local;
        }

        private void OnInvite() { var cb = _onInvite; Hide(); cb?.Invoke(); }
        private void OnRemove() { var cb = _onRemove; Hide(); cb?.Invoke(); }
    }
}
