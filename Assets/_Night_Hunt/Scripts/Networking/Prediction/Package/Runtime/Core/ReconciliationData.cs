using UnityEngine;

namespace NightHunt.Networking.Prediction.Core
{
    /// <summary>
    /// Payload sent from server to owner for reconciliation.
    /// </summary>
    public struct ReconciliationData<TState> where TState : struct
    {
        public uint ServerTick;
        public TState ServerState;
        public float ServerTimestamp;

        public ReconciliationData(uint serverTick, TState serverState, float serverTimestamp)
        {
            ServerTick = serverTick;
            ServerState = serverState;
            ServerTimestamp = serverTimestamp;
        }

        public static ReconciliationData<TState> Create(uint serverTick, TState serverState)
        {
            return new ReconciliationData<TState>(serverTick, serverState, Time.time);
        }
    }
}

