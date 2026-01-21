using FishNet.Object.Prediction;

namespace NightHunt.Networking.Prediction.Modules.Interaction
{
    public struct InteractionReconcileData : IReconcileData
    {
        public int TargetId;
        public int ActionType;
        public bool Success;

        private uint _tick;

        public InteractionReconcileData(int targetId, int actionType, bool success)
        {
            TargetId = targetId;
            ActionType = actionType;
            Success = success;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}

