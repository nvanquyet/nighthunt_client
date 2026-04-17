using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// ScriptableObject cấu hình cho một WorldItemSpawnPoint.
    ///
    /// Xác định:
    ///   - Loại object sẽ spawn (Item / Container / Chest)
    ///   - SpawnTable (tỷ lệ item sẽ roll)
    ///   - Thời gian respawn after bị loot
    ///   - Số lượng active tối đa
    ///
    /// Usage: Create asset qua menu "World/World Spawn Config"
    ///        và gán vào WorldItemSpawnPoint trên scene.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldSpawnConfig", menuName = "NightHunt/Gameplay/World Spawn Config")]
    public class WorldSpawnConfig : ScriptableObject
    {
        [Header("Spawn Type")]
        [Tooltip("Loại object sẽ spawn tại điểm này:\n" +
                 "  Item      → WorldItem (item rơi đất, scatter quanh điểm)\n" +
                 "  Container → WorldContainer (thùng / crate / rương / chest)")]
        public WorldSpawnType SpawnType = WorldSpawnType.Item;

        [Header("Loot Table")]
        [Tooltip("Bảng tỷ lệ item. Roll on spawn (Item) hoặc khi mở (Container/Chest).")]
        public SpawnTable SpawnTable;

        [Header("Respawn Settings")]
        [Tooltip("Spawn-point này có spawn lại after bị loot/despawn không?\n" +
                 "  false → chỉ spawn 1 lần duy nhất (one-shot)\n" +
                 "  true  → spawn lại sau RespawnTime")]
        public bool CanRespawn = true;

        [Tooltip("Thời gian chờ (giây) trước on spawn lại.\n" +
                 "Chỉ dùng khi CanRespawn = true.")]
        [Min(1f)]
        public float RespawnTime = 120f;

        [Tooltip("Số lần spawn tối đa tại điểm này.\n" +
                 "  0 = không giới hạn (∞)\n" +
                 "  > 0 = after đạt giới hạn, điểm này ngưng vĩnh viễn.\n" +
                 "Chỉ dùng khi CanRespawn = true.")]
        [Min(0)]
        public int MaxRespawnCount = 0;

        [Header("Capacity")]
        [Tooltip("Số lượng object tối đa được active từ spawn-point này cùng lúc.\n" +
                 "Thường là 1 cho Container/Chest. Có thể > 1 cho Item scatter.")]
        [Min(1)]
        public int MaxActive = 1;

        [Header("Item Scatter — chỉ dùng khi SpawnType = Item")]
        [Tooltip("Bán kính (meters) để scatter các WorldItem xung quanh điểm spawn.")]
        [Min(0f)]
        public float ScatterRadius = 1.5f;

        [Header("Container / Chest — chỉ dùng khi SpawnType = Container/Chest")]
        [Tooltip("Container / Chest bị khóa ngay on spawn.\n" +
                 "Player cần key hoặc unlock logic để mở.")]
        public bool SpawnLocked = false;

        [Header("Container — Auto Reset")]
        [Tooltip("Container / Chest có tự reset trạng thái after đã bị loot không?\n" +
                 "  false → đã loot xập thì cần respawn mới\n" +
                 "  true  → sau ContainerResetDelay giây, rương ‘reset’ lại (có thể mở lại, roll loot mới)")]
        public bool ContainerAutoReset = false;

        [Tooltip("Thời gian (giây) before container tự reset trạng thái.\n" +
                 "Chỉ dùng khi ContainerAutoReset = true.")]
        [Min(1f)]
        public float ContainerResetDelay = 60f;

        [Header("Interaction Config")]
        [Tooltip("Cấu hình cách player tương tác với object spawn ra từ điểm này.\n" +
                 "  Instant → nhấn 1 phát pickup/open\n" +
                 "  Hold    → giữ nút theo HoldDuration\n" +
                 "Nếu null, WorldItem/Container sẽ dùng value mặc định (Instant, 3m).")]
        public LootableConfig LootableConfig;
    }
}
