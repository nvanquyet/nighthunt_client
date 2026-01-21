using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Input
{
    /// <summary>
    /// Tick-aligned input buffer for replay during reconciliation.
    /// </summary>
    /// <typeparam name="TInput">Struct input type implementing IInputData.</typeparam>
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

        public int Count => _inputs.Count;

        public InputBuffer(int maxBufferSize = 64)
        {
            _maxBufferSize = maxBufferSize;
            _inputs = new List<InputEntry>(maxBufferSize);
        }

        public void AddInput(TInput input, uint tick)
        {
            var entry = new InputEntry(tick, input, Time.time);

            if (_inputs.Count >= _maxBufferSize)
                _inputs.RemoveAt(0);

            _inputs.Add(entry);
        }

        public bool TryGetInput(uint tick, out TInput input)
        {
            for (int i = 0; i < _inputs.Count; i++)
            {
                if (_inputs[i].Tick == tick)
                {
                    input = _inputs[i].Input;
                    return true;
                }
            }

            input = default;
            return false;
        }

        public List<(uint Tick, TInput Input, float Timestamp)> GetInputsFromTick(uint fromTick)
        {
            var results = new List<(uint, TInput, float)>(_inputs.Count);
            for (int i = 0; i < _inputs.Count; i++)
            {
                var entry = _inputs[i];
                if (entry.Tick >= fromTick)
                    results.Add((entry.Tick, entry.Input, entry.Timestamp));
            }
            results.Sort((a, b) => a.Item1.CompareTo(b.Item1)); // Sửa dòng này
            return results;
        }

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

        public void RemoveInputsBefore(uint tick)
        {
            _inputs.RemoveAll(entry => entry.Tick < tick);
        }

        public void Clear()
        {
            _inputs.Clear();
        }

        public uint GetLatestTick()
        {
            if (_inputs.Count > 0)
                return _inputs[_inputs.Count - 1].Tick;

            return 0;
        }
    }
}

