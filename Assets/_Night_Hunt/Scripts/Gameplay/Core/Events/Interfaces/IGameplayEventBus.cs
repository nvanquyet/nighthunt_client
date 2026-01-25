using System;

namespace NightHunt.Gameplay.Core.Events.Interfaces
{
    public interface IGameplayEventBus
    {
        /// <summary>
        /// Subscribe to an event type
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameplayEvent;
        
        /// <summary>
        /// Unsubscribe from an event type
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameplayEvent;

        /// <summary>
        /// Publish an event
        /// </summary>
        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameplayEvent;

        /// <summary>
        /// Clear all subscribers
        /// </summary>
        public void ClearAllSubscribers();
    }
}