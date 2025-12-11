using System.Collections;
using TMPro;
using UnityEngine;

namespace NightHunt.UI
{
    /// <summary>
    /// Simple toast service to display transient messages on top of UI.
    /// </summary>
    public class ToastService : MonoBehaviour
    {
        private static ToastService instance;
        public static ToastService Instance => instance != null ? instance : Create();

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI messageText;

        private Coroutine currentRoutine;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                EnsureComponents();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private static ToastService Create()
        {
            var go = new GameObject("ToastService");
            return go.AddComponent<ToastService>();
        }

        private void EnsureComponents()
        {
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9998;
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            if (messageText == null)
            {
                var textGO = new GameObject("Message");
                textGO.transform.SetParent(transform, false);
                messageText = textGO.AddComponent<TextMeshProUGUI>();
                var rect = messageText.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.2f, 0.1f);
                rect.anchorMax = new Vector2(0.8f, 0.2f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                messageText.alignment = TextAlignmentOptions.Center;
                messageText.color = Color.white;
                messageText.fontSize = 28;
            }
        }

        public void Show(string message, float durationSeconds = 2f)
        {
            EnsureComponents();
            messageText.text = message;
            if (currentRoutine != null)
            {
                StopCoroutine(currentRoutine);
            }
            currentRoutine = StartCoroutine(ShowRoutine(durationSeconds));
        }

        private IEnumerator ShowRoutine(float duration)
        {
            canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(duration);
            canvasGroup.alpha = 0f;
        }
    }
}

