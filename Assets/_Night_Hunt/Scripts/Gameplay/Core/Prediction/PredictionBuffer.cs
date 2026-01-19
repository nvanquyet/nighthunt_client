using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Prediction
{
    /// <summary>
    /// Buffer to store predicted states for rollback
    /// </summary>
    /// <typeparam name="TState">Type of state to buffer</typeparam>
    public class PredictionBuffer<TState> where TState : struct
    {
        private readonly int maxBufferSize;
        private readonly Queue<PredictedState<TState>> buffer = new Queue<PredictedState<TState>>();

        public PredictionBuffer(int maxSize = 60)
        {
            maxBufferSize = maxSize;
        }

        /// <summary>
        /// Add a predicted state to the buffer
        /// </summary>
        public void AddState(TState state, int tick)
        {
            buffer.Enqueue(new PredictedState<TState>
            {
                State = state,
                Tick = tick,
                Timestamp = Time.time
            });

            // Remove oldest states if buffer is full
            while (buffer.Count > maxBufferSize)
            {
                buffer.Dequeue();
            }
        }

        /// <summary>
        /// Get state at specific tick
        /// </summary>
        public bool TryGetState(int tick, out TState state)
        {
            foreach (var predictedState in buffer)
            {
                if (predictedState.Tick == tick)
                {
                    state = predictedState.State;
                    return true;
                }
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Remove all states up to and including the specified tick
        /// </summary>
        public void RemoveStatesUpTo(int tick)
        {
            while (buffer.Count > 0 && buffer.Peek().Tick <= tick)
            {
                buffer.Dequeue();
            }
        }

        /// <summary>
        /// Clear all buffered states
        /// </summary>
        public void Clear()
        {
            buffer.Clear();
        }

        /// <summary>
        /// Get buffer count
        /// </summary>
        public int Count => buffer.Count;
    }

    /// <summary>
    /// Predicted state with metadata
    /// </summary>
    public struct PredictedState<TState> where TState : struct
    {
        public TState State;
        public int Tick;
        public float Timestamp;
    }
}

