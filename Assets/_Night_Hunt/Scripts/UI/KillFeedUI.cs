using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// Kill feed UI showing kill/assist notifications
    /// </summary>
    public class KillFeedUI : MonoBehaviour
    {
        [Header("Kill Feed Settings")] [SerializeField]
        private Transform killFeedParent;

        [SerializeField] private GameObject killFeedItemPrefab;
        [SerializeField] private int maxItems = 5;
        [SerializeField] private float itemLifetime = 5f;

        [Header("Colors")] [SerializeField] private Color killColor = Color.red;
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
            KillFeedItem item = ComponentResolver.Find<KillFeedItem>(itemObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] KillFeedItem not found")
                .Resolve();

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

            //Activate the item gameobject after setup to avoid showing uninitialized values
            item.gameObject.SetActive(true);

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

            CanvasGroup canvasGroup = ComponentResolver.Find<CanvasGroup>(item)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] CanvasGroup not found")
                .Resolve();
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
    /// Kill feed type
    /// </summary>
    public enum KillFeedType
    {
        Kill,
        Assist,
        Death
    }
}