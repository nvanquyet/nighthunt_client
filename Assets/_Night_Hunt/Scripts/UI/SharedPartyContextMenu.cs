using System;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Shared context-dropdown for party slots (PartyMemberAvatarView and PartyModelSlotView).
    ///
    /// One instance lives as a DIRECT CHILD of the Home panel root with SetAsLastSibling()
    /// called every Show() — guarantees it renders on top of every party slot item.
    ///
    /// Pattern mirrors FriendPanelView.sharedDropdownPanel:
    ///   • Items (AvatarView / ModelSlotView) raise an onSlotClicked callback.
    ///   • List views (PartyMemberListView / PartyModelListView) call Show().
    ///   • No context-menu logic lives inside the item prefabs.
    ///
    /// SETUP (Prefab hierarchy place as last child of Home panel root):
    ///   PartyContextMenu (this script)
    ///   ├── Backdrop   (Button — fullscreen transparent, blocks raycasts, closes on click)
    ///   └── Panel      (RectTransform — the visible popup)
    ///       ├── Btn_ViewProfile
    ///       ├── Btn_Kick
    ///       └── Btn_Leave
    /// </summary>
    public class SharedPartyContextMenu : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Panel (RectTransform — anchored under its parent)")]
        [SerializeField] private RectTransform panel;

        [Header("Backdrop (fullscreen transparent button — click-outside dismiss)")]
        [SerializeField] private Button backdrop;

        [Header("Action Buttons")]
        [SerializeField] private Button btn_ViewProfile;
        [SerializeField] private Button btn_Kick;
        [SerializeField] private Button btn_Leave;

        // ── Runtime ──────────────────────────────────────────────────────────

        private Action<long> _onKick;
        private Action       _onLeave;
        private long         _userId;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (backdrop        != null) backdrop.onClick.AddListener(Hide);
            if (btn_ViewProfile != null) btn_ViewProfile.onClick.AddListener(OnViewProfile);
            if (btn_Kick        != null) btn_Kick.onClick.AddListener(OnKick);
            if (btn_Leave       != null) btn_Leave.onClick.AddListener(OnLeave);
            Hide();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Show the context menu anchored near <paramref name="anchor"/>.
        /// Moves this GameObject to the last sibling so it renders above every other sibling.
        /// </summary>
        /// <param name="anchor">RectTransform of the clicked slot — used for positioning.</param>
        /// <param name="showKick">Show Kick button (host && not own slot).</param>
        /// <param name="showLeave">Show Leave button (own slot).</param>
        /// <param name="userId">The member's userId — forwarded to <paramref name="onKick"/>.</param>
        /// <param name="onKick">Called with userId when Kick is pressed.</param>
        /// <param name="onLeave">Called when Leave is pressed.</param>
        public void Show(RectTransform anchor, bool showKick, bool showLeave,
                         long userId, Action<long> onKick, Action onLeave)
        {
            _userId  = userId;
            _onKick  = onKick;
            _onLeave = onLeave;

            if (btn_ViewProfile != null) btn_ViewProfile.gameObject.SetActive(true);
            if (btn_Kick        != null) btn_Kick.gameObject.SetActive(showKick);
            if (btn_Leave       != null) btn_Leave.gameObject.SetActive(showLeave);

            RepositionNear(anchor);

            // Bring this entire GameObject (backdrop + panel tree) to the top
            // of its sibling list so it draws over every party slot item.
            transform.SetAsLastSibling();

            if (backdrop != null) backdrop.gameObject.SetActive(true);
            if (panel    != null) panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (panel    != null) panel.gameObject.SetActive(false);
            _onKick  = null;
            _onLeave = null;
        }

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Converts the top-left world corner of <paramref name="anchor"/> into
        /// the local space of <c>panel.parent</c> and applies it as anchoredPosition.
        /// </summary>
        private void RepositionNear(RectTransform anchor)
        {
            if (panel == null || anchor == null) return;

            var parentRT = panel.parent as RectTransform;
            if (parentRT == null) return;

            // GetWorldCorners: 0=BL 1=TL 2=TR 3=BR
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                RectTransformUtility.WorldToScreenPoint(null, corners[1]), // top-left edge of slot
                null,
                out Vector2 local);

            panel.anchoredPosition = local;
        }

        private void OnViewProfile()
        {
            Hide();
            // TODO: open profile panel for _userId
        }

        private void OnKick()
        {
            var cb = _onKick;
            var id = _userId;
            Hide();
            cb?.Invoke(id);
        }

        private void OnLeave()
        {
            var cb = _onLeave;
            Hide();
            cb?.Invoke();
        }
    }
}
