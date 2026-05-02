
namespace NightHunt.Networking.Prediction.Reconciliation
{
    /// <summary>
    /// Strategy interface for deciding and applying reconciliation.
    /// </summary>
    /// <typeparam name="TState">Struct state type.</typeparam>
    public interface IReconciliationStrategy<TState> where TState : struct
    {
        bool ShouldReconcile(TState clientState, TState serverState, float threshold);
        TState Reconcile(TState clientState, TState serverState, float deltaTime);
    }
}

