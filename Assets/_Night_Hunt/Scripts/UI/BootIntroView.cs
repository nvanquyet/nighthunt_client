using System.Collections;
using NightHunt.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// First-boot intro surface shown before the startup loading overlay.
    /// Logic stays in LoadingManager; this component only owns visuals, timing,
    /// and optional intro sound playback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootIntroView : MonoBehaviour
    {
        private const string RuntimeName = "BootIntroPanel_Runtime";

        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Animator animator;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Text")]
        [SerializeField] private string title = "NIGHT HUNT";
        [SerializeField] private string subtitle = "INITIALIZING";

        [Header("Animation")]
        [SerializeField] private string introInState = "Intro In";
        [SerializeField] private string introOutState = "Intro Out";
        [SerializeField, Min(0f)] private float fadeInDuration = 0.25f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float holdAfterSound = 0.08f;

        [Header("Behaviour")]
        [SerializeField] private bool playSound = true;
        [SerializeField] private bool blockRaycasts = true;
        [SerializeField] private bool hideOnAwake = true;

        private bool _hasPlayed;

        private void Awake()
        {
            EnsureRuntimeWiring();

            if (hideOnAwake)
                HideImmediate();
        }

        public IEnumerator PlayIntro(AudioClip introClip, float minDuration, float maxDuration)
        {
            EnsureRuntimeWiring();

            if (_hasPlayed)
                yield break;

            _hasPlayed = true;

            if (root == null)
                yield break;

            root.SetActive(true);
            root.transform.SetAsLastSibling();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = blockRaycasts;
            }

            ApplyText();
            ShiftUIBridge.PlayAnimatorState(animator, introInState, crossFade: true);
            yield return FadeCanvas(0f, 1f, fadeInDuration);

            if (playSound && introClip != null)
            {
                var audio = AudioManager.Instance;
                if (audio != null)
                    audio.PlayUI(introClip);
            }

            float holdDuration = introClip != null
                ? Mathf.Clamp(introClip.length, minDuration, maxDuration)
                : Mathf.Clamp(minDuration, 0f, maxDuration);

            if (holdDuration > 0f)
                yield return WaitUnscaled(holdDuration + holdAfterSound);

            ShiftUIBridge.PlayAnimatorState(animator, introOutState, crossFade: true);
            yield return FadeCanvas(1f, 0f, fadeOutDuration);
            HideImmediate();
        }

        public void EnsureRuntimeWiring()
        {
            if (root == null)
                root = gameObject;

            if (canvasGroup == null && root != null)
                canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();

            if (animator == null && root != null)
                animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();

            if (backgroundImage == null && root != null)
                backgroundImage = root.GetComponent<Image>() ?? CreateBackground(root.transform);

            if (titleText == null && root != null)
                titleText = FindText("title") ?? CreateText("Title", root.transform, title, 46f, FontStyles.Bold, new Vector2(0f, 22f));

            if (subtitleText == null && root != null)
                subtitleText = FindText("subtitle") ?? CreateText("Subtitle", root.transform, subtitle, 16f, FontStyles.UpperCase, new Vector2(0f, -34f));

            ApplyText();
        }

        public void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (root != null)
                root.SetActive(false);
        }

        public static BootIntroView CreateRuntime(Transform parent)
        {
            if (parent == null)
                return null;

            var host = new GameObject(RuntimeName, typeof(RectTransform));
            host.layer = 5;
            host.transform.SetParent(parent, false);

            var rect = host.GetComponent<RectTransform>();
            StretchFull(rect);

            var view = host.AddComponent<BootIntroView>();
            view.EnsureRuntimeWiring();
            view.HideImmediate();
            return view;
        }

        private void ApplyText()
        {
            if (titleText != null)
                titleText.text = title;
            if (subtitleText != null)
                subtitleText.text = subtitle;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (canvasGroup == null)
                yield break;

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private TMP_Text FindText(string namePart)
        {
            if (root == null)
                return null;

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null && text.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return text;
            }

            return null;
        }

        private static Image CreateBackground(Transform parent)
        {
            var image = parent.gameObject.AddComponent<Image>();
            image.color = new Color(0.015f, 0.018f, 0.024f, 1f);
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            FontStyles style,
            Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 64f);
            rect.anchoredPosition = anchoredPosition;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchFull(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
