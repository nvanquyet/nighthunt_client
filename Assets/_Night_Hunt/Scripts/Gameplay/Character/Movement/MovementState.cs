using System;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    /// <summary>
    /// Serializable movement state for prediction
    /// </summary>
    [Serializable]
    public struct MovementState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public bool IsSprinting;
        public bool IsCrouching;
        public float Stamina;

        public bool IsStateDifferent(MovementState other, float threshold = 0.1f)
        {
            return Vector3.Distance(Position, other.Position) > threshold ||
                   Quaternion.Angle(Rotation, other.Rotation) > threshold ||
                   Vector3.Distance(Velocity, other.Velocity) > threshold ||
                   IsSprinting != other.IsSprinting ||
                   IsCrouching != other.IsCrouching ||
                   Mathf.Abs(Stamina - other.Stamina) > threshold;
        }
    }
}

