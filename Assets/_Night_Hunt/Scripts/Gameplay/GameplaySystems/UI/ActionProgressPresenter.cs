using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.GameplaySystems.UI
{
    public enum ActionProgressKind
    {
        None = 0,
        ItemUse = 1,
        Interaction = 2,
        Reload = 3,
        Deployable = 4,
    }

    /// <summary>
    /// Shared HUD presenter for timed actions: item use, hold interaction, reload,
    /// deploy placement confirm, and future channelled actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionProgressPresenter : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject _root;
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _cancelButton;

        private ActionProgressKind _activeKind;
        private Action _onCancel;

        public ActionProgressKind ActiveKind => _activeKind;
        public bool IsVisible => _root != null && _root.activeSelf;

        private void Awake()
        {
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(HandleCancelClicked);

            Hide();
        }

        private void OnDestroy()
        {
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        /// <summary>
        /// Returns the display priority for a given kind.
        /// Higher value = higher priority. A lower-priority Show() will not override a
        /// currently-active higher-priority action (e.g. Reload won't be replaced by ItemUse).
        /// </summary>
        private static int KindPriority(ActionProgressKind k) => k switch
        {
            ActionProgressKind.Interaction => 30,
            ActionProgressKind.Reload      => 20,
            ActionProgressKind.Deployable  => 10,
            ActionProgressKind.ItemUse     => 10,
            _                              => 0,
        };

        public void Show(ActionProgressKind kind, string label, bool cancellable, Action onCancel = null)
        {
            // Do not interrupt an already-active action of strictly higher priority.
            // E.g. a Reload bar must not be replaced mid-progress by an ItemUse bar.
            if (_activeKind != ActionProgressKind.None && KindPriority(_activeKind) > KindPriority(kind))
                return;

            _activeKind = kind;
            _onCancel = onCancel;

            if (_root != null)
                _root.SetActive(true);

            if (_slider != null)
                _slider.value = 0f;

            if (_label != null)
                _label.text = label ?? string.Empty;

            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(cancellable && onCancel != null);
        }

        public void SetProgress(ActionProgressKind kind, float progress)
        {
            if (_activeKind != kind)
                return;

            if (_slider != null)
                _slider.value = Mathf.Clamp01(progress);
        }

        public void Hide(ActionProgressKind kind)
        {
            if (_activeKind == kind)
                Hide();
        }

        public void Hide()
        {
            _activeKind = ActionProgressKind.None;
            _onCancel = null;

            if (_root != null)
                _root.SetActive(false);

            if (_slider != null)
                _slider.value = 0f;

            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(false);
        }

        private void HandleCancelClicked()
        {
            _onCancel?.Invoke();
        }
    }
}
