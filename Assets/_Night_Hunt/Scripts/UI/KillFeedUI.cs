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

            // Ensure this panel is visible — kill events fire even while settings/overlay is open.
            if (!gameObject.activeInHierarchy)
                gameObject.SetActive(true);

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

            // Auto remove after lifetime — runs on the item itself so it survives panel hide/show.
            item.FadeOutAndDestroy(itemLifetime);
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

        // Fade-out and item lifetime are now handled by KillFeedItem.FadeOutAndDestroy().

#if UNITY_EDITOR
        // ── Editor — Context Menu: Create KillFeedItem Template Prefab ────────

        [ContextMenu("NightHunt/Create KillFeedItem Template Prefab")]
        private void Editor_CreateKillFeedItemPrefab()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            const string path = dir + "/KillFeedItem_Template.prefab";

            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[KillFeedUI] KillFeedItem_Template already exists at {path}");
                return;
            }

            // Root
            var go = new GameObject("KillFeedItem_Template");
            go.AddComponent<CanvasGroup>();
            var rootRt = go.AddComponent<UnityEngine.RectTransform>();
            rootRt.sizeDelta = new UnityEngine.Vector2(300f, 30f);
            // KillFeedItem script stub (will be set by user)
            // Horizontal layout
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth  = true;
            hlg.childControlHeight = true;
            hlg.spacing            = 4f;
            // Killer label
            var killerGo  = new GameObject("KillerText");
            killerGo.transform.SetParent(go.transform, false);
            var killerTmp = killerGo.AddComponent<TMPro.TextMeshProUGUI>();
            killerTmp.text      = "PlayerA";
            killerTmp.fontSize  = 12f;
            killerTmp.color     = UnityEngine.Color.white;
            // Separator
            var sepGo  = new GameObject("SepText");
            sepGo.transform.SetParent(go.transform, false);
            var sepTmp = sepGo.AddComponent<TMPro.TextMeshProUGUI>();
            sepTmp.text     = "→";
            sepTmp.fontSize = 12f;
            // Victim label
            var victimGo  = new GameObject("VictimText");
            victimGo.transform.SetParent(go.transform, false);
            var victimTmp = victimGo.AddComponent<TMPro.TextMeshProUGUI>();
            victimTmp.text     = "PlayerB";
            victimTmp.fontSize = 12f;
            victimTmp.color    = UnityEngine.Color.red;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (killFeedItemPrefab == null)
            {
                killFeedItemPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[KillFeedUI] Created KillFeedItem_Template at {path}. " +
                      "Add KillFeedItem component, then assign to killFeedItemPrefab.");
        }
#endif
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