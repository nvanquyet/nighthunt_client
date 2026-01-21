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

        private uint _tick;

        public MovementReconcileData(Vector3 position, Quaternion rotation, Vector3 velocity, float stamina = 100f)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            Stamina = stamina;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}

