using UnityEngine;

namespace NightHunt.Gameplay.Character.Movement
{
    public enum RollMode
    {
        /// <summary>Flat ground burst — no vertical component.</summary>
        Dash,
        /// <summary>Forward arc — launches upward at roll start, follows ballistic arc.</summary>
        Leap
    }

    [CreateAssetMenu(fileName = "MovementSettings", menuName = "NightHunt/Prediction/Movement Settings")]
    public class MovementSettings : ScriptableObject
    {
        [Header("Speed")]
        [Min(0f)] public float baseSpeed = 5f;
        [Min(0f)] public float sprintSpeedMultiplier = 1.6f;
        [Min(0f)] public float crouchMultiplier = 0.6f;

        [Header("Acceleration")]
        [Min(0f)] public float acceleration = 25f;
        [Min(0f)] public float deceleration = 30f;

        [Header("Gravity / Jump")]
        [Min(0.1f)] public float gravity = 20f;
        [Tooltip("Extra gravity multiplier applied only while falling (verticalVelocity < 0). Higher = snappier landing. Recommended 1.5–2.5.")]
        [Min(1f)] public float fallGravityMultiplier = 2f;
        [Tooltip("Terminal velocity when falling (m/s). Prevents unbounded speed on long falls.")]
        [Min(1f)] public float maxFallSpeed = 20f;
        [Min(0f)] public float jumpHeight = 1.2f;
        [Tooltip("Downward velocity applied every grounded tick to hug terrain. Keep small (0.2–0.5) to avoid PhysX floor-push jitter.")]
        [Min(0f)] public float groundedStickDownVelocity = 0.3f;
        public bool enableJump = true;

        [Header("Stamina")]
        [Min(0f)] public float maxStamina = 100f;
        [Min(0f)] public float staminaDrainRate = 20f;
        [Min(0f)] public float staminaRegenRate = 15f;
        [Min(0f)] public float minStaminaToSprint = 10f;

        [Header("Roll / Dash")]
        [Tooltip("Stamina consumed instantly at roll start.")]
        [Min(0f)] public float rollStaminaCost = 20f;
        [Tooltip("Fixed horizontal distance the roll covers (metres), regardless of player base speed.")]
        [Min(0.5f)] public float rollDistance = 4f;
        [Tooltip("Seconds to travel rollDistance. Lower = faster dash.")]
        [Min(0.05f)] public float rollDuration = 0.35f;
        [Tooltip("Fraction of rollDuration spent easing out at end (0=hard stop, 0.3=smooth). Actual distance is slightly less when > 0.")]
        [Range(0f, 0.5f)] public float rollEaseOutFraction = 0.25f;
        [Tooltip("Dash = stays on the ground. Leap = launches in a forward arc.")]
        public RollMode rollMode = RollMode.Dash;
        [Tooltip("Vertical launch height for Leap mode (metres). Ignored when Dash.")]
        [Min(0f)] public float rollLeapHeight = 0.8f;
        public bool enableRoll = true;

        [Header("Rotation")]
        [Range(0f, 45f)] public float rotationDeadzoneDegrees = 8f;
        [Min(0f)] public float rotationSpeed = 10f;
        [Range(0f, 1f)] public float moveInputThreshold = 0.1f;
    }
}


