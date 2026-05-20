using NightHunt.Audio;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NightHunt.UI
{
    /// <summary>
    /// NightHunt standard button wrapper. 
    /// Automatically adds UIAudioTrigger and provides a central place for analytics or hover logic.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class NH_Button : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ICancelHandler, IDeselectHandler
    {
        [SerializeField] private float hoverScale = 1.035f;
        [SerializeField] private float pressedScale = 0.975f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightedColor = new Color(0.82f, 0.96f, 1f, 1f);
        [SerializeField] private Color pressedColor = new Color(0.62f, 0.86f, 1f, 1f);

        private Button _button;
        private Graphic _graphic;
        private UIAudioTrigger _audioTrigger;
        private Vector3 _initialScale;
        private bool _initialScaleCaptured;
        private bool _hovering;
        private bool _pressed;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _graphic = _button.targetGraphic != null
                ? _button.targetGraphic
                : GetComponent<Graphic>() ?? GetComponentInChildren<Graphic>(true);
            if (_button.targetGraphic == null && _graphic != null)
                _button.targetGraphic = _graphic;
            if (_graphic != null)
                _graphic.raycastTarget = true;

            _initialScale = transform.localScale;
            _initialScaleCaptured = true;
            EnsureButtonTransition();
            
            // Ensure UIAudioTrigger exists
            _audioTrigger = GetComponent<UIAudioTrigger>();
            if (_audioTrigger == null)
                _audioTrigger = gameObject.AddComponent<UIAudioTrigger>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanAnimate()) return;
            _hovering = true;
            if (!_pressed)
                ResetVisual(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            if (!_pressed)
                ResetVisual(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanAnimate()) return;
            _pressed = true;
            transform.localScale = _initialScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            _hovering = IsPointerInside(eventData);
            ResetVisual(_hovering && CanAnimate());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_button != null && _button.interactable)
            {
                // Analytics or global click logging could go here
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            ClearInteractionState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            ClearInteractionState();
        }

        private void OnDisable()
        {
            ClearInteractionState();
        }

        private bool CanAnimate()
        {
            return _button != null && _button.interactable && gameObject.activeInHierarchy;
        }

        private void ClearInteractionState()
        {
            _hovering = false;
            _pressed = false;
            ResetVisual(false);
        }

        private void ResetVisual(bool keepHover)
        {
            if (!_initialScaleCaptured)
            {
                _initialScale = transform.localScale;
                _initialScaleCaptured = true;
            }

            transform.localScale = keepHover ? _initialScale * hoverScale : _initialScale;
        }

        private bool IsPointerInside(PointerEventData eventData)
        {
            var rectTransform = transform as RectTransform;
            return rectTransform != null
                   && eventData != null
                   && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, eventData.enterEventCamera);
        }

        private void EnsureButtonTransition()
        {
            if (_button == null || _button.transition != Selectable.Transition.None)
                return;

            _button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = _button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = pressedColor;
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.6f, 0.65f, 0.45f);
            colors.fadeDuration = 0.08f;
            _button.colors = colors;
        }
        
        // Helper to add listeners in code
        public void AddListener(UnityEngine.Events.UnityAction action)
        {
            if (_button != null) _button.onClick.AddListener(action);
        }

        public void RemoveListener(UnityEngine.Events.UnityAction action)
        {
            if (_button != null) _button.onClick.RemoveListener(action);
        }
    }
}
