using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP; // ← thêm namespace MUIP

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
        [SerializeField] private ProgressBar _progressBar; // ← thay Slider
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
            if (_activeKind != ActionProgressKind.None && KindPriority(_activeKind) > KindPriority(kind))
                return;

            _activeKind = kind;
            _onCancel = onCancel;

            if (_root != null)
                _root.SetActive(true);

            if (_progressBar != null)
            {
                _progressBar.currentPercent = 0f; // ← reset về 0 (thang 0–100)
                _progressBar.isOn = true;         // ← bật counting
            }

            if (_label != null)
                _label.text = label ?? string.Empty;

            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(cancellable && onCancel != null);
        }

        public void SetProgress(ActionProgressKind kind, float progress)
        {
            if (_activeKind != kind)
                return;

            if (_progressBar != null)
                // Slider dùng 0–1, ProgressBar dùng 0–100
                _progressBar.currentPercent = Mathf.Clamp01(progress) * 100f;
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

            if (_progressBar != null)
            {
                _progressBar.currentPercent = 0f;
                _progressBar.isOn = false; // ← tắt counting khi ẩn
            }

            if (_cancelButton != null)
                _cancelButton.gameObject.SetActive(false);
        }

        private void HandleCancelClicked()
        {
            _onCancel?.Invoke();
        }
    }
}