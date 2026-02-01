using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    [CreateAssetMenu(fileName = "MovementSettings", menuName = "NightHunt/Prediction/Movement Settings")]
    public class MovementSettings : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Base speed is now managed by CharacterStats system. Use CharacterStatsConfig to set base MoveSpeed.")]
        [Min(0f)] public float sprintMultiplier = 1.5f;
        [Min(0f)] public float crouchMultiplier = 0.6f;

        [Header("Acceleration")]
        [Min(0f)] public float acceleration = 25f;
        [Min(0f)] public float deceleration = 30f;

        [Header("Gravity / Jump")]
        [Min(0.1f)] public float gravity = 20f;
        [Min(0f)] public float jumpHeight = 1.2f;
        [Min(0f)] public float groundedStickDownVelocity = 2f;

        [Header("Stamina")]
        [Min(0f)] public float maxStamina = 100f;
        [Min(0f)] public float staminaDrainRate = 20f;
        [Min(0f)] public float staminaRegenRate = 15f;
        [Min(0f)] public float minStaminaToSprint = 10f;

        [Header("Rotation")]
        [Range(0f, 45f)] public float rotationDeadzoneDegrees = 8f;
        [Min(0f)] public float rotationSpeed = 10f;
        [Range(0f, 1f)] public float moveInputThreshold = 0.1f;
    }
}


