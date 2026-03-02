using System;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// UINotificationService — single unified access point for all user-facing notifications.
    ///
    /// Replaces the duplicated pattern spread across every View:
    ///   • LobbyView.ShowError()          → local errorText or NoticePopup
    ///   • LobbyView.ShowErrorViaNotice() → PersistentUICanvas.Instance.NoticePopup
    ///   • HomeView loading access        → PersistentUICanvas.Instance.LoadingManager
    ///   • LoginView.ShowError()          → local errorText
    ///
    /// All Views should call UINotificationService.Instance.Toast / Notice / Loading
    /// and the service decides which backend to use (Toast vs popup).
    /// The concrete backend (ToastService, NoticePopup, LoadingManager) is assigned
    /// by PersistentUICanvas when it initialises.
    ///
    ///  ─────────────────────────────────────────────────
    ///  View code                  UINotificationService
    ///  ─────────────────────────────────────────────────
    ///  .Toast(msg)           →    ToastService.Show
    ///  .ToastError(msg)      →    ToastService.ShowError
    ///  .ToastSuccess(msg)    →    ToastService.ShowSuccess
    ///  .Notice(title, msg)   →    NoticePopup.Show  (modal, blocks input)
    ///  .ShowLoading(msg)     →    LoadingManager.Show
    ///  .HideLoading()        →    LoadingManager.Hide
    ///  ─────────────────────────────────────────────────
    ///
    /// Setup:
    ///   1. Attach this component to the PersistentUICanvas GameObject (or its own persistent GO).
    ///   2. In PersistentUICanvas.OnPersistentAwake, call:
    ///        UINotificationService.Instance.Configure(toastService, noticePopup, loadingManager);
    /// </summary>
    public class UINotificationService : SingletonPersistent<UINotificationService>
    {
        // ── Backends (wired at runtime by PersistentUICanvas) ─────────────────

        private ToastService    _toast;
        private NoticePopup     _notice;
        private LoadingManager  _loading;

        // ── Configuration ─────────────────────────────────────────────────────

        /// <summary>
        /// Called once by PersistentUICanvas after its sub-components are ready.
        /// All three parameters are optional; pass null to skip a backend.
        /// </summary>
        public void Configure(ToastService toast, NoticePopup notice, LoadingManager loading)
        {
            _toast   = toast;
            _notice  = notice;
            _loading = loading;
        }

        // ── Toast API (non-blocking, auto-dismissing) ─────────────────────────

        /// <summary>Show a neutral informational toast.</summary>
        public void Toast(string message, float duration = -1f)
        {
            if (_toast != null) { _toast.Show(message, duration); return; }
            Debug.Log($"[UINotification] TOAST: {message}");
        }

        /// <summary>Show a red error toast.</summary>
        public void ToastError(string message, float duration = -1f)
        {
            if (_toast != null) { _toast.ShowError(message, duration); return; }
            Debug.LogWarning($"[UINotification] ERROR: {message}");
        }

        /// <summary>Show a green success toast.</summary>
        public void ToastSuccess(string message, float duration = -1f)
        {
            if (_toast != null) { _toast.ShowSuccess(message, duration); return; }
            Debug.Log($"[UINotification] SUCCESS: {message}");
        }

        // ── Notice API (blocking modal popup) ─────────────────────────────────

        /// <summary>
        /// Show a blocking modal popup with OK button.
        /// Use for force-logout, session expired, or errors that require acknowledgement.
        /// </summary>
        /// <param name="title">Bold title line (e.g. "Session Expired").</param>
        /// <param name="message">Body text with full details.</param>
        /// <param name="onConfirm">Optional callback when OK is pressed.</param>
        /// <param name="autoDismissSeconds">0 = no auto-dismiss; &gt;0 = dismiss after N seconds.</param>
        public void Notice(string title, string message, Action onConfirm = null, float autoDismissSeconds = 0f)
        {
            if (_notice != null)
            {
                _notice.Show(title, message, onConfirm, autoDismissSeconds);
                return;
            }
            // Fallback: toast so the player still sees something
            ToastError($"{title}: {message}");
            Debug.LogWarning($"[UINotification] NOTICE (no popup backend): {title} — {message}");
        }

        /// <summary>
        /// Convenience: show an error notice using "Error" as the title.
        /// Commonly used for AUTH_FORCE_LOGOUT, session expiry, server errors.
        /// </summary>
        public void NoticeError(string message, Action onConfirm = null)
            => Notice("Error", message, onConfirm);

        // ── Loading API ───────────────────────────────────────────────────────

        /// <summary>Show the full-screen loading overlay.</summary>
        /// <param name="message">Optional override text (null = keep default label).</param>
        public void ShowLoading(string message = null)
        {
            if (_loading != null) { _loading.Show(message); return; }
            Debug.Log($"[UINotification] LOADING: {message ?? "..."}");
        }


        /// <summary>Hide the full-screen loading overlay.</summary>
        public void HideLoading()
        {
            if (_loading != null) { _loading.Hide(); return; }
        }

        /// <summary>Returns true if the loading overlay is currently showing.</summary>
        public bool IsLoading => _loading != null && _loading.IsShowing();
    }
}
