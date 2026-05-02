namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Unified contract for any networked object that has a team identity and
    /// needs FOW visibility management via <see cref="FogTeamVisibilityBinder"/>.
    ///
    /// FOW visibility rules (enforced by FogTeamVisibilityBinder):
    ///   - Same team as local player  → NO hider → always visible
    ///   - Different team            → FogOfWarHider → hidden outside vision reveal radius
    ///   - TeamId == -1 (neutral)    → NO hider → always visible (map objects, neutral pickups)
    ///
    /// Implement this on:
    ///   • NetworkPlayer          (team set via SyncVar, changes on team switch)
    ///   • BaseDeployable         (VisionWard, RespawnBeacon — team set in Initialize())
    ///   • ProjectileNetworked    (grenade, molotov — team inherited from thrower)
    ///   • Any future object that needs FOW team-based visibility
    ///
    /// Do NOT implement on:
    ///   • Map geometry (MapStatic, Wall, Ground) — always visible, no component needed
    ///   • Local-player-only objects (UI, minimap markers)
    ///
    /// AlwaysVisible contract:
    ///   If <see cref="AlwaysVisible"/> returns true, FogTeamVisibilityBinder skips the team
    ///   check and ensures the object is always shown. Use for items that should be visible
    ///   regardless of FOW (e.g. ally dropped loot that should be visible to all teammates).
    /// </summary>
    public interface IFogTeamOwned
    {
        /// <summary>
        /// Team ID of this object.
        /// -1 = neutral / unowned (treated as always visible by FogTeamVisibilityBinder).
        /// Must be readable on all clients (synchronized via SyncVar or ObserversRpc).
        /// </summary>
        int FogOwnerTeamId { get; }

        /// <summary>
        /// When true, FogTeamVisibilityBinder skips team comparison and ensures this
        /// object is always shown to all clients (no FogOfWarHider applied).
        ///
        /// Use cases:
        ///   - Dropped loot that all players should see
        ///   - Neutral/environmental deployables
        ///   - Any object where team-based hiding is explicitly opt-out
        /// </summary>
        bool FogAlwaysVisible { get; }
    }
}
