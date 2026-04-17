using System;
using System.Collections;
using NightHunt.Core;
using Michsky.UI.Shift;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace NightHunt.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Serializable slot: một button = MainButton (text sync) + UnityEvent
    /// persistent (Inspector) + runtime Action (code).
    ///
    /// Không bao giờ gọi RemoveAllListeners trên onClicked.
    /// Runtime callback được swap qua _runtimeBtnX mỗi lần Show*().
    /// </summary>
    // ══════════════════════════════════════════════════════════════════════════
    [Serializable]
    public class ModalButtonSlot
    {
        [Tooltip("MainButton component — manage text cho cả 3 trạng thái (normal/highlighted/pressed).")]
        public MainButton mainButton;

        [Space(4)]
        [Tooltip("Persistent events — wired trong Inspector. Không bị xóa giữa các lần Show*().")]
        public UnityEvent onClicked = new UnityEvent();

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Đặt label và sync ngay sang cả 3 TMP text.
        /// Shift MainButton.Start() đọc buttonText một lần — sau đó không reactive.
        /// Ghi thẳng vào normalText/highlightedText/pressedText để đảm bảo display đúng
        /// dù useCustomText = true hay false.</summary>
        public void SetLabel(string text)
        {
            if (mainButton == null) return;
            mainButton.buttonText = text;
            // Always write directly to TMP fields — MainButton.Start() is not reactive.
            if (mainButton.normalText      != null) mainButton.normalText.text      = text;
            if (mainButton.highlightedText != null) mainButton.highlightedText.text = text;
            if (mainButton.pressedText     != null) mainButton.pressedText.text     = text;
        }

        public void SetVisible(bool visible)
            => mainButton?.gameObject.SetActive(visible);

        public bool IsVisible
            => mainButton != null && mainButton.gameObject.activeSelf;
    }

    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Generic modal — Singleton. Wrap <see cref="ModalWindowManager"/> (Shift)
    /// để tái dùng animation, và dùng <see cref="MainButton"/> (Shift) cho label.
    ///
    /// ── Modes ──────────────────────────────────────────────────────────────
    ///   ShowConfirm   — btn1[Confirm] + btn3[Cancel]
    ///   ShowCountdown — btn1[Confirm] + btn3[Cancel] + auto-expire (hiện trong desc)
    ///   ShowInput     — btn1[Confirm] + btn3[Cancel] + TMP_InputField
    ///   ShowNotice    — btn3[OK] only
    ///   ShowMulti     — btn1[Primary] + btn2[Secondary] + btn3[Dismiss]
    ///
    /// ── Event model ────────────────────────────────────────────────────────
    ///   Mỗi <see cref="ModalButtonSlot"/> chứa:
    ///     • onClicked  — UnityEvent persistent (Inspector, KHÔNG bị xóa)
    ///     • Runtime Action — truyền qua tham số Show*(), được swap mỗi lần gọi
    ///
    ///   Thứ tự khi click:
    ///     1. StopCountdown
    ///     2. Close modal (Shift ModalWindowOut)
    ///     3. slot.onClicked.Invoke()    ← persistent Inspector events
    ///     4. runtimeCallback?.Invoke()  ← code callback
    ///
    /// ── Inspector SETUP ────────────────────────────────────────────────────
    ///   modalManager   — ModalWindowManager (Shift) — animation + title/desc
    ///   btn1Slot       — Primary / Confirm  (OnSuccess)
    ///   btn2Slot       — Secondary          (OnAlternative)
    ///   btn3Slot       — Dismiss / Cancel   (OnFailed / OnCancel)
    ///   inputContainer — wrapper chứa inputField (ẩn khi không dùng input mode)
    ///   inputField     — TMP_InputField
    /// </summary>
    // ══════════════════════════════════════════════════════════════════════════
    public sealed class GameModalWindow : Singleton<GameModalWindow>
    {
        // ══ Inspector ══════════════════════════════════════════════════════════

        [Header("Shift — Modal Manager (animation + title/desc)")]
        [SerializeField] private Michsky.UI.Shift.ModalWindowManager modalManager;

        [Header("Buttons")]
        [SerializeField] private ModalButtonSlot btn1Slot;   // Primary / Confirm
        [SerializeField] private ModalButtonSlot btn2Slot;   // Secondary (3-btn mode)
        [SerializeField] private ModalButtonSlot btn3Slot;   // Dismiss / Cancel / OK

        [Header("Input (optional)")]
        [SerializeField] private GameObject     inputContainer;
        [SerializeField] private TMP_InputField inputField;

        // ══ Runtime ════════════════════════════════════════════════════════════

        private Action          _runtimeBtn1;
        private Action<string>  _runtimeBtn1Input;   // input-mode confirm
        private Action          _runtimeBtn2;
        private Action          _runtimeBtn3;        // dismiss / cancel / expire

        private bool      _isOpen;
        private Coroutine _countdownCoroutine;
        private string    _countdownBaseDesc;        // lưu desc gốc để append countdown
        private long      _activeInvitationId;       // 0 = not tracking; set by ShowCountdown(invitationId)

        // ══ Init ═══════════════════════════════════════════════════════════════

        protected override void OnSingletonAwake()
        {
            // Wire click → internal handler (một lần duy nhất, KHÔNG xóa giữa các Show*)
            GetButton(btn1Slot)?.onClick.AddListener(OnBtn1Clicked);
            GetButton(btn2Slot)?.onClick.AddListener(OnBtn2Clicked);
            GetButton(btn3Slot)?.onClick.AddListener(OnBtn3Clicked);

            // Ngăn ModalWindowManager.Start() ghi đè text của ta
            if (modalManager != null)
                modalManager.useCustomTexts = true;
        }

        // ══ Public API ═════════════════════════════════════════════════════════

        // ── ShowConfirm ────────────────────────────────────────────────────────

        /// <summary>2 buttons: [Confirm] [Cancel].</summary>
        public void ShowConfirm(
            string title,        string desc,
            Action onConfirm   = null,
            Action onCancel    = null,
            string confirmText = "Confirm",
            string cancelText  = "Cancel")
        {
            PrepareContent(title, desc, showInput: false, placeholder: null);
            ConfigureSlots(
                s1: true,  l1: confirmText,
                s2: false, l2: null,
                s3: true,  l3: cancelText);

            _runtimeBtn1      = onConfirm;
            _runtimeBtn1Input = null;
            _runtimeBtn2      = null;
            _runtimeBtn3      = onCancel;
            DoOpen();
        }

        // ── ShowCountdown ──────────────────────────────────────────────────────

        /// <summary>
        /// 2 buttons + auto-expire countdown display ngay trong description.
        /// <paramref name="onExpire"/> bắn khi hết giờ HOẶC khi cancel/dismiss được nhấn.
        /// </summary>
        public void ShowCountdown(
            string title,      string desc,
            int    seconds,
            Action onConfirm    = null,
            Action onExpire     = null,
            bool   showConfirm  = true,
            string confirmText  = "X\u00e1c nh\u1eadn",
            string cancelText   = "H\u1ee7y",
            long   invitationId = 0)
        {
            PrepareContent(title, desc, showInput: false, placeholder: null);
            ConfigureSlots(
                s1: showConfirm, l1: confirmText,
                s2: false,       l2: null,
                s3: true,        l3: cancelText);

            _runtimeBtn1       = onConfirm;
            _runtimeBtn1Input  = null;
            _runtimeBtn2       = null;
            _runtimeBtn3       = onExpire;
            _countdownBaseDesc = desc;
            _activeInvitationId = invitationId;
            StartCountdown(seconds);
            DoOpen();
        }

        /// <summary>Close the modal only if it was opened for the given invitation ID.</summary>
        public void DismissIfMatchingInvitation(long invitationId)
        {
            if (_activeInvitationId != 0 && _activeInvitationId == invitationId)
                Close();
        }

        // ── ShowInput ──────────────────────────────────────────────────────────

        /// <summary>2 buttons + text input field (e.g. nhập mật khẩu).</summary>
        public void ShowInput(
            string         title,        string desc,
            string         placeholder,
            Action<string> onConfirm   = null,
            Action         onCancel    = null,
            string         confirmText = "Confirm",
            string         cancelText  = "Cancel")
        {
            PrepareContent(title, desc, showInput: true, placeholder);
            ConfigureSlots(
                s1: true,  l1: confirmText,
                s2: false, l2: null,
                s3: true,  l3: cancelText);

            _runtimeBtn1      = null;
            _runtimeBtn1Input = onConfirm;
            _runtimeBtn2      = null;
            _runtimeBtn3      = onCancel;
            DoOpen();
        }

        // ── ShowNotice ─────────────────────────────────────────────────────────

        /// <summary>1 button: [OK] — notification đơn giản.</summary>
        public void ShowNotice(
            string title,     string desc,
            string closeText = "OK",
            Action onClose   = null)
        {
            PrepareContent(title, desc, showInput: false, placeholder: null);
            ConfigureSlots(
                s1: false, l1: null,
                s2: false, l2: null,
                s3: true,  l3: closeText);

            _runtimeBtn1      = null;
            _runtimeBtn1Input = null;
            _runtimeBtn2      = null;
            _runtimeBtn3      = onClose;
            DoOpen();
        }

        // ── ShowMulti ──────────────────────────────────────────────────────────

        /// <summary>
        /// 3 buttons. Ví dụ: [Đăng xuất] [Quit Game] [Cancel].
        /// </summary>
        public void ShowMulti(
            string title,  string desc,
            string btn1Text, Action btn1Callback,
            string btn2Text, Action btn2Callback,
            string dismissText     = "Cancel",
            Action dismissCallback = null)
        {
            PrepareContent(title, desc, showInput: false, placeholder: null);
            ConfigureSlots(
                s1: true, l1: btn1Text,
                s2: true, l2: btn2Text,
                s3: true, l3: dismissText);

            _runtimeBtn1      = btn1Callback;
            _runtimeBtn1Input = null;
            _runtimeBtn2      = btn2Callback;
            _runtimeBtn3      = dismissCallback;
            DoOpen();
        }

        // ── Close (silent — không fire callback) ──────────────────────────────

        /// <summary>
        /// Close modal không kích hoạt bất kỳ callback nào.
        /// Uses khi external event đã resolve (vd: swap request được chấp nhận từ xa).
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _activeInvitationId = 0;
            StopCountdown();
            modalManager?.ModalWindowOut();
        }

        public bool IsOpen => _isOpen;

        // ══ Internal ═══════════════════════════════════════════════════════════

        private void PrepareContent(
            string title,  string desc,
            bool   showInput, string placeholder)
        {
            StopCountdown();

            // Đẩy text trực tiếp vào TMP fields (useCustomTexts = true nên Start() không ghi đè)
            if (modalManager != null)
            {
                if (modalManager.windowTitle       != null) modalManager.windowTitle.text       = title;
                if (modalManager.windowDescription != null) modalManager.windowDescription.text = desc;
            }

            if (inputContainer != null) inputContainer.SetActive(showInput);
            if (inputField != null)
            {
                inputField.text = "";
                if (showInput && placeholder != null)
                {
                    var ph = inputField.placeholder as TextMeshProUGUI;
                    if (ph != null) ph.text = placeholder;
                }
            }
        }

        private void ConfigureSlots(
            bool s1, string l1,
            bool s2, string l2,
            bool s3, string l3)
        {
            btn1Slot?.SetVisible(s1);
            btn2Slot?.SetVisible(s2);
            btn3Slot?.SetVisible(s3);

            if (s1 && l1 != null) btn1Slot?.SetLabel(l1);
            if (s2 && l2 != null) btn2Slot?.SetLabel(l2);
            if (s3 && l3 != null) btn3Slot?.SetLabel(l3);
        }

        private void DoOpen()
        {
            if (modalManager == null) return;
            if (_isOpen) modalManager.ModalWindowOut(); // reset animation nếu đang mở
            _isOpen = true;
            modalManager.ModalWindowIn();
        }

        // ── Click handlers ─────────────────────────────────────────────────────

        private void OnBtn1Clicked()
        {
            StopCountdown();
            var c1   = _runtimeBtn1;
            var c1i  = _runtimeBtn1Input;
            var inp  = inputField?.text ?? "";
            var slot = btn1Slot;

            Close();

            slot?.onClicked.Invoke();          // persistent Inspector event trước
            if (c1i != null) c1i.Invoke(inp);  // runtime: input mode
            else             c1?.Invoke();      // runtime: normal mode
        }

        private void OnBtn2Clicked()
        {
            StopCountdown();
            var cb   = _runtimeBtn2;
            var slot = btn2Slot;

            Close();

            slot?.onClicked.Invoke();
            cb?.Invoke();
        }

        private void OnBtn3Clicked()
        {
            StopCountdown();
            var cb   = _runtimeBtn3;
            var slot = btn3Slot;

            Close();

            slot?.onClicked.Invoke();
            cb?.Invoke();
        }

        // ── Countdown ──────────────────────────────────────────────────────────

        private void StartCountdown(int seconds)
        {
            StopCountdown();
            _countdownCoroutine = StartCoroutine(CountdownRoutine(seconds));
        }

        private void StopCountdown()
        {
            if (_countdownCoroutine == null) return;
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        private IEnumerator CountdownRoutine(int seconds)
        {
            for (int r = seconds; r > 0; r--)
            {
                if (!_isOpen) yield break;
                if (modalManager?.windowDescription != null)
                    modalManager.windowDescription.text = $"{_countdownBaseDesc}\n\n{r}s";
                yield return new WaitForSecondsRealtime(1f);
            }

            if (!_isOpen) yield break;
            if (modalManager?.windowDescription != null)
                modalManager.windowDescription.text = $"{_countdownBaseDesc}\n\n0s";
            yield return new WaitForSecondsRealtime(0.2f);
            if (!_isOpen) yield break;

            // Auto-expire — hành xử giống nhấn btn3 (Cancel)
            _countdownCoroutine = null;
            OnBtn3Clicked();
        }

        // ── Utility ────────────────────────────────────────────────────────────

        private static UnityEngine.UI.Button GetButton(ModalButtonSlot slot)
            => slot?.mainButton?.GetComponent<UnityEngine.UI.Button>();
    }
}