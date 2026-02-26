using UnityEngine;

namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// Scene component đánh dấu một vị trí spawn World object (item/container/chest).
    ///
    /// DESIGN:
    ///   - Chỉ chứa dữ liệu vị trí + WorldSpawnConfig.
    ///   - WorldSpawnManager đọc tất cả WorldItemSpawnPoint trong scene khi server start
    ///     và quản lý toàn bộ lifecycle (spawn → loot → respawn).
    ///   - Không chứa logic spawn — đúng SRP.
    ///
    /// Gizmos:
    ///   - Sphere = radius spawn (scatter cho Item mode).
    ///   - Label = SpawnType + ConfigName.
    /// </summary>
    public class WorldItemSpawnPoint : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("WorldSpawnConfig xác định loại object, SpawnTable, respawn time, v.v.")]
        [SerializeField] private WorldSpawnConfig spawnConfig;

        [Header("Visualization")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.6f, 0f, 0.8f);
        [SerializeField] private bool showLabel = true;

        // ── Properties ───────────────────────────────────────────────────────────

        /// <summary>Config gắn vào điểm spawn này.</summary>
        public WorldSpawnConfig SpawnConfig => spawnConfig;

        /// <summary>
        /// Loại object sẽ spawn tại đây.
        /// Shortcut để WorldSpawnManager không cần null-check SpawnConfig mỗi lần.
        /// </summary>
        public WorldSpawnType SpawnType => spawnConfig != null ? spawnConfig.SpawnType : WorldSpawnType.Item;

        // ── Reset state (dùng bởi WorldSpawnManager khi respawn) ─────────────────

        private int _activeCount;
        private int _spawnCount; // tổng số lần đã spawn

        /// <summary>Số object hiện đang active từ điểm spawn này.</summary>
        public int ActiveCount => _activeCount;

        /// <summary>Tổng số lần đã spawn (cả cuộc đời của spawn-point).</summary>
        public int SpawnCount => _spawnCount;

        /// <summary>True nếu đã đạt MaxRespawnCount và không spawn nữa.</summary>
        public bool IsExhausted
        {
            get
            {
                if (spawnConfig == null) return true;
                if (!spawnConfig.CanRespawn && _spawnCount > 0) return true; // one-shot: đã spawn rồi
                if (spawnConfig.CanRespawn && spawnConfig.MaxRespawnCount > 0
                    && _spawnCount >= spawnConfig.MaxRespawnCount) return true;
                return false;
            }
        }

        /// <summary>True nếu đã đạt MaxActive và không thể spawn thêm.</summary>
        public bool IsFull => spawnConfig != null && _activeCount >= spawnConfig.MaxActive;

        internal void RegisterActive()   { _activeCount++; _spawnCount++; }
        internal void UnregisterActive() => _activeCount = Mathf.Max(0, _activeCount - 1);

        // ── Position helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Trả về vị trí spawn.
        /// Với Item mode: random trong ScatterRadius.
        /// Với Container / Chest: chính xác vị trí của transform.
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
            Gizmos.DrawSphere(transform.position, 0.2f);

            // Scatter radius (chỉ cho Item mode)
            if (spawnConfig != null && spawnConfig.SpawnType == WorldSpawnType.Item && spawnConfig.ScatterRadius > 0f)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
                Gizmos.DrawWireSphere(transform.position, spawnConfig.ScatterRadius);
            }

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
