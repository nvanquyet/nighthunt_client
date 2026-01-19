using System;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Generic state machine interface
    /// </summary>
    /// <typeparam name="TState">Type of state enum</typeparam>
    public interface IStateMachine<TState> where TState : Enum
    {
        /// <summary>
        /// Current state
        /// </summary>
        TState CurrentState { get; }

        /// <summary>
        /// Previous state
        /// </summary>
        TState PreviousState { get; }

        /// <summary>
        /// Transition to a new state
        /// </summary>
        bool TransitionTo(TState newState);

        /// <summary>
        /// Check if transition is allowed
        /// </summary>
        bool CanTransitionTo(TState newState);

        /// <summary>
        /// Event fired when state changes
        /// </summary>
        event Action<TState, TState> OnStateChanged;
    }
}

