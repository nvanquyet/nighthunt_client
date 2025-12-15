using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

namespace NightHunt.UI
{
    /// <summary>
    /// Kill feed UI showing kill/assist notifications
    /// </summary>
    public class KillFeedUI : MonoBehaviour
    {
        [Header("Kill Feed Settings")]
        [SerializeField] private Transform killFeedParent;
        [SerializeField] private GameObject killFeedItemPrefab;
        [SerializeField] private int maxItems = 5;
        [SerializeField] private float itemLifetime = 5f;

        [Header("Colors")]
        [SerializeField] private Color killColor = Color.red;
        [SerializeField] private Color assistColor = Color.yellow;
        [SerializeField] private Color deathColor = Color.gray;

        private Queue<KillFeedItem> killFeedItems = new Queue<KillFeedItem>();

        /// <summary>
        /// Add kill notification
        /// </summary>
        public void AddKill(string killerName, string victimName, string weaponName = "")
        {
            AddKillFeedItem(killerName, victimName, weaponName, KillFeedType.Kill);
        }

        /// <summary>
        /// Add assist notification
        /// </summary>
        public void AddAssist(string assistName, string victimName)
        {
            AddKillFeedItem(assistName, victimName, "", KillFeedType.Assist);
        }

        /// <summary>
        /// Add death notification
        /// </summary>
        public void AddDeath(string victimName, string killerName = "")
        {
            if (string.IsNullOrEmpty(killerName))
            {
                AddKillFeedItem("", victimName, "", KillFeedType.Death);
            }
            else
            {
                AddKillFeedItem(killerName, victimName, "", KillFeedType.Death);
            }
        }

        /// <summary>
        /// Add kill feed item
        /// </summary>
        private void AddKillFeedItem(string actorName, string targetName, string weaponName, KillFeedType type)
        {
            if (killFeedItemPrefab == null || killFeedParent == null)
                return;

            GameObject itemObj = Instantiate(killFeedItemPrefab, killFeedParent);
            KillFeedItem item = itemObj.GetComponent<KillFeedItem>();

            if (item == null)
            {
                item = itemObj.AddComponent<KillFeedItem>();
            }

            item.Initialize(actorName, targetName, weaponName, type, GetColorForType(type));
            killFeedItems.Enqueue(item);

            // Remove oldest if over limit
            if (killFeedItems.Count > maxItems)
            {
                var oldest = killFeedItems.Dequeue();
                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }

            // Auto remove after lifetime
            StartCoroutine(RemoveItemAfterDelay(item, itemLifetime));
        }

        /// <summary>
        /// Get color for kill feed type
        /// </summary>
        private Color GetColorForType(KillFeedType type)
        {
            switch (type)
            {
                case KillFeedType.Kill:
                    return killColor;
                case KillFeedType.Assist:
                    return assistColor;
                case KillFeedType.Death:
                    return deathColor;
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Remove item after delay
        /// </summary>
        private IEnumerator RemoveItemAfterDelay(KillFeedItem item, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (item != null)
            {
                // Fade out
                StartCoroutine(FadeOutItem(item));
            }
        }

        /// <summary>
        /// Fade out item
        /// </summary>
        private IEnumerator FadeOutItem(KillFeedItem item)
        {
            if (item == null) yield break;

            CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = item.gameObject.AddComponent<CanvasGroup>();
            }

            float fadeTime = 0.5f;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - (elapsed / fadeTime);
                yield return null;
            }

            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
    }

    /// <summary>
    /// Kill feed item component
    /// </summary>
    public class KillFeedItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;

        public void Initialize(string actorName, string targetName, string weaponName, KillFeedType type, Color color)
        {
            if (text == null)
            {
                text = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (text != null)
            {
                string message = FormatMessage(actorName, targetName, weaponName, type);
                text.text = message;
                text.color = color;
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

    /// <summary>
    /// Kill feed type
    /// </summary>
    public enum KillFeedType
    {
        Kill,
        Assist,
        Death
    }
}

