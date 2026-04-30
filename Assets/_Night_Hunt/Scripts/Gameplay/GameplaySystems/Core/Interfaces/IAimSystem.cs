using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Reports the resolved aim direction and world position for the local player.
    ///
    /// AimSystem sources:
    ///   Gun / Melee  – mouse ray cast onto the ground plane, clamped by VisionRange.
    ///   Throwable    – MOBA-style drag joystick (set via SetThrowableAim).
    /// </summary>
    public interface IAimSystem
    {
        /// <summary>Normalised horizontal direction from player toward aim point.</summary>
        Vector3 FinalAimDir { get; }

        /// <summary>World-space point the player is aiming at (Y = player ground Y).</summary>
        Vector3 FinalAimPos { get; }

        /// <summary>World-space aim point projected onto the gameplay ground plane.</summary>
        Vector3 FinalAimGroundPos { get; }

        /// <summary>Raw mouse-world intersection (before clamping). Same as FinalAimPos for guns.</summary>
        Vector3 AimWorldPoint { get; }

        /// <summary>True while throwable aim mode is active (joystick / drag).</summary>
        bool IsThrowableMode { get; }

        /// <summary>
        /// Override aim with a throwable joystick direction.
        /// Call with Vector2.zero to exit throwable mode and revert to mouse aim.
        /// </summary>
        void SetThrowableAim(Vector2 joystickInput);

        /// <summary>
        /// Wire this AimSystem to a spawned player at runtime.
        /// Called from NetworkPlayer.EnableInput() after the local player spawns.
        /// </summary>
        void Initialize(Transform playerRoot, IPlayerStatSystem statSystem);

        /// <summary>Show or hide the world-space aim cursor.</summary>
        void SetCursorVisible(bool visible);

        /// <summary>Current VisionRange from the player stat system (or fallback).</summary>
        float GetVisionRange();
    }
}
