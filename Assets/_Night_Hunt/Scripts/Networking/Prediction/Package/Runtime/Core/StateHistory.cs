using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Networking.Prediction.Core
{
    /// <summary>
    /// Tick-indexed state buffer supporting rollback/replay.
    /// </summary>
    /// <typeparam name="TState">Struct state type.</typeparam>
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

        public int Count => _history.Count;
        public uint CurrentTick => _currentTick;

        public StateHistory(int maxHistorySize = 32)
        {
            _maxHistorySize = maxHistorySize;
            _history = new List<StateSnapshot>(maxHistorySize);
            _currentTick = 0;
        }

        public void AddState(TState state, uint tick = 0)
        {
            if (tick == 0)
                tick = _currentTick;

            var snapshot = new StateSnapshot(tick, state, Time.time);

            if (_history.Count >= _maxHistorySize)
                _history.RemoveAt(0);

            _history.Add(snapshot);
            _currentTick = tick;
        }

        public bool TryGetState(uint tick, out TState state)
        {
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

        public bool TryGetStateAtOrBefore(uint tick, out TState state)
        {
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

        public void RemoveStatesBefore(uint tick)
        {
            _history.RemoveAll(snapshot => snapshot.Tick < tick);
        }

        public void Clear()
        {
            _history.Clear();
            _currentTick = 0;
        }

        public void IncrementTick()
        {
            _currentTick++;
        }

        public void SetTick(uint tick)
        {
            _currentTick = tick;
        }

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

        public uint GetLatestTick()
        {
            if (_history.Count > 0)
                return _history[_history.Count - 1].Tick;

            return 0;
        }
    }
}

