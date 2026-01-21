using UnityEngine;

namespace NightHunt.Networking.Prediction.Reconciliation
{
    public class SmoothReconciliation<TState> : IReconciliationStrategy<TState> where TState : struct
    {
        public bool ShouldReconcile(TState clientState, TState serverState, float threshold)
        {
            return !Equals(clientState, serverState);
        }

        public TState Reconcile(TState clientState, TState serverState, float deltaTime)
        {
            // For structs without lerp support, fallback to snap.
            return serverState;
        }
    }
}

