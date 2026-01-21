using UnityEngine;

namespace NightHunt.Networking.Prediction.Core
{
    /// <summary>
    /// Data structure cho server reconciliation.
    /// Chứa state từ server để client có thể reconcile nếu có lệch.
    /// </summary>
    /// <typeparam name="TState">Type của state (phải là struct)</typeparam>
    public struct ReconciliationData<TState> where TState : struct
    {
        /// <summary>
        /// Tick của state này trên server.
        /// </summary>
        public uint ServerTick;

        /// <summary>
        /// State từ server (authoritative).
        /// </summary>
        public TState ServerState;

        /// <summary>
        /// Timestamp khi server gửi state này.
        /// </summary>
        public float ServerTimestamp;

        /// <summary>
        /// Khởi tạo ReconciliationData.
        /// </summary>
        /// <param name="serverTick">Tick trên server</param>
        /// <param name="serverState">State từ server</param>
        /// <param name="serverTimestamp">Timestamp từ server</param>
        public ReconciliationData(uint serverTick, TState serverState, float serverTimestamp)
        {
            ServerTick = serverTick;
            ServerState = serverState;
            ServerTimestamp = serverTimestamp;
        }

        /// <summary>
        /// Tạo ReconciliationData từ state hiện tại với tick.
        /// </summary>
        /// <param name="serverTick">Tick trên server</param>
        /// <param name="serverState">State từ server</param>
        /// <returns>ReconciliationData mới</returns>
        public static ReconciliationData<TState> Create(uint serverTick, TState serverState)
        {
            return new ReconciliationData<TState>(serverTick, serverState, Time.time);
        }
    }
}

