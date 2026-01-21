using FishNet.Object.Prediction;

namespace NightHunt.Networking.Prediction.Modules.Interaction
{
    public struct InteractionReplicateData : IReplicateData
    {
        public int TargetId;
        public int ActionType;

        private uint _tick;

        public InteractionReplicateData(int targetId, int actionType)
        {
            TargetId = targetId;
            ActionType = actionType;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}

