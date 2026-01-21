using UnityEngine;

namespace NightHunt.Networking.Prediction.Reconciliation
{
    /// <summary>
    /// Smooth reconciliation strategy - lerp correction.
    /// Dùng cho small errors để có smooth transition.
    /// </summary>
    /// <typeparam name="TState">Type của state</typeparam>
    public class SmoothReconciliation<TState> : IReconciliationStrategy<TState> where TState : struct
    {
        private float _smoothTime = 0.1f;
        private float _currentVelocity;

        /// <summary>
        /// Khởi tạo SmoothReconciliation với smooth time.
        /// </summary>
        /// <param name="smoothTime">Thời gian để smooth (default: 0.1s)</param>
        public SmoothReconciliation(float smoothTime = 0.1f)
        {
            _smoothTime = smoothTime;
        }

        /// <summary>
        /// Kiểm tra xem có cần reconcile không.
        /// Smooth strategy chỉ reconcile nếu error > threshold.
        /// </summary>
        public bool ShouldReconcile(TState clientState, TState serverState, float threshold)
        {
            // Smooth strategy: Chỉ reconcile nếu error > threshold
            // Note: Default implementation dùng Equals, derived classes có thể override để tính distance
            return !clientState.Equals(serverState);
        }

        /// <summary>
        /// Reconcile bằng cách smooth lerp từ client state về server state.
        /// </summary>
        public TState Reconcile(TState clientState, TState serverState, float deltaTime)
        {
            // Smooth: Lerp từ client state về server state
            // Note: Default implementation chỉ trả về server state
            // Derived classes cho Vector3/Quaternion sẽ implement lerp logic
            return serverState;
        }
    }
}

