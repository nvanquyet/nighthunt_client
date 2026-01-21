using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Core
{
    /// <summary>
    /// Quản lý lịch sử state snapshots với tick-based indexing.
    /// Hỗ trợ rollback và replay mechanism cho client-side prediction.
    /// </summary>
    /// <typeparam name="TState">Type của state (phải là struct để zero-allocation)</typeparam>
    public class StateHistory<TState> where TState : struct
    {
        private readonly struct StateSnapshot
        {
            public readonly uint Tick;
            public readonly TState State;
            public readonly float Timestamp;

            public StateSnapshot(uint tick, TState state, float timestamp)
            {
                Tick = tick;
                State = state;
                Timestamp = timestamp;
            }
        }

        private readonly List<StateSnapshot> _history;
        private readonly int _maxHistorySize;
        private uint _currentTick;

        /// <summary>
        /// Số lượng state snapshots hiện có trong history.
        /// </summary>
        public int Count => _history.Count;

        /// <summary>
        /// Tick hiện tại.
        /// </summary>
        public uint CurrentTick => _currentTick;

        /// <summary>
        /// Khởi tạo StateHistory với buffer size tùy chỉnh.
        /// </summary>
        /// <param name="maxHistorySize">Số lượng state snapshots tối đa (default: 32)</param>
        public StateHistory(int maxHistorySize = 32)
        {
            _maxHistorySize = maxHistorySize;
            _history = new List<StateSnapshot>(maxHistorySize);
            _currentTick = 0;
        }

        /// <summary>
        /// Thêm state snapshot vào history với tick hiện tại.
        /// </summary>
        /// <param name="state">State snapshot cần lưu</param>
        /// <param name="tick">Tick của state (nếu 0 thì dùng CurrentTick)</param>
        public void AddState(TState state, uint tick = 0)
        {
            if (tick == 0)
            {
                tick = _currentTick;
            }

            var snapshot = new StateSnapshot(tick, state, Time.time);

            // Nếu history đầy, xóa snapshot cũ nhất
            if (_history.Count >= _maxHistorySize)
            {
                _history.RemoveAt(0);
            }

            _history.Add(snapshot);
            _currentTick = tick;
        }

        /// <summary>
        /// Lấy state snapshot tại tick cụ thể.
        /// </summary>
        /// <param name="tick">Tick cần lấy state</param>
        /// <param name="state">Output state nếu tìm thấy</param>
        /// <returns>True nếu tìm thấy state tại tick đó</returns>
        public bool TryGetState(uint tick, out TState state)
        {
            // Tìm từ cuối lên (state mới nhất trước)
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].Tick == tick)
                {
                    state = _history[i].State;
                    return true;
                }
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Lấy state snapshot gần nhất trước hoặc tại tick cụ thể.
        /// </summary>
        /// <param name="tick">Tick cần tìm</param>
        /// <param name="state">Output state nếu tìm thấy</param>
        /// <returns>True nếu tìm thấy state</returns>
        public bool TryGetStateAtOrBefore(uint tick, out TState state)
        {
            // Tìm từ cuối lên, lấy state gần nhất <= tick
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].Tick <= tick)
                {
                    state = _history[i].State;
                    return true;
                }
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Xóa tất cả state snapshots cũ hơn tick cụ thể.
        /// </summary>
        /// <param name="tick">Tick threshold - xóa tất cả state < tick này</param>
        public void RemoveStatesBefore(uint tick)
        {
            _history.RemoveAll(snapshot => snapshot.Tick < tick);
        }

        /// <summary>
        /// Xóa tất cả state snapshots.
        /// </summary>
        public void Clear()
        {
            _history.Clear();
            _currentTick = 0;
        }

        /// <summary>
        /// Tăng tick hiện tại lên 1.
        /// </summary>
        public void IncrementTick()
        {
            _currentTick++;
        }

        /// <summary>
        /// Set tick hiện tại.
        /// </summary>
        /// <param name="tick">Tick mới</param>
        public void SetTick(uint tick)
        {
            _currentTick = tick;
        }

        /// <summary>
        /// Lấy state snapshot mới nhất.
        /// </summary>
        /// <param name="state">Output state mới nhất</param>
        /// <returns>True nếu có state trong history</returns>
        public bool TryGetLatestState(out TState state)
        {
            if (_history.Count > 0)
            {
                state = _history[_history.Count - 1].State;
                return true;
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Lấy tick của state snapshot mới nhất.
        /// </summary>
        /// <returns>Tick của state mới nhất, hoặc 0 nếu không có state</returns>
        public uint GetLatestTick()
        {
            if (_history.Count > 0)
            {
                return _history[_history.Count - 1].Tick;
            }
            return 0;
        }
    }
}

