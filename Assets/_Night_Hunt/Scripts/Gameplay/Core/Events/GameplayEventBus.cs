using System;
using System.Collections.Generic;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Centralized event bus for gameplay events
    /// Singleton pattern với proper cleanup
    /// </summary>
    public class GameplayEventBus : Singleton<GameplayEventBus>
    {
        private Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

        protected override void OnDestroy()
        {
            ClearAllSubscribers();
            base.OnDestroy();
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
            if (HasInstance)
            {
                Destroy(Instance.gameObject);
            }
        }
    }
}