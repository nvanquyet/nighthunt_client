using System;
using System.Collections.Generic;

namespace NightHunt.Networking.Prediction
{
    /// <summary>
    /// Generic tracker cho các predicted action theo requestId.
    /// Dùng được cho interaction, weapon fire, v.v.
    /// 
    /// Flow chuẩn:
    /// - Client:
    ///   1) Tạo action (implement IPredictedAction) với đủ context.
    ///   2) Gọi RegisterAndStart(action) → tracker tạo requestId, lưu lại, gọi StartPrediction().
    ///   3) Gửi requestId + data lên server qua RPC.
    /// - Server:
    ///   4) Validate, xử lý logic.
    ///   5) Gửi kết quả về client (Success/Fail + requestId).
    /// - Client:
    ///   6) Gọi Resolve(requestId, success) → Confirm() hoặc Rollback() rồi remove entry.
    /// </summary>
    public class PredictionTracker<TAction> where TAction : class, IPredictedAction
    {
        private readonly Dictionary<PredictionRequestId, TAction> _pending =
            new Dictionary<PredictionRequestId, TAction>();

        private uint _nextId = 1;

        /// <summary>
        /// Số lượng action đang chờ server result.
        /// </summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// Tạo requestId mới (unique trên client này).
        /// </summary>
        private PredictionRequestId GetNextId()
        {
            // Bỏ qua 0 để giữ Invalid = 0
            if (_nextId == 0)
                _nextId = 1;

            var id = new PredictionRequestId(_nextId);
            _nextId++;
            return id;
        }

        /// <summary>
        /// Đăng ký action mới, chạy StartPrediction và trả về requestId để gửi lên server.
        /// </summary>
        public PredictionRequestId RegisterAndStart(TAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var id = GetNextId();
            _pending[id] = action;

            action.StartPrediction();

            return id;
        }

        /// <summary>
        /// Gọi khi nhận kết quả từ server.
        /// - Nếu success = true → Confirm().
        /// - Nếu success = false → Rollback().
        /// Sau đó remove khỏi pending list.
        /// </summary>
        public void Resolve(PredictionRequestId requestId, bool success)
        {
            if (!requestId.IsValid)
                return;

            if (!_pending.TryGetValue(requestId, out var action) || action == null)
                return;

            _pending.Remove(requestId);

            if (success)
            {
                action.Confirm();
            }
            else
            {
                action.Rollback();
            }
        }

        /// <summary>
        /// Hủy tất cả prediction đang pending (ví dụ khi disconnect / scene change).
        /// Mặc định sẽ Rollback tất cả để tránh state bị kẹt.
        /// </summary>
        public void CancelAll(bool rollback = true)
        {
            if (!rollback)
            {
                _pending.Clear();
                return;
            }

            foreach (var kvp in _pending)
            {
                kvp.Value?.Rollback();
            }

            _pending.Clear();
        }
    }
}


