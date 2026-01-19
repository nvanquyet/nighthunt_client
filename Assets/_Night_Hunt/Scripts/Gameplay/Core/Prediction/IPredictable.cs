namespace NightHunt.Gameplay.Core.Prediction
{
    /// <summary>
    /// Interface for objects that can be predicted
    /// </summary>
    /// <typeparam name="TState">Type of state struct</typeparam>
    public interface IPredictable<TState> where TState : struct
    {
        /// <summary>
        /// Get current state for prediction
        /// </summary>
        TState GetCurrentState();

        /// <summary>
        /// Set state (for reconciliation)
        /// </summary>
        void SetState(TState state);
    }
}

