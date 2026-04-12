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
                    "[ToastService] 'prefab' field is not assigned!\n" +
                    "Toasts will NOT show. Fix: assign a MUIP NotificationManager prefab in Inspector.\n" +
                    "Use Context Menu → 'NightHunt/Auto-Assign Toast Prefab' to auto-find.");
                return;
            }

            if (container == null)
            {
                Debug.LogWarning("[ToastService] 'container' RectTransform is not assigned. Toasts will be parented at root.");
            }

            for (int i = 0; i < maxVisible; i++)
            {
                var item = Instantiate(prefab, container);
                item.gameObject.SetActive(false);
                var captured = item;
                item.onClose.AddListener(() => HandleClose(captured));
                Debug.Log($"[ToastService] Attached onClose listener to {item.name}");
                pool.Add(item);
            }

            Debug.Log($"[ToastService] Initialized pool with {pool.Count} items. container={(container==null?"null":container.name)}");
        }

        public void Show(string title, string message, Action onConfirm = null)
        {
            if (_prefabMissing)
            {
                Debug.LogWarning($"[ToastService] Cannot show toast '{title}' — prefab not assigned.");
                return;
            }

            Debug.Log($"[ToastService] Show requested: '{title}' — '{message}'");

            var item = GetAvailableItem();
            if (item == null)
            {
                Debug.Log($"[ToastService] No available item; enqueueing toast '{title}'");
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
            Debug.Log("[ToastService] GetAvailableItem: none available");
            return null;
        }

        private void ShowItem(NotificationManager item, string title, string message, Action callback)
        {
            Debug.Log($"[ToastService] ShowItem -> item={item?.name ?? "null"}, title='{title}'");
            item.title = title;
            item.description = message;

            callbacks[item] = callback;

            item.gameObject.SetActive(true);
            item.transform.SetAsFirstSibling();

            item.UpdateUI();
            Debug.Log($"[ToastService] Updated UI for {item.name} and opening");
            item.Open();
        }

        private void HandleClose(NotificationManager item)
        {
            Debug.Log($"[ToastService] HandleClose called for {item?.name ?? "null"}");
            if (callbacks.TryGetValue(item, out var cb))
            {
                Debug.Log($"[ToastService] Invoking callback for {item.name}");
                cb?.Invoke();
                callbacks.Remove(item);
            }

            item.gameObject.SetActive(false);

            if (waiting.Count > 0)
            {
                var next = waiting.Dequeue();
                Debug.Log($"[ToastService] Dequeued next toast '{next.title}' — showing now");
                ShowItem(item, next.title, next.message, next.callback);
            }
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