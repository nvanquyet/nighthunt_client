using UnityEngine;

namespace NightHunt.Core
{
    // ──────────────────────────────────────────────────────────────────────────
    //  NightHuntLayers  &  NightHuntTags
    //  Single source of truth for all layer names, layer IDs, composite masks,
    //  and tag names used throughout the project.
    //
    //  ┌─────────────────────────────────────────────────────────────────────┐
    //  │  CANONICAL LAYER MAP  (ProjectSettings/TagManager.asset)            │
    //  ├─────┬────────────────┬──────────────────────────────────────────────┤
    //  │Slot │ Name           │ Purpose                                      │
    //  ├─────┼────────────────┼──────────────────────────────────────────────┤
    //  │  0  │ Default        │ Unity reserved                               │
    //  │  1  │ TransparentFX  │ Unity reserved                               │
    //  │  2  │ Ignore Raycast │ Unity reserved                               │
    //  │  3  │ (empty)        │ Unity reserved – never use                   │
    //  │  4  │ Water          │ Unity reserved                               │
    //  │  5  │ UI             │ Unity reserved                               │
    //  │  6  │ Player         │ Player root + model root + CharacterController│
    //  │  7  │ PlayerHitBox   │ Per-bone hitbox colliders (damage detection) │
    //  │  8  │ Projectile     │ Weapon bullets / rockets in flight           │
    //  │  9  │ (empty)        │ reserved                                     │
    //  │ 10  │ Interactable   │ Pickup triggers, doors, interactive props    │
    //  │ 11  │ Zone           │ Trigger volumes: lockdown, buff, safe-zones  │
    //  │ 12  │ Throwable      │ Grenade / throwable physics bodies           │
    //  │ 13  │ DeadCharacter  │ Ragdoll dead-player bodies                   │
    //  │ 14  │ (empty)        │ reserved                                     │
    //  │ 15  │ MapObstacle    │ Map-level physics obstacles (covers, crates) │
    //  │ 20  │ Items          │ Dropped world-item 3-D model (physics/visual)│
    //  │ 21  │ Minimap        │ Minimap markers (minimap camera only)        │
    //  │ 22  │ MapStatic      │ Static architecture always visible in FOW    │
    //  │ 23  │ Wall           │ Map walls — blocks physics + line-of-sight   │
    //  │ 24  │ Ground         │ Terrain / floor — aim raycast, nav reference │
    //  │ 25  │ SeeThrough     │ See-through shader replacement meshes        │
    //  └─────┴────────────────┴──────────────────────────────────────────────┘
    //
    //  FOG-OF-WAR rendering rules (enforced via FogTeamVisibilityBinder):
    //   • Same-team players  → NO FogOfWarHider  → always visible
    //   • Enemy players      → FogOfWarHider     → hidden outside vision cone
    //   • Wall / Ground / MapStatic / MapObstacle → no hider; visible (darkened in fog)
    //   • Dropped Items      → designer choice (attach FogOfWarHider to hide in fog)
    //   • SeeThrough mesh    → visual replacement only, camera line-of-sight triggers it
    //
    //  SEE-THROUGH SHADER rules:
    //   • Layer "SeeThrough"  → mesh on which the see-through material is applied
    //   • Layer "Wall"        → physics collider that actually blocks movement
    //   • Multi-level floors  → collider → "Ground"; ceiling visual → "SeeThrough"
    //     (STS activates when player is below AND camera ray passes through the ceiling)
    //   • Pure floor (ground) → "Ground" only; STS should NOT be applied
    //
    //  MINIMAP camera cullingMask:
    //   Set to:  Minimap | Ground | Wall | MapStatic | MapObstacle
    //   Do NOT include: Player, Items, Projectile etc.  (clutters minimap)
    //   Add MinimapMarkerController to player / objective prefabs (auto-sets layer).
    // ──────────────────────────────────────────────────────────────────────────

