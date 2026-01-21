using UnityEngine;

namespace NightHunt.Networking.Prediction.Reconciliation
{
    /// <summary>
    /// Snap reconciliation strategy - instant correction.
    /// Dùng cho large errors hoặc khi cần correction ngay lập tức.
    /// </summary>
    /// <typeparam name="TState">Type của state</typeparam>
    public class SnapReconciliation<TState> : IReconciliationStrategy<TState> where TState : struct
    {
        /// <summary>
        /// Kiểm tra xem có cần reconcile không.
        /// Snap strategy luôn reconcile nếu states khác nhau.
        /// </summary>
        public bool ShouldReconcile(TState clientState, TState serverState, float threshold)
        {
            // Snap strategy: Luôn reconcile nếu states khác nhau
            return !clientState.Equals(serverState);
        }

        /// <summary>
        /// Reconcile bằng cách snap trực tiếp về server state.
        /// </summary>
        public TState Reconcile(TState clientState, TState serverState, float deltaTime)
        {
            // Snap: Trả về server state ngay lập tức
            return serverState;
        }
    }
}

