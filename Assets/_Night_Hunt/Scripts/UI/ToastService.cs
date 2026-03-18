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
    }
}