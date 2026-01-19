using UnityEngine;

namespace NightHunt.Gameplay.Core.Prediction
{
    /// <summary>
    /// Handles reconciliation between client prediction and server state
    /// </summary>
    /// <typeparam name="TState">Type of state to reconcile</typeparam>
    public class ServerReconciliation<TState> where TState : struct
    {
        private readonly IPredictable<TState> predictable;
        private readonly float reconciliationThreshold;

        public ServerReconciliation(IPredictable<TState> predictable, float threshold = 0.1f)
        {
            this.predictable = predictable ?? throw new System.ArgumentNullException(nameof(predictable));
            this.reconciliationThreshold = threshold;
        }

        /// <summary>
        /// Reconcile client prediction with server state
        /// </summary>
        public bool Reconcile(TState serverState)
        {
            TState currentState = predictable.GetCurrentState();
            
            // Simple comparison - can be improved with custom comparison
            if (!currentState.Equals(serverState))
            {
                predictable.SetState(serverState);
                return true; // Reconciliation occurred
            }

            return false; // No reconciliation needed
        }

        /// <summary>
        /// Check if reconciliation is needed
        /// </summary>
        public bool NeedsReconciliation(TState serverState)
        {
            TState currentState = predictable.GetCurrentState();
            return !currentState.Equals(serverState);
        }
    }
}