    public static class NightHuntLayers
    {
        // ── Layer name constants ────────────────────────────────────────────
        public const string Default         = "Default";
        public const string Player          = "Player";
        public const string PlayerHitBox    = "PlayerHitBox";
        public const string Projectile      = "Projectile";
        public const string Interactable    = "Interactable";
        public const string Zone            = "Zone";
        public const string Throwable       = "Throwable";
        public const string DeadCharacter   = "DeadCharacter";
        public const string MapObstacle     = "MapObstacle";
        public const string Items           = "Items";
        public const string Minimap         = "Minimap";
        public const string MapStatic       = "MapStatic";
        public const string Wall            = "Wall";
        public const string Ground          = "Ground";
        public const string SeeThrough      = "SeeThrough";

        // ── Lazy-cached layer IDs ───────────────────────────────────────────
        // All cached as -2 so unresolved (= -1) can be distinguished from uncached.
        private static int _player          = -2;
        private static int _playerHitBox    = -2;
        private static int _projectile      = -2;
        private static int _interactable    = -2;
        private static int _zone            = -2;
        private static int _throwable       = -2;
        private static int _deadCharacter   = -2;
        private static int _mapObstacle     = -2;
        private static int _items           = -2;
        private static int _minimap         = -2;
        private static int _mapStatic       = -2;
        private static int _wall            = -2;
        private static int _ground          = -2;
        private static int _seeThrough      = -2;

        public static int IdPlayer          => Resolve(ref _player,          Player);
        public static int IdPlayerHitBox    => Resolve(ref _playerHitBox,    PlayerHitBox);
        public static int IdProjectile      => Resolve(ref _projectile,      Projectile);
        public static int IdInteractable    => Resolve(ref _interactable,    Interactable);
        public static int IdZone            => Resolve(ref _zone,            Zone);
        public static int IdThrowable       => Resolve(ref _throwable,       Throwable);
        public static int IdDeadCharacter   => Resolve(ref _deadCharacter,   DeadCharacter);
        public static int IdMapObstacle     => Resolve(ref _mapObstacle,     MapObstacle);
        public static int IdItems           => Resolve(ref _items,           Items);
        public static int IdMinimap         => Resolve(ref _minimap,         Minimap);
        public static int IdMapStatic       => Resolve(ref _mapStatic,       MapStatic);
        public static int IdWall            => Resolve(ref _wall,            Wall);
        public static int IdGround          => Resolve(ref _ground,          Ground);
        public static int IdSeeThrough      => Resolve(ref _seeThrough,      SeeThrough);

        private static int Resolve(ref int cached, string name)
        {
            if (cached == -2) cached = LayerMask.NameToLayer(name);
            return cached;
        }

        /// <summary>
        /// Call once after ProjectSettings layers are confirmed valid (e.g. on game start).
        /// Logs a warning for any layer that could not be resolved.
        /// </summary>
        public static void ValidateAll()
        {
            Check(IdPlayer,          Player);
            Check(IdPlayerHitBox,    PlayerHitBox);
            Check(IdProjectile,      Projectile);
            Check(IdInteractable,    Interactable);
            Check(IdZone,            Zone);
            Check(IdThrowable,       Throwable);
            Check(IdDeadCharacter,   DeadCharacter);
            Check(IdMapObstacle,     MapObstacle);
            Check(IdItems,           Items);
            Check(IdMinimap,         Minimap);
            Check(IdMapStatic,       MapStatic);
            Check(IdWall,            Wall);
            Check(IdGround,          Ground);
            Check(IdSeeThrough,      SeeThrough);
        }

        private static void Check(int id, string name)
        {
            if (id == -1)
                Debug.LogWarning($"[NightHuntLayers] Layer '{name}' not found in ProjectSettings. " +
                                 "Run  NightHunt ▸ Tools ▸ Layer & Tag Setup  to fix.");
        }

        // ── Composite LayerMask helpers ─────────────────────────────────────

