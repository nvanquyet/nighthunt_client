namespace NightHunt.Networking.Prediction.Reconciliation
{
    /// <summary>
    /// Interface cho reconciliation strategy.
    /// Mỗi strategy có cách xử lý reconciliation khác nhau (snap, smooth, hybrid).
    /// </summary>
    /// <typeparam name="TState">Type của state</typeparam>
    public interface IReconciliationStrategy<TState> where TState : struct
    {
        /// <summary>
        /// Kiểm tra xem có cần reconcile không dựa trên threshold.
        /// </summary>
        /// <param name="clientState">State trên client</param>
        /// <param name="serverState">State trên server</param>
        /// <param name="threshold">Threshold để so sánh</param>
        /// <returns>True nếu cần reconcile</returns>
        bool ShouldReconcile(TState clientState, TState serverState, float threshold);

        /// <summary>
        /// Reconcile state từ server về client.
        /// </summary>
        /// <param name="clientState">State hiện tại trên client</param>
        /// <param name="serverState">State từ server</param>
        /// <param name="deltaTime">Thời gian từ lần reconcile trước</param>
        /// <returns>State sau khi reconcile</returns>
        TState Reconcile(TState clientState, TState serverState, float deltaTime);
    }
}

