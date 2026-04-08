using UnityEngine;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Character lifecycle states
    /// </summary>
    public enum CharacterLifecycleState
    {
        Alive,
        Downed,
        Dead,
        Respawning,
        Spectating
    }

    /// <summary>
    /// State machine for character lifecycle
    /// </summary>
    public class CharacterStateMachine : StateMachineComponent<CharacterLifecycleState>
    {
        [Header("State Machine Settings")]
        [SerializeField] private CharacterLifecycleState initialState = CharacterLifecycleState.Alive;

        protected override void InitializeStateMachine()
        {
            stateMachine = new StateMachine<CharacterLifecycleState>(initialState);

            // Define allowed transitions
            // Alive can transition to Downed, Dead, or Spectating
            stateMachine.AddTransitions(CharacterLifecycleState.Alive,
                CharacterLifecycleState.Downed,
                CharacterLifecycleState.Dead,
                CharacterLifecycleState.Spectating);

            // Downed can transition to Dead, Alive (revived), or Spectating
            stateMachine.AddTransitions(CharacterLifecycleState.Downed,
                CharacterLifecycleState.Dead,
                CharacterLifecycleState.Alive,
                CharacterLifecycleState.Spectating);

            // Dead can transition to Respawning, Spectating, or directly to Alive
            // (direct Dead→Alive needed when server confirms respawn before client SM processes Respawning state)
            stateMachine.AddTransitions(CharacterLifecycleState.Dead,
                CharacterLifecycleState.Respawning,
                CharacterLifecycleState.Alive,
                CharacterLifecycleState.Spectating);

            // Respawning can transition to Alive
            stateMachine.AddTransitions(CharacterLifecycleState.Respawning,
                CharacterLifecycleState.Alive);

            // Spectating can transition to Alive (when respawned)
            stateMachine.AddTransitions(CharacterLifecycleState.Spectating,
                CharacterLifecycleState.Alive);

            // Subscribe to state changes
            stateMachine.OnStateChanged += OnStateChanged;
        }

        private void OnStateChanged(CharacterLifecycleState previousState, CharacterLifecycleState newState)
        {
            Debug.Log($"[CharacterStateMachine] State changed: {previousState} -> {newState}");
            // Handle state change logic here
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.OnStateChanged -= OnStateChanged;
            }
        }
    }
}

