using UnityEngine;

namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// Scene component marking a world-object spawn location (item / container / chest).
    ///
    /// DESIGN:
    ///   - Stores only position data and a WorldSpawnConfig reference.
    ///   - WorldSpawnManager reads all WorldItemSpawnPoint instances in the scene on server start
    ///     and manages the full lifecycle (spawn → loot → respawn).
    ///   - Contains no spawn logic — correct SRP.
    ///
    /// Gizmos:
    ///   - Sphere = radius spawn (scatter cho Item mode).
    ///   - Label = SpawnType + ConfigName.
    /// </summary>
    public class WorldItemSpawnPoint : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("WorldSpawnConfig defining the object type, SpawnTable, respawn time, etc.")]
        [SerializeField] private WorldSpawnConfig spawnConfig;

        [Header("Visualization")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.6f, 0f, 0.8f);
        [SerializeField] private bool showLabel = true;

        // ── Properties ───────────────────────────────────────────────────────────

        /// <summary>SpawnConfig assigned to this spawn point.</summary>
        public WorldSpawnConfig SpawnConfig => spawnConfig;

        /// <summary>
        /// The type of object to spawn here.
        /// Shortcut so WorldSpawnManager avoids null-checking SpawnConfig every frame.
        /// </summary>
        public WorldSpawnType SpawnType => spawnConfig != null ? spawnConfig.SpawnType : WorldSpawnType.Item;

        // ── Reset state (called by WorldSpawnManager on respawn) ──────────────────

        private int _activeCount;
        private int _spawnCount; // total spawn count over the lifetime of this point

        /// <summary>Number of currently active objects spawned from this point.</summary>
        public int ActiveCount => _activeCount;

        /// <summary>Total number of spawns over the lifetime of this spawn point.</summary>
        public int SpawnCount => _spawnCount;

        /// <summary>True when MaxRespawnCount has been reached and no further spawns will occur.</summary>
        public bool IsExhausted
        {
            get
            {
                if (spawnConfig == null) return true;
                if (!spawnConfig.CanRespawn && _spawnCount > 0) return true; // one-shot: already spawned
                if (spawnConfig.CanRespawn && spawnConfig.MaxRespawnCount > 0
                    && _spawnCount >= spawnConfig.MaxRespawnCount) return true;
                return false;
            }
        }

        /// <summary>True when MaxActive has been reached and no further spawns can occur.</summary>
        public bool IsFull => spawnConfig != null && _activeCount >= spawnConfig.MaxActive;

        internal void RegisterActive()   { _activeCount++; _spawnCount++; }
        internal void UnregisterActive() => _activeCount = Mathf.Max(0, _activeCount - 1);

        // ── Position helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the spawn position.
        /// In Item mode: random point within ScatterRadius.
        /// In Container / Chest mode: exact transform position.
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            if (spawnConfig == null || spawnConfig.SpawnType != WorldSpawnType.Item)
                return transform.position;

            float r = spawnConfig.ScatterRadius;
            if (r <= 0f) return transform.position;

            Vector2 rand = Random.insideUnitCircle * r;
            return transform.position + new Vector3(rand.x, 0f, rand.y);
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            // Center sphere
            //Gizmos.DrawSphere(transform.position, 0.2f);

            // Scatter radius (Item mode only).
            // if (spawnConfig != null && spawnConfig.SpawnType == WorldSpawnType.Item && spawnConfig.ScatterRadius > 0f)
            // {
            //     Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
            //     Gizmos.DrawWireSphere(transform.position, spawnConfig.ScatterRadius);
            // }

            if (showLabel && spawnConfig != null)
            {
                string respawnInfo = spawnConfig.CanRespawn
                    ? (spawnConfig.MaxRespawnCount > 0
                        ? $"Respawn {_spawnCount}/{spawnConfig.MaxRespawnCount} ({spawnConfig.RespawnTime}s)"
                        : $"Respawn ∞ ({spawnConfig.RespawnTime}s)")
                    : "One-shot";
                string exhaustedTag = IsExhausted ? " [DONE]" : "";
                string label = $"{spawnConfig.SpawnType} | {spawnConfig.name}{exhaustedTag}\n{respawnInfo}";
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, label);
            }
        }
#endif
    }
}
