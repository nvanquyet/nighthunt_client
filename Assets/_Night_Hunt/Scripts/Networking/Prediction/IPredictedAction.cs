namespace NightHunt.Networking.Prediction
{
    /// <summary>
    /// Interface chung cho 1 action có client-side prediction.
    /// - StartPrediction: chạy optimistic update trên client khi gửi request.
    /// - Confirm: server chấp nhận, finalize state (thường không cần làm nhiều).
    /// - Rollback: server từ chối, khôi phục lại state ban đầu.
    /// </summary>
    public interface IPredictedAction
    {
        /// <summary>
        /// Thực hiện client-side prediction (optimistic update).
        /// </summary>
        void StartPrediction();

        /// <summary>
        /// Được gọi khi server chấp nhận (Success = true).
        /// </summary>
        void Confirm();

        /// <summary>
        /// Được gọi khi server từ chối (Success = false) để rollback lại state.
        /// </summary>
        void Rollback();
    }
}


