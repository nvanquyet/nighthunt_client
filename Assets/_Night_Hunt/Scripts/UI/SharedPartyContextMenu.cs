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
    /// Pattern mirrors SharedFriendContextMenu:
    ///   • Items (AvatarView / ModelSlotView) raise an onSlotClicked callback.
    ///   • List views (PartyMemberListView / PartyModelListView) call Show().
    ///   • No context-menu logic lives inside the item prefabs.
    ///
    /// FIXES vs original:
    ///   1. Backdrop is now set fullscreen (anchorMin=0,0 / anchorMax=1,1) in Awake,
    ///      matching SharedFriendContextMenu — clicks outside the panel are captured
    ///      regardless of prefab layout in the Inspector.
    ///   2. RepositionNear() clamps the panel so it never clips outside its parent rect.
    ///   3. Show() calls panel.transform.SetAsLastSibling() so it renders above the
    ///      backdrop even if the prefab has them in a different order.
    ///
    /// SETUP (Prefab hierarchy — place as last child of Home panel root):
    ///   PartyContextMenu (this script)
    ///   ├── Backdrop   (Button — fullscreen transparent, blocks raycasts, closes on click)
    ///   └── Panel      (RectTransform — the visible popup, MUST be sibling of Backdrop)
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
            // ── FIX 1: Set backdrop as fullscreen transparent overlay ──────────
            // Matches the pattern in SharedFriendContextMenu.Awake().
            // This guarantees clicks outside the Panel are caught regardless of
            // how the prefab positions its Backdrop RectTransform in the Inspector.
            if (backdrop != null)
            {
                var rt = backdrop.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                // Needs a raycast-target Graphic; add an invisible Image if none exists.
                var img = backdrop.GetComponent<Image>();
                if (img == null) img = backdrop.gameObject.AddComponent<Image>();
                img.color        = new Color(0f, 0f, 0f, 0f); // fully transparent
                img.raycastTarget = true;
            }

            if (backdrop        != null) backdrop.onClick.AddListener(Hide);
            if (btn_ViewProfile != null) btn_ViewProfile.onClick.AddListener(OnViewProfile);
            if (btn_Kick        != null) btn_Kick.onClick.AddListener(OnKick);
            if (btn_Leave       != null) btn_Leave.onClick.AddListener(OnLeave);

            Hide();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Show the context menu anchored near <paramref name="anchor"/>.
        /// Moves this entire GameObject to the last sibling of its parent so it renders
        /// above every other party slot item. Then ensures Panel renders above Backdrop
        /// within the menu's own children.
        /// </summary>
        /// <param name="anchor">RectTransform of the clicked slot — used for positioning.</param>
        /// <param name="showKick">Show Kick button (host and not own slot).</param>
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

            // ── FIX 2: Bring the whole menu tree to the top of its sibling list ──
            // Guarantees it renders above every party slot item (same as FriendContextMenu).
            transform.SetAsLastSibling();

            // ── FIX 3: Ensure Panel is above Backdrop within our own children ───
            // If prefab has Backdrop after Panel, we'd click through the backdrop.
            if (panel != null) panel.transform.SetAsLastSibling();

            if (backdrop != null) backdrop.gameObject.SetActive(true);
            if (panel    != null) panel.gameObject.SetActive(true);

            RepositionNear(anchor);
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
        /// Converts the top-left world corner of <paramref name="anchor"/> into local space
        /// of <c>panel.parent</c> and applies it as anchoredPosition.
        ///
        /// ── FIX: Clamps the resulting position so the panel never clips outside
        /// its parent's rect, regardless of where on screen the slot lives.
        /// </summary>
        private void RepositionNear(RectTransform anchor)
        {
            if (panel == null || anchor == null) return;

            var parentRT = panel.parent as RectTransform;
            if (parentRT == null) return;

            // GetWorldCorners: 0=BL, 1=TL, 2=TR, 3=BR
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);

            // Anchor to the top-left corner of the slot
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT,
                RectTransformUtility.WorldToScreenPoint(null, corners[1]),
                null,
                out Vector2 local);

            // ── FIX: Clamp so the panel stays inside the parent rect ─────────
            Rect   parentRect = parentRT.rect;
            Vector2 panelSize  = panel.sizeDelta;

            // horizontal: don't let the right edge escape
            float maxX = parentRect.xMax - panelSize.x;
            local.x = Mathf.Clamp(local.x, parentRect.xMin, maxX);

            // vertical: don't let the bottom edge escape (panel drops downward from local.y)
            float minY = parentRect.yMin + panelSize.y;
            local.y = Mathf.Clamp(local.y, minY, parentRect.yMax);

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