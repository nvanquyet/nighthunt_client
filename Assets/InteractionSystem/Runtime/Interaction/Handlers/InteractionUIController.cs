using System.Collections;
using NightHunt.InteractionSystem.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.InteractionSystem.Interaction
{
    public class InteractionUIController : MonoBehaviour
    {
        public static InteractionUIController Instance { get; private set; }

        [Header("UI Elements")] [SerializeField]
        private GameObject promptPanel;

        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private Image progressBar;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation")] [SerializeField] private float fadeSpeed = 5f;

        private IInteractable currentInteractable;
        private HoldInteractionHandler holdHandler;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void ShowPrompt(IInteractable interactable)
        {
            if (interactable == null)
            {
                HidePrompt();
                return;
            }

            currentInteractable = interactable;

            // Update text based on interaction type
            string prompt = interactable.InteractionType switch
            {
                InteractionType.Immediate => $"Press E - {interactable.InteractionPrompt}",
                InteractionType.Hold => $"Hold E - {interactable.InteractionPrompt}",
                InteractionType.Toggle => $"Press E - {interactable.InteractionPrompt}",
                InteractionType.Container => $"Press E - {interactable.InteractionPrompt}",
                _ => interactable.InteractionPrompt
            };

            promptText.text = prompt;

            // Show/hide progress bar
            bool showProgress = interactable.InteractionType == InteractionType.Hold;
            progressBar.gameObject.SetActive(showProgress);

            if (showProgress)
            {
                progressBar.fillAmount = 0f;
            }

            // Fade in
            promptPanel.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        public void HidePrompt()
        {
            currentInteractable = null;
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        public void UpdateProgress(float progress)
        {
            if (progressBar.gameObject.activeSelf)
            {
                progressBar.fillAmount = progress;
            }
        }

        private IEnumerator FadeIn()
        {
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += Time.deltaTime * fadeSpeed;
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            promptPanel.SetActive(false);
        }
    }
}