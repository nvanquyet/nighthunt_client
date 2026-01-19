using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Centralized event bus for gameplay events
    /// Singleton pattern với proper cleanup
    /// </summary>
    public class GameplayEventBus : MonoBehaviour
    {
        private static GameplayEventBus _instance;
        public static GameplayEventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Tìm trong scene trước
                    _instance = FindFirstObjectByType<GameplayEventBus>();
                    
                    // Nếu không có, tạo mới
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("GameplayEventBus");
                        _instance = go.AddComponent<GameplayEventBus>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            // Cleanup khi application quit
            _instance = null;
        }

        private void OnDestroy()
        {
            // Clear subscribers khi destroy
            ClearAllSubscribers();
            
            // Reset static instance nếu đây là instance chính
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Subscribe to an event type
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameplayEvent
        {
            Type eventType = typeof(TEvent);
            if (!subscribers.ContainsKey(eventType))
            {
                subscribers[eventType] = new List<Delegate>();
            }
            subscribers[eventType].Add(handler);
        }

        /// <summary>
        /// Unsubscribe from an event type
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameplayEvent
        {
            Type eventType = typeof(TEvent);
            if (subscribers.ContainsKey(eventType))
            {
                subscribers[eventType].Remove(handler);
            }
        }

        /// <summary>
        /// Publish an event
        /// </summary>
        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameplayEvent
        {
            Type eventType = typeof(TEvent);
            if (subscribers.ContainsKey(eventType))
            {
                // Iterate over a copy to allow unsubscribing during iteration
                foreach (Delegate handler in new List<Delegate>(subscribers[eventType]))
                {
                    if (handler is Action<TEvent> typedHandler)
                    {
                        typedHandler?.Invoke(gameEvent);
                    }
                }
            }
        }

        /// <summary>
        /// Clear all subscribers
        /// </summary>
        public void ClearAllSubscribers()
        {
            subscribers.Clear();
        }

        /// <summary>
        /// Manually destroy the singleton (useful for scene cleanup)
        /// </summary>
        public static void DestroySingleton()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }
    }
}