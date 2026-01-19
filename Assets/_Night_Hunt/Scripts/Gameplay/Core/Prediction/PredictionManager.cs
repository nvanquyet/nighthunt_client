using System;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Prediction
{
    /// <summary>
    /// Generic prediction manager with reconciliation
    /// </summary>
    /// <typeparam name="TState">Type of state to predict</typeparam>
    public class PredictionManager<TState> where TState : struct
    {
        private readonly IPredictable<TState> predictable;
        private readonly PredictionBuffer<TState> buffer;
        private int currentTick = 0;
        private int lastServerTick = -1;

        public PredictionManager(IPredictable<TState> predictable, int bufferSize = 60)
        {
            this.predictable = predictable ?? throw new ArgumentNullException(nameof(predictable));
            this.buffer = new PredictionBuffer<TState>(bufferSize);
        }

        /// <summary>
        /// Predict next state based on input
        /// </summary>
        public void Predict()
        {
            currentTick++;
            TState currentState = predictable.GetCurrentState();
            buffer.AddState(currentState, currentTick);
        }

        /// <summary>
        /// Reconcile with server state
        /// </summary>
        public void Reconcile(TState serverState, int serverTick, float threshold = 0.1f)
        {
            if (serverTick <= lastServerTick)
            {
                // Old server update, ignore
                return;
            }

            lastServerTick = serverTick;

            // Check if we need to rollback
            if (buffer.TryGetState(serverTick, out TState predictedState))
            {
                // Simple comparison - can be improved with custom comparison
                if (!predictedState.Equals(serverState))
                {
                    // Rollback to server state
                    predictable.SetState(serverState);

                    // Remove states up to server tick
                    buffer.RemoveStatesUpTo(serverTick);

                    // Re-apply predictions from server tick onwards
                    ReapplyPredictions(serverTick);
                }
            }
            else
            {
                // No prediction for this tick, just apply server state
                predictable.SetState(serverState);
            }
        }

        /// <summary>
        /// Re-apply predictions from a specific tick
        /// </summary>
        private void ReapplyPredictions(int fromTick)
        {
            // This would need to be implemented based on specific prediction logic
            // For now, we just clear the buffer and start fresh
            buffer.RemoveStatesUpTo(fromTick);
        }

        /// <summary>
        /// Get current tick
        /// </summary>
        public int CurrentTick => currentTick;

        /// <summary>
        /// Reset prediction manager
        /// </summary>
        public void Reset()
        {
            currentTick = 0;
            lastServerTick = -1;
            buffer.Clear();
        }
    }
}

