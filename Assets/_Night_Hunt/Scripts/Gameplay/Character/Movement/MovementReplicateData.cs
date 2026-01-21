using FishNet.Object.Prediction;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    public struct MovementReplicateData : IReplicateData
    {
        public Vector2 MoveInput;
        public Quaternion Rotation;
        public bool IsSprinting;
        public bool IsCrouching;

        private uint _tick;

        public MovementReplicateData(Vector2 moveInput, Quaternion rotation, bool isSprinting, bool isCrouching)
        {
            MoveInput = moveInput;
            Rotation = rotation;
            IsSprinting = isSprinting;
            IsCrouching = isCrouching;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}

