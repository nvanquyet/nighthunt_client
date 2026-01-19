using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    /// <summary>
    /// Generic event dispatcher with type safety
    /// </summary>
    /// <typeparam name="TEvent">Type of event</typeparam>
    public class EventDispatcher<TEvent> where TEvent : IGameplayEvent
    {
        private readonly List<Action<TEvent>> subscribers = new List<Action<TEvent>>();

        /// <summary>
        /// Subscribe to events
        /// </summary>
        public void Subscribe(Action<TEvent> handler)
        {
            if (handler == null) return;
            if (!subscribers.Contains(handler))
            {
                subscribers.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribe from events
        /// </summary>
        public void Unsubscribe(Action<TEvent> handler)
        {
            subscribers.Remove(handler);
        }

        /// <summary>
        /// Dispatch event to all subscribers
        /// </summary>
        public void Dispatch(TEvent eventData)
        {
            for (int i = subscribers.Count - 1; i >= 0; i--)
            {
                try
                {
                    subscribers[i]?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventDispatcher] Error dispatching event {typeof(TEvent).Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Clear all subscribers
        /// </summary>
        public void Clear()
        {
            subscribers.Clear();
        }

        /// <summary>
        /// Get subscriber count
        /// </summary>
        public int SubscriberCount => subscribers.Count;
    }
}

