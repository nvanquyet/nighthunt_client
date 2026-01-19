using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Base state machine implementation
    /// </summary>
    /// <typeparam name="TState">Type of state enum</typeparam>
    public class StateMachine<TState> : IStateMachine<TState> where TState : Enum
    {
        private TState currentState;
        private TState previousState;
        private readonly Dictionary<TState, HashSet<TState>> allowedTransitions = new Dictionary<TState, HashSet<TState>>();

        public TState CurrentState => currentState;
        public TState PreviousState => previousState;

        public event Action<TState, TState> OnStateChanged;

        public StateMachine(TState initialState)
        {
            currentState = initialState;
            previousState = initialState;
        }

        /// <summary>
        /// Add allowed transition from one state to another
        /// </summary>
        public void AddTransition(TState from, TState to)
        {
            if (!allowedTransitions.ContainsKey(from))
            {
                allowedTransitions[from] = new HashSet<TState>();
            }

            allowedTransitions[from].Add(to);
        }

        /// <summary>
        /// Add multiple transitions from one state
        /// </summary>
        public void AddTransitions(TState from, params TState[] toStates)
        {
            foreach (var to in toStates)
            {
                AddTransition(from, to);
            }
        }

        /// <summary>
        /// Check if transition is allowed
        /// </summary>
        public bool CanTransitionTo(TState newState)
        {
            // If no transitions defined for current state, allow all transitions
            if (!allowedTransitions.ContainsKey(currentState))
            {
                return true;
            }

            return allowedTransitions[currentState].Contains(newState);
        }

        /// <summary>
        /// Transition to a new state
        /// </summary>
        public bool TransitionTo(TState newState)
        {
            if (EqualityComparer<TState>.Default.Equals(currentState, newState))
            {
                return false; // Already in this state
            }

            if (!CanTransitionTo(newState))
            {
                Debug.LogWarning($"[StateMachine] Cannot transition from {currentState} to {newState}");
                return false;
            }

            previousState = currentState;
            currentState = newState;

            OnStateChanged?.Invoke(previousState, currentState);
            return true;
        }

        /// <summary>
        /// Force transition without validation (use with caution)
        /// </summary>
        public void ForceTransition(TState newState)
        {
            previousState = currentState;
            currentState = newState;
            OnStateChanged?.Invoke(previousState, currentState);
        }

        /// <summary>
        /// Reset state machine to initial state
        /// </summary>
        public void Reset(TState initialState)
        {
            previousState = initialState;
            currentState = initialState;
            allowedTransitions.Clear();
        }
    }
}

