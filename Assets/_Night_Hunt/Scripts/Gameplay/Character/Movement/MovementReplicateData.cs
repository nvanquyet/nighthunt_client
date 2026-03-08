using FishNet.Object.Prediction;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    public struct MovementReplicateData : IReplicateData
    {
        public Vector2 Move;
        /// <summary>Camera-relative yaw — used to compute movement direction (camRot * inputDir).</summary>
        public float Yaw;
        /// <summary>
        /// Aim-derived yaw — where the character MODEL should face.
        /// In STRAFE/firing mode this is the cursor-to-ground angle.
        /// In TANK mode it equals Yaw (movement direction drives facing).
        /// </summary>
        public float AimYaw;
        public bool Sprint;
        public bool Crouch;
        public bool CameraLocked;

        private uint _tick;

        public MovementReplicateData(
            Vector2 move,
            float yaw,
            float aimYaw,
            bool sprint,
            bool crouch,
            bool cameraLocked)
        {
            Move = move;
            Yaw = yaw;
            AimYaw = aimYaw;
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
