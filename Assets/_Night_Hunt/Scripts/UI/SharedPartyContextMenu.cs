using System;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    public class SharedPartyContextMenu : MonoBehaviour
    {
        public static SharedPartyContextMenu Instance { get; private set; }

        [SerializeField] private RectTransform panel;
        [SerializeField] private Button backdrop;
        [SerializeField] private Button btn_ViewProfile;
        [SerializeField] private Button btn_TransferLeader;
        [SerializeField] private Button btn_Kick;
        [SerializeField] private Button btn_Leave;

        private Action<long> _onKick;
        private Action<long> _onTransferLeader;
        private Action       _onLeave;
        private long         _userId;

        private void Awake()
        {
            Instance = this;
            if (backdrop != null)
            {
                var rt = backdrop.GetComponent<RectTransform>();
                UIContextMenuPositioner.PrepareFullscreenBackdrop(rt);
                var img = backdrop.GetComponent<Image>() ?? backdrop.gameObject.AddComponent<Image>();
                img.color = new Color(0,0,0,0); img.raycastTarget = true;
                backdrop.onClick.AddListener(Hide);
            }
            if (btn_ViewProfile != null) btn_ViewProfile.onClick.AddListener(OnViewProfile);
            if (btn_TransferLeader != null) btn_TransferLeader.onClick.AddListener(OnTransferLeader);
            if (btn_Kick != null) btn_Kick.onClick.AddListener(OnKick);
            if (btn_Leave != null) btn_Leave.onClick.AddListener(OnLeave);
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
            if (Instance == this)
                Instance = null;
        }

        public void Show(RectTransform anchor, bool showKick, bool showLeave, long userId, Action<long> onKick, Action onLeave, bool showTransferLeader = false, Action<long> onTransferLeader = null)
        {
            _userId = userId;
            _onKick = onKick;
            _onLeave = onLeave;
            _onTransferLeader = onTransferLeader;

            if (btn_ViewProfile != null) btn_ViewProfile.gameObject.SetActive(true);
            if (btn_TransferLeader != null) btn_TransferLeader.gameObject.SetActive(showTransferLeader);
            if (btn_Kick != null) btn_Kick.gameObject.SetActive(showKick);
            if (btn_Leave != null) btn_Leave.gameObject.SetActive(showLeave);

            UIContextMenuRegistry.CloseAllExcept(this);
            
            transform.SetAsLastSibling();
            if (panel != null) panel.transform.SetAsLastSibling();
            if (backdrop != null) backdrop.gameObject.SetActive(true);
            if (panel != null) panel.gameObject.SetActive(true);
            
            if (anchor != null && anchor.rect.height > 150)
            {
                UIContextMenuPositioner.PlaceAtMouse(panel);
            }
            else if (anchor != null)
            {
                UIContextMenuPositioner.PlaceNearTopLeft(panel, anchor);
            }
            else
            {
                UIContextMenuPositioner.PlaceAtMouse(panel);
            }
        }

        public void Hide()
        {
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            if (panel != null) panel.gameObject.SetActive(false);
            _onKick = null;
            _onTransferLeader = null;
            _onLeave = null;
            _userId = 0;
        }

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        private void OnViewProfile() { var id = _userId; Hide(); PlayerProfilePanel.Instance?.Show(id); }
        private void OnTransferLeader() { var cb = _onTransferLeader; var id = _userId; Hide(); cb?.Invoke(id); }
        private void OnKick() { var cb = _onKick; var id = _userId; Hide(); cb?.Invoke(id); }
        private void OnLeave() { var cb = _onLeave; Hide(); cb?.Invoke(); }
    }
}
