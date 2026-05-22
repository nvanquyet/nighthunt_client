using System;
using System.Collections.Generic;
using NightHunt.Core;
using UnityEngine;
using Michsky.MUIP;

namespace NightHunt.UI
{
    /// <summary>
    /// ToastService — MUIP NotificationManager pool.
    ///
    /// SETUP REQUIRED (Inspector):
    ///   • prefab    — assign a MUIP NotificationManager prefab.
    ///                 Use Context Menu → "NightHunt/Auto-Assign Toast Prefab" to find it.
    ///                 If null at runtime, Show() degrades gracefully with a warning log.
    ///   • container — RectTransform anchor (e.g. top-right corner of PersistentUICanvas).
    /// </summary>
    public class ToastService : SingletonPersistent<ToastService>
    {
        [SerializeField] private RectTransform container;
        [SerializeField] private NotificationManager prefab;
        [SerializeField] private int maxVisible = 3;
        [SerializeField] private bool verboseFlowLogging = true;

        private readonly List<NotificationManager>          pool      = new();
        private readonly Queue<ToastRequest>                waiting   = new();
        private readonly Dictionary<NotificationManager, Action> callbacks = new();

        private bool _prefabMissing;

        protected override void OnSingletonAwake()
        {
            if (prefab == null)
            {
                _prefabMissing = true;
                Debug.LogError(
                    "[FLOW][TOAST] 'prefab' field is not assigned!\n" +
                    "Toasts will NOT show. Fix: assign a MUIP NotificationManager prefab in Inspector.\n" +
                    "Use Context Menu → 'NightHunt/Auto-Assign Toast Prefab' to auto-find.");
                return;
            }

            if (container == null)
            {
                Debug.LogWarning("[FLOW][TOAST] 'container' RectTransform is not assigned. Toasts will be parented at root.");
            }

            for (int i = 0; i < maxVisible; i++)
            {
                var item = Instantiate(prefab, container);
                // MUIP NotificationManager.startBehaviour defaults to Disable, which means
                // Start() will call SetActive(false) on the NEXT frame after first activation,
                // cancelling the toast. Override to None so pooled items are managed by us.
                item.startBehaviour = NotificationManager.StartBehaviour.None;
                item.gameObject.SetActive(false);
                var captured = item;
                item.onClose.AddListener(() => HandleClose(captured));
                TLog($"Attached onClose listener to {item.name}");
                pool.Add(item);
            }

            TLog($"Initialized pool with {pool.Count} items. container={(container==null?"null":container.name)} canvas={DescribeCanvas(container)}");
        }

        public void Show(string title, string message, Action onConfirm = null)
        {
            if (_prefabMissing)
            {
                Debug.LogWarning($"[FLOW][TOAST] Cannot show toast '{title}' - prefab not assigned.");
                return;
            }

            TLog($"Show requested title='{title}' message='{message}' pool={pool.Count} waiting={waiting.Count}");

            var item = GetAvailableItem();
            if (item == null)
            {
                TLog($"No available item; enqueueing toast '{title}'");
                waiting.Enqueue(new ToastRequest(title, message, onConfirm));
                return;
            }

            ShowItem(item, title, message, onConfirm);
        }

        private NotificationManager GetAvailableItem()
        {
            foreach (var item in pool)
            {
                if (!item.gameObject.activeSelf)
                    return item;
            }
            TLog("GetAvailableItem: none available");
            return null;
        }

        private void ShowItem(NotificationManager item, string title, string message, Action callback)
        {
            if (item == null)
            {
                Debug.LogError($"[FLOW][TOAST] ShowItem aborted: item is null. title='{title}'");
                return;
            }

            TLog($"ShowItem start item={item.name} title='{title}' parent={item.transform.parent?.name ?? "null"}");
            item.title = title;
            item.description = message;

            callbacks[item] = callback;

            if (container != null && item.transform.parent != container)
            {
                TLog($"Reparenting toast item from {item.transform.parent?.name ?? "null"} to {container.name}");
                item.transform.SetParent(container, false);
            }

            item.gameObject.SetActive(true);
            item.transform.localScale = Vector3.one;
            item.transform.SetAsLastSibling();
            LogToastVisibility("before UpdateUI", item);

            item.UpdateUI();
            Canvas.ForceUpdateCanvases();
            TLog($"Updated UI for {item.name}; opening");
            item.Open();
            Canvas.ForceUpdateCanvases();
            LogToastVisibility("after Open", item);
        }

