namespace NightHunt.Networking.Prediction.Reconciliation
{
    public class HybridReconciliation<TState> : IReconciliationStrategy<TState> where TState : struct
    {
        public bool ShouldReconcile(TState clientState, TState serverState, float threshold)
        {
            return !Equals(clientState, serverState);
        }

        public TState Reconcile(TState clientState, TState serverState, float deltaTime)
        {
            // Placeholder: choose snap/smooth dynamically by threshold.
            return serverState;
        }
    }
}

