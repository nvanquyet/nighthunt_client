using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

namespace Michsky.UI.Shift
{
    public class CustomInputField : MonoBehaviour, IPointerClickHandler
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

        void Start()
        {
            inputFieldAnimator = gameObject.GetComponent<Animator>();
            inputText = gameObject.GetComponent<TMP_InputField>();

            // Check if text is empty or not
            if (inputText.text.Length == 0 || inputText.text.Length <= 0)
                isEmpty = true;

            else
                isEmpty = false;

            // Animate if it's empty
            if (isEmpty == true)
                inputFieldAnimator.Play(outAnim);

            else
                inputFieldAnimator.Play(inAnim);
        }

        void Update()
        {
            bool hasText = inputText.text.Length > 0;

            // Only change animator state when it actually transitions, not every frame.
            if (hasText && isEmpty)
            {
                isEmpty = false;
                inputFieldAnimator.Play(inAnim);
            }
            else if (!hasText && !isEmpty && !isClicked)
            {
                isEmpty = true;
                inputFieldAnimator.Play(outAnim);
            }
        }

        public void Animate()
        {
            isClicked = true;
            inputFieldAnimator.Play(inAnim);
            fieldTrigger.SetActive(true);

            // On mobile the fieldTrigger overlay intercepts touch events so
            // TMP_InputField never receives focus on its own — force-activate it.
            inputText.ActivateInputField();
        }

        public void FieldTrigger()
        {
            if (isEmpty == true)
            {
                inputFieldAnimator.Play(outAnim);
                fieldTrigger.SetActive(false);
                isClicked = false;
            }

            else
            {
                fieldTrigger.SetActive(false);
                isClicked = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Animate();
        }
    }
}