        private void HandleClose(NotificationManager item)
        {
            TLog($"HandleClose called for {item?.name ?? "null"}");
            if (callbacks.TryGetValue(item, out var cb))
            {
                TLog($"Invoking callback for {item.name}");
                cb?.Invoke();
                callbacks.Remove(item);
            }

            item.gameObject.SetActive(false);

            if (waiting.Count > 0)
            {
                var next = waiting.Dequeue();
                TLog($"Dequeued next toast '{next.title}' - showing now");
                ShowItem(item, next.title, next.message, next.callback);
            }
        }

        private void LogToastVisibility(string phase, NotificationManager item)
        {
            if (!verboseFlowLogging || item == null) return;

            var rt = item.GetComponent<RectTransform>();
            var parent = item.transform.parent;
            int siblingIndex = item.transform.GetSiblingIndex();
            int siblingCount = parent != null ? parent.childCount : 0;
            string rect = rt != null ? $"{rt.rect.width:F1}x{rt.rect.height:F1}" : "no-rect";
            string parentActive = parent != null
                ? $"parentActive={parent.gameObject.activeSelf}/{parent.gameObject.activeInHierarchy}"
                : "parent=null";

            Debug.Log(
                $"[FLOW][TOAST] {phase} item={item.name} active={item.gameObject.activeSelf}/{item.gameObject.activeInHierarchy} " +
                $"sibling={siblingIndex}/{siblingCount} rect={rect} {parentActive} canvas={DescribeCanvas(item.transform)}");

            if (!item.gameObject.activeInHierarchy)
                Debug.LogWarning($"[FLOW][TOAST] {item.name} is activeSelf but not activeInHierarchy. A parent object is inactive.");

            if (rt != null && (rt.rect.width <= 1f || rt.rect.height <= 1f))
                Debug.LogWarning($"[FLOW][TOAST] {item.name} rect is too small ({rect}). Check toast prefab/layout anchors.");
        }

        private void TLog(string msg)
        {
            if (verboseFlowLogging)
                Debug.Log($"[FLOW][TOAST] {msg}");
        }

        private static string DescribeCanvas(Component component)
        {
            if (component == null) return "null";
            var canvas = component.GetComponentInParent<Canvas>();
            if (canvas == null) return "none";
            return $"{canvas.name}(active={canvas.gameObject.activeSelf}/{canvas.gameObject.activeInHierarchy}, order={canvas.sortingOrder}, mode={canvas.renderMode})";
        }

        private struct ToastRequest
        {
            public string title;
            public string message;
            public Action callback;

            public ToastRequest(string t, string m, Action c)
            {
                title = t;
                message = m;
                callback = c;
            }
        }

#if UNITY_EDITOR
        // ── Editor — Context Menu: Toast Prefab Setup Instructions ───────────

        [ContextMenu("NightHunt/Toast Prefab Setup Instructions")]
        private void Editor_ToastPrefabInstructions()
        {
            Debug.Log(
                "[ToastService] The 'prefab' field expects a MUIP NotificationManager prefab.\n" +
                "Setup steps:\n" +
                "  1. In the Project window, navigate to Packages/Michsky MUIP/Resources/ " +
                "(or your local MUIP import folder) and find an existing NotificationManager prefab.\n" +
                "  2. Duplicate it into Assets/_Night_Hunt/Prefabs/UI/ and rename it 'Toast_Notification.prefab'.\n" +
                "  3. Drag the duplicated prefab into the 'prefab' field on this ToastService component.\n" +
                "  4. Optionally customise colours / fonts in the prefab's NotificationManager component.\n" +
                "  5. Make sure the 'container' RectTransform field is also assigned (overlay canvas corner).\n" +
                "The prefab cannot be created from code because NotificationManager relies on serialised " +
                "Unity event data and MUIP's own child hierarchy.");
        }

        [ContextMenu("NightHunt/Auto-Assign Toast Prefab")]
        private void Editor_AutoAssignToastPrefab()
        {
            if (prefab != null) { Debug.Log("[ToastService] prefab already assigned."); return; }

            string[] candidates = {
                "Assets/_Night_Hunt/Prefabs/UI/Toast_Notification.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/Notification.prefab",
                "Assets/Resources/NotificationManager.prefab"
            };
            foreach (var c in candidates)
            {
                var nm = UnityEditor.AssetDatabase.LoadAssetAtPath<NotificationManager>(c);
                if (nm != null)
                {
                    prefab = nm;
                    UnityEditor.EditorUtility.SetDirty(this);
                    Debug.Log($"[ToastService] prefab auto-assigned from {c}");
                    return;
                }
            }
            Debug.LogWarning("[ToastService] Could not find a NotificationManager prefab. " +
                             "Run 'Toast Prefab Setup Instructions' for manual steps.");
        }
#endif
    }
}
