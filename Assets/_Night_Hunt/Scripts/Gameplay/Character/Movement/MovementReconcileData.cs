using FishNet.Object.Prediction;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    public struct MovementReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float Stamina;
        public bool IsRolling;
        public float RollTimer;
        public Vector3 RollDir;
        public bool IsSprinting;
        public bool IsCrouching;
        public bool IsGrounded;
        public bool JumpTriggered;
        public bool RollTriggered;

        private uint _tick;

        public MovementReconcileData(
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            float stamina,
            bool isRolling = false,
            float rollTimer = 0f,
            Vector3 rollDir = default,
            bool isSprinting = false,
            bool isCrouching = false,
            bool isGrounded = false,
            bool jumpTriggered = false,
            bool rollTriggered = false)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            Stamina = stamina;
            IsRolling = isRolling;
            RollTimer = rollTimer;
            RollDir = rollDir;
            IsSprinting = isSprinting;
            IsCrouching = isCrouching;
            IsGrounded = isGrounded;
            JumpTriggered = jumpTriggered;
            RollTriggered = rollTriggered;
            _tick = 0;
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
        public void Dispose() { }
    }
}
