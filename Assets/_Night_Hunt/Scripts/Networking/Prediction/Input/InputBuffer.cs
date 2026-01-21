using System;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Networking.Prediction.Input
{
    /// <summary>
    /// Buffer để lưu input với tick alignment cho client-side prediction.
    /// Hỗ trợ replay inputs khi reconcile.
    /// </summary>
    /// <typeparam name="TInput">Type của input (phải implement IInputData)</typeparam>
    public class InputBuffer<TInput> where TInput : struct, IInputData
    {
        private readonly struct InputEntry
        {
            public readonly uint Tick;
            public readonly TInput Input;
            public readonly float Timestamp;

            public InputEntry(uint tick, TInput input, float timestamp)
            {
                Tick = tick;
                Input = input;
                Timestamp = timestamp;
            }
        }

        private readonly List<InputEntry> _inputs;
        private readonly int _maxBufferSize;

        /// <summary>
        /// Số lượng inputs hiện có trong buffer.
        /// </summary>
        public int Count => _inputs.Count;

        /// <summary>
        /// Khởi tạo InputBuffer với buffer size tùy chỉnh.
        /// </summary>
        /// <param name="maxBufferSize">Số lượng inputs tối đa (default: 64)</param>
        public InputBuffer(int maxBufferSize = 64)
        {
            _maxBufferSize = maxBufferSize;
            _inputs = new List<InputEntry>(maxBufferSize);
        }

        /// <summary>
        /// Thêm input vào buffer với tick.
        /// </summary>
        /// <param name="input">Input cần thêm</param>
        /// <param name="tick">Tick của input</param>
        public void AddInput(TInput input, uint tick)
        {
            var entry = new InputEntry(tick, input, UnityEngine.Time.time);

            // Nếu buffer đầy, xóa input cũ nhất
            if (_inputs.Count >= _maxBufferSize)
            {
                _inputs.RemoveAt(0);
            }

            _inputs.Add(entry);
        }

        /// <summary>
        /// Lấy input tại tick cụ thể.
        /// </summary>
        /// <param name="tick">Tick cần lấy input</param>
        /// <param name="input">Output input nếu tìm thấy</param>
        /// <returns>True nếu tìm thấy input tại tick đó</returns>
        public bool TryGetInput(uint tick, out TInput input)
        {
            foreach (var entry in _inputs)
            {
                if (entry.Tick == tick)
                {
                    input = entry.Input;
                    return true;
                }
            }

            input = default;
            return false;
        }

        /// <summary>
        /// Lấy tất cả inputs từ tick cụ thể đến hiện tại.
        /// Dùng cho replay khi reconcile.
        /// </summary>
        /// <param name="fromTick">Tick bắt đầu</param>
        /// <returns>List các input entries từ tick đó</returns>
        public List<(uint Tick, TInput Input, float Timestamp)> GetInputsFromTick(uint fromTick)
        {
            return _inputs.Where(entry => entry.Tick >= fromTick)
                         .OrderBy(entry => entry.Tick)
                         .Select(entry => (entry.Tick, entry.Input, entry.Timestamp))
                         .ToList();
        }

        /// <summary>
        /// Lấy input mới nhất.
        /// </summary>
        /// <param name="input">Output input mới nhất</param>
        /// <returns>True nếu có input trong buffer</returns>
        public bool TryGetLatestInput(out TInput input)
        {
            if (_inputs.Count > 0)
            {
                input = _inputs[_inputs.Count - 1].Input;
                return true;
            }

            input = default;
            return false;
        }

        /// <summary>
        /// Xóa tất cả inputs cũ hơn tick cụ thể.
        /// </summary>
        /// <param name="tick">Tick threshold - xóa tất cả inputs < tick này</param>
        public void RemoveInputsBefore(uint tick)
        {
            _inputs.RemoveAll(entry => entry.Tick < tick);
        }

        /// <summary>
        /// Xóa tất cả inputs.
        /// </summary>
        public void Clear()
        {
            _inputs.Clear();
        }

        /// <summary>
        /// Lấy tick của input mới nhất.
        /// </summary>
        /// <returns>Tick của input mới nhất, hoặc 0 nếu không có input</returns>
        public uint GetLatestTick()
        {
            if (_inputs.Count > 0)
            {
                return _inputs[_inputs.Count - 1].Tick;
            }
            return 0;
        }
    }
}

