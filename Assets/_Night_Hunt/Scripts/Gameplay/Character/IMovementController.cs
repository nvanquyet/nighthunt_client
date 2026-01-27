using UnityEngine;
using NightHunt.Gameplay.Character.Movement;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Interface cho tất cả movement controllers.
    /// Cho phép swap giữa predicted và normal movement dễ dàng.
    /// </summary>
    public interface IMovementController
    {
        bool IsSpawned { get; }
        bool IsOwner { get; }
        // ============= INPUT API =============
        
        /// <summary>
        /// Set movement input (WASD)
        /// </summary>
        void SetMoveInput(Vector2 input);
        
        /// <summary>
        /// Set sprint state
        /// </summary>
        void SetSprinting(bool sprinting);
        
        /// <summary>
        /// Set crouch state
        /// </summary>
        void SetCrouching(bool crouching);
        
        // ============= GETTERS =============
        
        /// <summary>
        /// Get current movement speed
        /// </summary>
        float GetCurrentMoveSpeed();
        
        /// <summary>
        /// Get current stamina
        /// </summary>
        float GetStamina();
        
        /// <summary>
        /// Check if currently sprinting
        /// </summary>
        bool IsSprinting();
        
        /// <summary>
        /// Check if currently crouching
        /// </summary>
        bool IsCrouching();
        
        // ============= STATE MANAGEMENT =============
        
        /// <summary>
        /// Set weight penalty (0-1)
        /// </summary>
        void SetWeightPenalty(float penalty);
        
        /// <summary>
        /// Set stamina drain multiplier
        /// </summary>
        void SetStaminaDrainMultiplier(float multiplier);
        
        /// <summary>
        /// Get current movement state
        /// </summary>
        MovementState GetCurrentState();
        
        /// <summary>
        /// Set movement state (for teleport/respawn)
        /// </summary>
        void SetState(MovementState state);
    }
}
