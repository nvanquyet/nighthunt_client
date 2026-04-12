using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NightHunt.Audio
{
    /// <summary>
    /// UIAudioTrigger — attach to any Button or Selectable to play UI sounds via AudioManager.
    ///
    /// SETUP MODES:
    ///   1. Add component directly to a Button GameObject.
    ///   2. Or use the [AddMenu] helper to batch-add to all buttons in a panel.
    ///
    /// AUTO-WIRING:
    ///   If a Button component is on the same GO, UIAudioTrigger automatically subscribes
    ///   to Button.onClick for the click sound. No code needed.
    ///
    /// HOVER SOUND:
    ///   Implements IPointerEnterHandler — fires hover when pointer enters the button rect.
    ///   Works on both mouse (PC) and EventSystem navigation (gamepad).
    ///
    /// Override sounds per-button:
    ///   Leave overrideClickClip null to use AudioLibrary.uiClick (default).
    ///   Assign a custom clip for special buttons (e.g., Play button = bigger confirm sound).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIAudioTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("Sound Overrides (leave null = use AudioLibrary default)")]
        [Tooltip("Custom click sound for this button. null = AudioLibrary.uiClick.")]
        [SerializeField] private AudioClip overrideClickClip;

        [Tooltip("Custom hover sound. null = AudioLibrary.uiHover.")]
        [SerializeField] private AudioClip overrideHoverClip;

        [Header("Options")]
        [Tooltip("Play hover sound when pointer enters this element.")]
        [SerializeField] private bool playHover = true;

        [Tooltip("Play click sound on pointer click (in addition to Button.onClick auto-wiring).")]
        [SerializeField] private bool playClick = true;

        [Tooltip("Volume multiplier for sounds on this specific button (0–2).")]
        [SerializeField, Range(0f, 2f)] private float volumeMultiplier = 1f;

        // ── Auto-wire to Button ─────────────────────────────────────────────

        private void Awake()
        {
            // Auto-subscribe to Button.onClick so click fires even if triggered by code
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.RemoveListener(OnButtonClicked);
        }

        // ── Event System Handlers ─────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!playHover) return;
            if (!AudioManager.HasInstance) return;

            AudioClip clip = overrideHoverClip ?? AudioManager.Instance.Library?.uiHover;
            AudioManager.Instance.PlayUI(clip, 0.6f * volumeMultiplier);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // IPointerClickHandler fires on all clicks — primary left button only
            if (eventData.button != PointerEventData.InputButton.Left) return;
            TriggerClick();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Manually trigger click sound from code (e.g. keyboard nav confirm).</summary>
        public void TriggerClick()
        {
            if (!playClick) return;
            if (!AudioManager.HasInstance) return;

            AudioClip clip = overrideClickClip ?? AudioManager.Instance.Library?.uiClick;
            AudioManager.Instance.PlayUI(clip, volumeMultiplier);
        }

        // ── Button.onClick callback ───────────────────────────────────────────

        private void OnButtonClicked()
        {
            // Fired when Button.onClick.Invoke() is called programmatically (bypasses
            // the EventSystem, so OnPointerClick never fires in that path).
            // Guard: avoid double-play when a real pointer click comes through
            // (OnPointerClick already fired first).  We detect a pointer click in the
            // same frame by checking whether EventSystem.currentInputModule just processed
            // a pointer event. A simple IsPointerOverGameObject check is sufficient here.
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return; // real click → OnPointerClick already handled sound

            TriggerClick();
        }
    }
}
