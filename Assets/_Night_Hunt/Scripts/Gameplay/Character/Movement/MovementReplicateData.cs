using FishNet.Object.Prediction;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    public struct MovementReplicateData : IReplicateData
    {
        public Vector2 Move;
        public float Yaw;
        public bool Sprint;
        public bool Crouch;
        public bool CameraLocked;

        private uint _tick;

        public MovementReplicateData(
            Vector2 move,
            float yaw,
            bool sprint,
            bool crouch,
            bool cameraLocked)
        {
            Move = move;
            Yaw = yaw;
            Sprint = sprint;
            Crouch = crouch;
            CameraLocked = cameraLocked;
            _tick = 0;
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
        public void Dispose() { }
    }
}
