using System.Collections;
using UnityEngine;
using TMPro;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Kill feed item component
    /// </summary>
    public class KillFeedItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        /// <summary>
        /// Fade out then destroy this item after <paramref name="delay"/> seconds.
        /// Runs on this GameObject so it works regardless of parent panel active state.
        /// </summary>
        public void FadeOutAndDestroy(float delay, float fadeTime = 0.5f)
        {
            StartCoroutine(FadeCoroutine(delay, fadeTime));
        }

        private IEnumerator FadeCoroutine(float delay, float fadeTime)
        {
            yield return new WaitForSeconds(delay);

            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                cg.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }

            Destroy(gameObject);
        }

        public void Initialize(string actorName, string targetName, string weaponName, KillFeedType type, Color color)
        {
            if (_text == null)
                _text = ComponentResolver.Find<TextMeshProUGUI>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] TextMeshProUGUI not found")
        .Resolve();

            if (_text != null)
            {
                string message = FormatMessage(actorName, targetName, weaponName, type);
                _text.text = message;
                _text.color = color;
            }
        }

        private string FormatMessage(string actor, string target, string weapon, KillFeedType type)
        {
            switch (type)
            {
                case KillFeedType.Kill:
                    if (string.IsNullOrEmpty(weapon))
                    {
                        return $"{actor} killed {target}";
                    }
                    return $"{actor} killed {target} with {weapon}";

                case KillFeedType.Assist:
                    return $"{actor} assisted in killing {target}";

                case KillFeedType.Death:
                    if (string.IsNullOrEmpty(actor))
                    {
                        return $"{target} died";
                    }
                    return $"{target} was killed by {actor}";

                default:
                    return "";
            }
        }
    }
}
