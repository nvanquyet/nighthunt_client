using UnityEngine;
using FishNet.Object;
using FOW;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.Player
{
    /// <summary>
    /// OBSOLETE — placeholder stub, not connected to anything.
    /// FOW vision is handled by FogVisionBinder (stat → ViewRadius) +
    /// FogTeamVisibilityBinder (enemy hider) +
    /// CharacterVisualController (death/respawn toggle).
    /// This class can be removed when convenient.
    /// </summary>
    [System.Obsolete("Not implemented. Use FogVisionBinder + FogTeamVisibilityBinder instead.")]
    public class PlayerVisionSystem : NetworkBehaviour
    {
        [Header("References (unused)")]
        [SerializeField] private NetworkPlayer networkPlayer;
        [SerializeField] private FogOfWarRevealer3D fogOfWarRevealer3D;
    }
}