using System;
using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;

namespace NightHunt.UI
{
    public class ToastService : MonoBehaviour
    {
        public static ToastService Instance;

        [SerializeField] private RectTransform container;
        [SerializeField] private NotificationManager prefab;
        [SerializeField] private int maxVisible = 3;

        private List<NotificationManager> pool = new();
        private Queue<ToastRequest> waiting = new();

        private Dictionary<NotificationManager, Action> callbacks = new();

        private void Awake()
        {
            Instance = this;

            for (int i = 0; i < maxVisible; i++)
            {
                var item = Instantiate(prefab, container);
                item.gameObject.SetActive(false);

                var captured = item;

                item.onClose.AddListener(() =>
                {
                    HandleClose(captured);
                });

                pool.Add(item);
            }
        }

        public void Show(string title, string message, Action onConfirm = null)
        {
            var item = GetAvailableItem();

            if (item == null)
            {
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

            return null;
        }

        private void ShowItem(NotificationManager item, string title, string message, Action callback)
        {
            item.title = title;
            item.description = message;

            callbacks[item] = callback;

            item.gameObject.SetActive(true);
            item.transform.SetAsFirstSibling();

            item.UpdateUI();
            item.Open();
        }

        private void HandleClose(NotificationManager item)
        {
            if (callbacks.TryGetValue(item, out var cb))
            {
                cb?.Invoke();
                callbacks.Remove(item);
            }

            item.gameObject.SetActive(false);

            if (waiting.Count > 0)
            {
                var next = waiting.Dequeue();
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