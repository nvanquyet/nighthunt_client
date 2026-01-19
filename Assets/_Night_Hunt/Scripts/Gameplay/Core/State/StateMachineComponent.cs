using UnityEngine;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// MonoBehaviour wrapper for state machine
    /// </summary>
    /// <typeparam name="TState">Type of state enum</typeparam>
    public abstract class StateMachineComponent<TState> : MonoBehaviour where TState : System.Enum
    {
        protected StateMachine<TState> stateMachine;

        protected virtual void Awake()
        {
            InitializeStateMachine();
        }

        /// <summary>
        /// Initialize state machine - override in derived classes
        /// </summary>
        protected abstract void InitializeStateMachine();

        /// <summary>
        /// Get current state
        /// </summary>
        public TState CurrentState => stateMachine != null ? stateMachine.CurrentState : default(TState);

        /// <summary>
        /// Transition to new state
        /// </summary>
        public bool TransitionTo(TState newState)
        {
            return stateMachine?.TransitionTo(newState) ?? false;
        }

        /// <summary>
        /// Check if can transition to state
        /// </summary>
        public bool CanTransitionTo(TState newState)
        {
            return stateMachine?.CanTransitionTo(newState) ?? false;
        }
    }
}

