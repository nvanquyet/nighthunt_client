using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Michsky.UI.Shift
{
    public class CustomInputField : MonoBehaviour, IPointerClickHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Resources")]
        public GameObject fieldTrigger;
        private TMP_InputField inputText;
        private Animator inputFieldAnimator;

        // [Header("Settings")]
        bool isEmpty = true;
        bool isClicked = false;
        string inAnim = "In";
        string outAnim = "Out";

        void Awake()
        {
            inputFieldAnimator = gameObject.GetComponent<Animator>();
            inputText = gameObject.GetComponent<TMP_InputField>();
            SanitizeFieldTrigger();
            DisableFieldTrigger();
        }

        void OnEnable()
        {
            // Reset click state so keyboard can be re-opened after panel hide/show.
            isClicked = false;
            SanitizeFieldTrigger();
            DisableFieldTrigger();

            // Deactivate any stale focus so the field resets cleanly on mobile.
            if (inputText != null && inputText.isFocused)
                inputText.DeactivateInputField();
        }

        void Start()
        {
            RefreshVisualState();
        }

        void Update()
        {
            if (inputText == null)
                return;

            bool hasText = inputText.text.Length > 0;

            if (hasText && isEmpty)
            {
                isEmpty = false;
                PlayState(inAnim);
            }
            else if (!hasText && !isEmpty && !isClicked && !inputText.isFocused)
            {
                isEmpty = true;
                PlayState(outAnim);
            }
        }

        public void Animate()
        {
            FocusInput();
        }

        public void FieldTrigger()
        {
            DisableFieldTrigger();

            if (inputText != null && inputText.isFocused)
                return;

            isClicked = false;
            RefreshVisualState();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            FocusInput();
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocusedVisual();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isClicked = false;
            DisableFieldTrigger();
            RefreshVisualState();
        }

        private void FocusInput()
        {
            if (inputText == null)
                inputText = gameObject.GetComponent<TMP_InputField>();

            SetFocusedVisual();
            DisableFieldTrigger();

            if (inputText == null || !inputText.interactable)
                return;

            inputText.Select();
            inputText.ActivateInputField();

#if UNITY_ANDROID || UNITY_IOS
            StartCoroutine(ReinforceMobileFocus());
#endif
        }

        private IEnumerator ReinforceMobileFocus()
        {
#if UNITY_ANDROID || UNITY_IOS
            // Wait one frame for Unity's EventSystem to settle after Select().
            yield return null;

            if (inputText == null || !inputText.isActiveAndEnabled || !inputText.interactable)
                yield break;

            // FIX: compare against inputText.gameObject, not this gameObject.
            // Previously used 'gameObject' which is CustomInputField's GO — these may differ
            // from TMP_InputField's GO causing the condition to always be false → keyboard never shown.
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject != inputText.gameObject)
            {
                inputText.Select();
            }

            inputText.ActivateInputField();

            // Extra frame retry — some Android devices need a second pulse to open keyboard.
            yield return null;

            if (inputText != null && inputText.isActiveAndEnabled && inputText.interactable
                && !TouchScreenKeyboard.visible)
            {
                inputText.ActivateInputField();
            }
#else
            yield break;
#endif
        }

        private void SetFocusedVisual()
        {
            isClicked = true;
            isEmpty = false;
            PlayState(inAnim);
        }

        private void RefreshVisualState()
        {
            if (inputText == null)
                inputText = gameObject.GetComponent<TMP_InputField>();

            bool hasText = inputText != null && inputText.text.Length > 0;
            isEmpty = !hasText;

            if (hasText || isClicked || (inputText != null && inputText.isFocused))
                PlayState(inAnim);
            else
                PlayState(outAnim);
        }

        private void PlayState(string stateName)
        {
            if (inputFieldAnimator == null)
                inputFieldAnimator = gameObject.GetComponent<Animator>();

            if (inputFieldAnimator != null && !string.IsNullOrEmpty(stateName))
                inputFieldAnimator.Play(stateName);
        }

        private void DisableFieldTrigger()
        {
            if (fieldTrigger != null && fieldTrigger.activeSelf)
                fieldTrigger.SetActive(false);
        }

        private void SanitizeFieldTrigger()
        {
            if (fieldTrigger == null)
                return;

            if (fieldTrigger.TryGetComponent<Graphic>(out var graphic))
                graphic.raycastTarget = false;

            if (fieldTrigger.TryGetComponent<EventTrigger>(out var eventTrigger))
                eventTrigger.enabled = false;
        }
    }
}