        /// <summary>Masks for hitscan raycasts — bullets stop on these.</summary>
        public static LayerMask MaskHitscanBlock =>
            LayerMask.GetMask(PlayerHitBox, Wall, Ground, MapObstacle);

        /// <summary>Obstacle mask for boss turret / AI line-of-sight checks.</summary>
        public static LayerMask MaskLOSObstacles =>
            LayerMask.GetMask(Wall, Ground, MapObstacle, MapStatic);

        /// <summary>Layers the aim-ground raycast (CombatInputHandler / AimSystem) hits.</summary>
        public static LayerMask MaskGroundAim =>
            LayerMask.GetMask(Ground);

        /// <summary>Interaction proximity sphere cast (InteractionSystem).</summary>
        public static LayerMask MaskInteraction =>
            LayerMask.GetMask(Interactable);

        /// <summary>Zone trigger detection (ZoneBuff, LockdownZone).</summary>
        public static LayerMask MaskZones =>
            LayerMask.GetMask(Zone);

        /// <summary>Minimap markers only.</summary>
        public static LayerMask MaskMinimapOnly =>
            LayerMask.GetMask(Minimap);

        /// <summary>
        /// Minimap camera cullingMask — renders map background + player markers.
        /// Assign this to the <c>MinimapCameraController</c> camera in the Inspector.
        /// </summary>
        public static LayerMask MaskMinimapCamera =>
            LayerMask.GetMask(Minimap, Ground, Wall, MapStatic, MapObstacle);

        /// <summary>
        /// FOW shadow-casting obstacles (used by FogOfWarRevealer3D obstacle detection).
        /// </summary>
        public static LayerMask MaskFOWObstacles =>
            LayerMask.GetMask(Wall, MapStatic, MapObstacle);

        /// <summary>
        /// Layers the SeeThrough PlayerToCameraRaycastTriggerManager should scan.
        /// Include the wall layers that can physically stand between player and camera.
        /// </summary>
        public static LayerMask MaskSeeThroughObstacles =>
            LayerMask.GetMask(Wall, MapObstacle, SeeThrough);

        /// <summary>
        /// Layers that receive NavMesh baking (static walkable/unwalkable surfaces).
        /// </summary>
        public static LayerMask MaskNavMeshBake =>
            LayerMask.GetMask(Ground, Wall, MapStatic, MapObstacle);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  NightHuntTags
    //  All tags used by the project. Built-in Unity tags (Untagged, Respawn,
    //  Finish, EditorOnly, MainCamera, Player, GameController) are excluded
    //  because they are always present.
    //
    //  NOTE: NightHunt uses component interfaces (IInteractable, IHittable, …)
    //  rather than tag-based GetComponent lookups in critical paths.
    //  Tags here are kept for specialised lookups and debug filtering.
    // ──────────────────────────────────────────────────────────────────────────

    public static class NightHuntTags
    {
        /// <summary>Generic enemy entity (NPCs, boss, turrets).</summary>
        public const string Enemy        = "Enemy";

        /// <summary>Dropped loot or world-spawn item.</summary>
        public const string Pickup       = "Pickup";

        /// <summary>Destructible prop in the world.</summary>
        public const string Destroyable  = "Destroyable";

        /// <summary>General interactable map object (door, switch).</summary>
        public const string Usable       = "Usable";

        /// <summary>Deployed beacon (RespawnBeacon prefab).</summary>
        public const string Beacon       = "Beacon";

        /// <summary>Vehicle entity.</summary>
        public const string Vehicle      = "Vehicle";

        /// <summary>Game-logic management object (NetworkGameManager, ServerGameManager…).</summary>
        public const string GameManager  = "GameController";

        /// <summary>AI-controlled player-like unit.</summary>
        public const string AIPlayer     = "AIPlayer";

        /// <summary>AI neutral / wildlife unit.</summary>
        public const string AINeutral    = "AINeutral";
    }
}
