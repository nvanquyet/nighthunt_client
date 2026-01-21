using UnityEngine;

namespace NightHunt.Networking.Prediction.Reconciliation
{
    /// <summary>
    /// Hybrid reconciliation strategy - kết hợp snap và smooth.
    /// Dùng snap cho large errors, smooth cho small errors.
    /// </summary>
    /// <typeparam name="TState">Type của state</typeparam>
    public class HybridReconciliation<TState> : IReconciliationStrategy<TState> where TState : struct
    {
        private readonly SnapReconciliation<TState> _snapStrategy;
        private readonly SmoothReconciliation<TState> _smoothStrategy;
        private float _snapThreshold = 1f;
        private float _smoothTime = 0.1f;

        /// <summary>
        /// Khởi tạo HybridReconciliation với thresholds.
        /// </summary>
        /// <param name="snapThreshold">Threshold để dùng snap (default: 1.0)</param>
        /// <param name="smoothTime">Thời gian để smooth (default: 0.1s)</param>
        public HybridReconciliation(float snapThreshold = 1f, float smoothTime = 0.1f)
        {
            _snapThreshold = snapThreshold;
            _smoothTime = smoothTime;
            _snapStrategy = new SnapReconciliation<TState>();
            _smoothStrategy = new SmoothReconciliation<TState>(smoothTime);
        }

        /// <summary>
        /// Kiểm tra xem có cần reconcile không.
        /// Hybrid strategy luôn reconcile nếu states khác nhau.
        /// </summary>
        public bool ShouldReconcile(TState clientState, TState serverState, float threshold)
        {
            // Hybrid strategy: Luôn reconcile nếu states khác nhau
            return !clientState.Equals(serverState);
        }

        /// <summary>
        /// Reconcile bằng cách chọn strategy dựa trên error magnitude.
        /// </summary>
        public TState Reconcile(TState clientState, TState serverState, float deltaTime)
        {
            // Tính error magnitude (default: dùng threshold, derived classes có thể override)
            float error = CalculateError(clientState, serverState);

            // Nếu error lớn hơn snap threshold → dùng snap
            if (error > _snapThreshold)
            {
                return _snapStrategy.Reconcile(clientState, serverState, deltaTime);
            }
            // Nếu error nhỏ → dùng smooth
            else
            {
                return _smoothStrategy.Reconcile(clientState, serverState, deltaTime);
            }
        }

        /// <summary>
        /// Tính error magnitude giữa 2 states.
        /// Override method này trong derived classes để tính distance cụ thể.
        /// </summary>
        /// <param name="clientState">State trên client</param>
        /// <param name="serverState">State trên server</param>
        /// <returns>Error magnitude</returns>
        protected virtual float CalculateError(TState clientState, TState serverState)
        {
            // Default: Trả về 0 nếu bằng nhau, 1 nếu khác nhau
            // Derived classes sẽ override để tính distance thực tế (ví dụ: Vector3.Distance)
            return clientState.Equals(serverState) ? 0f : 1f;
        }
    }
}

