
namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Classifies a bullet-acquirable target by game-object type.
    /// Used by <see cref="BulletTargetConfig"/> to define acquisition priority order.
    ///
    /// Lower numeric value = higher default priority when no custom order is configured,
    /// but the canonical priority is the ordered list in <see cref="BulletTargetConfig.PriorityOrder"/>.
    /// </summary>
    public enum HittableTargetType
    {
        /// <summary>Player-controlled characters and AI units. Always highest priority.</summary>
        Character   = 0,

        /// <summary>Respawn beacons and item beacons placed by players.</summary>
        Beacon      = 1,

        /// <summary>Player-deployed objects: vision wards, deployable shields, etc.</summary>
        Deployable  = 2,

        /// <summary>Interactive world containers, loot chests, corpses.</summary>
        WorldObject = 3,

        /// <summary>Static destructible structures: doors, switches, barrier walls.</summary>
        Structure   = 4,

        /// <summary>Catch-all for any registered target not covered above.</summary>
        Misc        = 99,
    }
}
