using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// ScriptableObject that configures a single WorldItemSpawnPoint.
    ///
    /// Defines:
    ///   - The type of object to spawn (Item / Container / Chest)
    ///   - SpawnTable (item roll weights)
    ///   - Respawn delay after being looted
    ///   - Maximum number of active spawns
    ///
    /// Usage: Create asset qua menu "World/World Spawn Config"
    ///        and assign it to a WorldItemSpawnPoint in the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldSpawnConfig", menuName = "NightHunt/Gameplay/World Spawn Config")]
    public class WorldSpawnConfig : ScriptableObject
    {
        [Header("Spawn Type")]
        [Tooltip("The type of object to spawn at this point.\nItem → WorldItem (ground drop, scattered around the point).\nContainer → WorldContainer (crate / chest / loot box).")]
        public WorldSpawnType SpawnType = WorldSpawnType.Item;

        [Header("Loot Table")]
        [Tooltip("Item loot table. Rolled on spawn (Item) or when opened (Container/Chest).")]
        public SpawnTable SpawnTable;

        [Header("Respawn Settings")]
        [Tooltip("Whether this spawn point respawns after being looted/despawned.\nfalse → one-shot only.\ntrue → respawns after RespawnTime.")]
        public bool CanRespawn = true;

        [Tooltip("Delay in seconds before respawning. Only used when CanRespawn = true.")]
        [Min(1f)]
        public float RespawnTime = 120f;

        [Tooltip("Maximum number of respawns at this point.\n0 = unlimited.\n> 0 = stops permanently after reaching the limit.\nOnly used when CanRespawn = true.")]
        [Min(0)]
        public int MaxRespawnCount = 0;

        [Header("Capacity")]
        [Tooltip("Maximum number of objects active from this spawn point simultaneously.\nTypically 1 for Container/Chest; may be > 1 for item scatter.")]
        [Min(1)]
        public int MaxActive = 1;

        [Header("Item Scatter — SpawnType = Item only")]
        [Tooltip("Radius (meters) to scatter WorldItems around the spawn point.")]
        [Min(0f)]
        public float ScatterRadius = 1.5f;

        [Header("Container / Chest — SpawnType = Container/Chest only")]
        [Tooltip("Container / Chest spawns locked. Player needs a key or unlock logic to open it.")]
        public bool SpawnLocked = false;

        [Header("Container — Auto Reset")]
        [Tooltip("Whether the container auto-resets after being looted.\nfalse → requires a fresh respawn.\ntrue → resets after ContainerResetDelay seconds (can be opened again with new loot).")]
        public bool ContainerAutoReset = false;

        [Tooltip("Delay in seconds before the container resets. Only used when ContainerAutoReset = true.")]
        [Min(1f)]
        public float ContainerResetDelay = 60f;

        [Header("Interaction Config")]
        [Tooltip("How the player interacts with objects spawned from this point.\nInstant → single press to pick up / open.\nHold → hold button for HoldDuration.\nIf null, WorldItem/Container uses defaults (Instant, 3 m).")]
        public LootableConfig LootableConfig;
    }
}
