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
    ///   ├── Backdrop   (Button — fullscreen transparent, closes on click)  ← FIRST child (behind panel)
    ///   └── Panel      (RectTransform — the visible popup)                  ← SECOND child (in front)
    ///       ├── Btn_ViewProfile   (always visible)
    ///       ├── Btn_InviteToParty (conditional: visible only when canInvite)
    ///       └── Btn_RemoveFriend
    ///
    /// Anchor positioning:
    ///   Pass <c>FriendItemView.ContextMenuAnchor</c> as anchorPoint.
    ///   The panel's anchoredPosition is set to the world-position of that anchor's pivot —
    ///   so designers control placement by repositioning the anchor child on the item prefab.
    /// </summary>
    public class SharedFriendContextMenu : MonoBehaviour
    {
        [Header("Panel (RectTransform — positioned near the clicked row)")]
        [SerializeField] private RectTransform panel;

        [Header("Backdrop (fullscreen transparent button — click-outside dismiss)")]
        [SerializeField] private Button backdrop;

        [Header("Action Buttons")]
        [SerializeField] private Button btn_ViewProfile;
        [Tooltip("Shown only when iAmPartyHost AND friend is online. Hidden otherwise.")]
        [SerializeField] private Button btn_InviteToParty;
        [SerializeField] private Button btn_RemoveFriend;

        // ── Runtime ──────────────────────────────────────────────────────────

        private long   _userId;
        private Action _onViewProfile;
        private Action _onInvite;
        private Action _onRemove;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Wire buttons
            if (backdrop          != null) backdrop.onClick.AddListener(Hide);
            if (btn_ViewProfile   != null) btn_ViewProfile.onClick.AddListener(OnViewProfile);
            if (btn_InviteToParty != null) btn_InviteToParty.onClick.AddListener(OnInvite);
            if (btn_RemoveFriend  != null) btn_RemoveFriend.onClick.AddListener(OnRemove);

            // Ensure backdrop covers the entire parent (fullscreen transparent overlay).
            // This guarantees clicks outside the Panel are caught regardless of prefab setup.
            if (backdrop != null)
            {
                var rt = backdrop.GetComponent<RectTransform>();
                UIContextMenuPositioner.PrepareFullscreenBackdrop(rt);
                // Needs a raycast-target Graphic; add invisible Image if none exists.
                var img = backdrop.GetComponent<Image>();
                if (img == null) img = backdrop.gameObject.AddComponent<Image>();
                img.color        = new Color(0f, 0f, 0f, 0f); // invisible
                img.raycastTarget = true;
            }

            UIContextMenuRegistry.Register(this, Hide);
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            UIContextMenuRegistry.Unregister(this);
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Show the context menu anchored at <paramref name="anchorPoint"/>.
        /// </summary>
        /// <param name="anchorPoint">
        ///   The RectTransform whose world pivot position determines where the menu appears.
        ///   Use <c>FriendItemView.ContextMenuAnchor</c> (a designer-placed child RT on the prefab).
        /// </param>
        /// <param name="canInvite">Show the Invite to Party button.</param>
        /// <param name="userId">Friend's userId (for future profile navigation).</param>
        /// <param name="onViewProfile">Called when View Profile is pressed.</param>
        /// <param name="onInvite">Called when Invite to Party is pressed.</param>
        /// <param name="onRemove">Called when Remove Friend is pressed.</param>
        public void Show(RectTransform anchorPoint, bool canInvite, long userId,
                         Action onViewProfile, Action onInvite, Action onRemove)
        {
            _userId        = userId;
            _onViewProfile = onViewProfile;
            _onInvite      = onInvite;
            _onRemove      = onRemove;

            if (btn_ViewProfile   != null) btn_ViewProfile.gameObject.SetActive(true);
            if (btn_InviteToParty != null) btn_InviteToParty.gameObject.SetActive(canInvite);

            UIContextMenuRegistry.CloseAllExcept(this);

            // Bring whole context-menu tree to top of parent siblings (renders above all rows).
            transform.SetAsLastSibling();

            // Within the context menu, ensure Panel renders on top of Backdrop.
            if (panel != null) panel.transform.SetAsLastSibling();

            if (backdrop != null) backdrop.gameObject.SetActive(true);
            if (panel    != null) panel.gameObject.SetActive(true);

            RepositionNear(anchorPoint);
        }

        public void Hide()
        {
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (panel    != null) panel.gameObject.SetActive(false);
            _onViewProfile = null;
            _onInvite      = null;
            _onRemove      = null;
            _userId        = 0;
        }

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Places <see cref="panel"/> so its anchoredPosition matches the world-space pivot
        /// of <paramref name="anchorPoint"/>.  Designers control the exact spot by moving the
        /// anchor child object on the FriendItemView prefab.
        /// </summary>
        private void RepositionNear(RectTransform anchorPoint)
        {
            UIContextMenuPositioner.PlaceNearPivot(panel, anchorPoint);

            // Use the anchor's world pivot (not corners) — the designer controls the exact
            // placement by repositioning the contextMenuAnchor child Transform in the prefab.
        }

        private void OnViewProfile() { var cb = _onViewProfile; Hide(); cb?.Invoke(); }
        private void OnInvite()      { var cb = _onInvite;      Hide(); cb?.Invoke(); }
        private void OnRemove()      { var cb = _onRemove;      Hide(); cb?.Invoke(); }
    }
}
