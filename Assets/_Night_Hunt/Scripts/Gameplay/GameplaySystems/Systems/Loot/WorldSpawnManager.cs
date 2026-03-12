using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.World;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Server-authoritative manager quản lý toàn bộ World object spawn lifecycle.
    ///
    /// TRÁCH NHIỆM:
    ///   1. Khi server start: tìm tất cả WorldItemSpawnPoint trong scene và khởi động spawn-loop.
    ///   2. API spawn: SpawnWorldItem, SpawnWorldContainer — dùng prefab có sẵn.
    ///   3. Implement IDropHandler để InventorySystem gọi khi player drop item.
    ///
    /// ═══════════════════════════════════════════════════════════════════════════
    /// CRITICAL — THỨ TỰ SPAWN ĐÚNG:
    ///
    ///   ❌ SAI (cũ):
    ///      ServerManager.Spawn(netObj);     ← FishNet fires OnStartClient() NGAY ĐÂY
    ///      worldItem.Initialize(data);      ← quá trễ, OnStartClient đã chạy rồi
    ///
    ///   ✅ ĐÚNG (mới):
    ///      worldItem.InitializeBeforeSpawn(data);   ← set data trực tiếp, chưa có SyncVar
    ///      ServerManager.Spawn(netObj);              ← FishNet embed SyncVar vào spawn packet
    ///                                                   → client nhận đủ data ngay lần đầu
    ///
    /// Lý do:
    ///   - Host mode: OnStartClient() chạy synchronously BÊN TRONG ServerManager.Spawn().
    ///     Nếu data chưa set trước Spawn → OnStartClient thấy SyncVar rỗng → không spawn model.
    ///   - Dedicated server: SyncVar value được embed vào spawn packet khi Spawn() được gọi.
    ///     Nếu Initialize() gọi sau Spawn → client nhận spawn packet không có data → miss model.
    ///
    ///   InitializeBeforeSpawn() set data trực tiếp lên field + SyncVar TRƯỚC Spawn(),
    ///   nên khi FishNet build spawn packet, SyncVar đã có value → được embed vào packet.
    ///   Cả host lẫn dedicated server client đều nhận đúng data ngay lần đầu.
    /// ═══════════════════════════════════════════════════════════════════════════
    /// </summary>
    public class WorldSpawnManager : NetworkBehaviour, IDropHandler
    {
        // ── Singleton ────────────────────────────────────────────────────────────

        public static WorldSpawnManager Instance { get; private set; }

        // ── Prefab references ────────────────────────────────────────────────────

        [Header("Network Prefabs — gán prefab có NetworkObject component")]
        [Tooltip("Prefab WorldItem (item rơi đất). Phải có NetworkObject + WorldItem component.")]
        [SerializeField]
        private GameObject worldItemPrefab;

        [Tooltip(
            "Prefab WorldContainer (thùng / crate / rương / chest). Phải có NetworkObject + WorldContainer component.")]
        [SerializeField]
        private GameObject worldContainerPrefab;

        // ── Internal ─────────────────────────────────────────────────────────────

        private readonly List<WorldItemSpawnPoint> _spawnPoints = new();

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _spawnPoints.Clear();
            _spawnPoints.AddRange(FindObjectsByType<WorldItemSpawnPoint>(FindObjectsSortMode.None));

            Debug.Log(
                $"[WorldSpawnManager] OnStartServer: found {_spawnPoints.Count} WorldItemSpawnPoint(s). Starting spawn loops.");

            foreach (var point in _spawnPoints)
            {
                if (point.SpawnConfig != null)
                    StartCoroutine(SpawnLoop(point));
            }
        }

        // ── Spawn Loop ───────────────────────────────────────────────────────────

        [Server]
        private IEnumerator SpawnLoop(WorldItemSpawnPoint point)
        {
            var config = point.SpawnConfig;

            while (true)
            {
                if (point.IsExhausted) yield break;

                while (point.IsFull) yield return null;

                SpawnAtPoint(point);

                if (!config.CanRespawn) yield break;

                while (point.IsFull) yield return null;

                if (point.IsExhausted) yield break;

                yield return new WaitForSeconds(config.RespawnTime);
            }
        }

        [Server]
        private void SpawnAtPoint(WorldItemSpawnPoint point)
        {
            var config = point.SpawnConfig;
            if (config == null) return;

            switch (config.SpawnType)
            {
                case WorldSpawnType.Item:
                    // Truyền transform.position (không phải GetSpawnPosition()) để
                    // SpawnWorldItemsFromTable tự scatter mỗi item trong ScatterRadius.
                    // GetSpawnPosition() đã tự scatter → gọi ở đây gây double-scatter.
                    SpawnWorldItemsFromTable(config.SpawnTable, point.transform.position,
                        config.ScatterRadius, point, config.LootableConfig);
                    break;
                case WorldSpawnType.Container:
                    SpawnWorldContainer(config, point.transform.position, point);
                    break;
            }
        }

        // ── Public Spawn API ─────────────────────────────────────────────────────

        /// <summary>
        /// Spawn một WorldItem (item rơi đất). Server-only.
        ///
        /// THỨ TỰ BẮT BUỘC:
        ///   1. Instantiate GO
        ///   2. worldItem.InitializeBeforeSpawn(data)  ← SET DATA TRƯỚC
        ///   3. ServerManager.Spawn(netObj)            ← SPAWN SAU
        ///
        /// Lý do xem comment class WorldSpawnManager.
        /// </summary>
        [Server]
        public WorldItem SpawnWorldItem(
            ItemInstanceData data,
            Vector3 position,
            Quaternion rotation,
            WorldItemSpawnPoint sourcePoint = null,
            LootableConfig lootableConfig = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldItem: server-only!");
                return null;
            }

            if (worldItemPrefab == null)
            {
                Debug.LogError("[WorldSpawnManager] SpawnWorldItem: worldItemPrefab chưa được gán!");
                return null;
            }

            // ── 1. Instantiate ────────────────────────────────────────────────────
            var go = Instantiate(worldItemPrefab, position, rotation);
            var netObj = ComponentResolver.Find<NetworkObject>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            var worldItem = ComponentResolver.Find<WorldItem>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] WorldItem not found")
                .Resolve();

            if (netObj == null || worldItem == null)
            {
                Debug.LogError(
                    "[WorldSpawnManager] SpawnWorldItem: worldItemPrefab thiếu NetworkObject hoặc WorldItem!");
                Destroy(go);
                return null;
            }

            // ── 2. InitializeBeforeSpawn — SET DATA TRƯỚC KHI SPAWN ──────────────
            //    Ghi data trực tiếp vào field + SyncVar nội bộ của WorldItem.
            //    Khi FishNet build spawn packet ở bước 3, SyncVar đã có value
            //    và được embed vào packet → client nhận đủ data ngay lần đầu.
            worldItem.InitializeBeforeSpawn(data, lootableConfig);

            Debug.Log($"[WorldSpawnManager] SpawnWorldItem: InitializeBeforeSpawn done. " +
                      $"defID='{data.DefinitionID}' pos={position}. About to Spawn...");

            // ── 3. Spawn — FishNet tạo NetworkObject, gửi packet tới clients ──────
            ServerManager.Spawn(netObj);

            Debug.Log($"[WorldSpawnManager] SpawnWorldItem: Spawned. " +
                      $"NetId={netObj.ObjectId} Observers={netObj.Observers.Count} defID='{data.DefinitionID}'");

            // ── 4. Đăng ký sourcePoint callback ──────────────────────────────────
            if (sourcePoint != null)
            {
                sourcePoint.RegisterActive();
                worldItem.OnDespawned += () => sourcePoint.UnregisterActive();
            }

            return worldItem;
        }

        /// <summary>
        /// Spawn một WorldContainer (thùng/crate). Server-only.
        ///
        /// THỨ TỰ BẮT BUỘC: InitializeBeforeSpawn → ServerManager.Spawn (tương tự WorldItem).
        /// </summary>
        [Server]
        public WorldContainer SpawnWorldContainer(
            WorldSpawnConfig config,
            Vector3 position,
            WorldItemSpawnPoint sourcePoint = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldContainer: server-only!");
                return null;
            }

            if (worldContainerPrefab == null)
            {
                Debug.LogError("[WorldSpawnManager] SpawnWorldContainer: worldContainerPrefab chưa được gán!");
                return null;
            }

            var go = Instantiate(worldContainerPrefab, position, Quaternion.identity);
            var netObj = ComponentResolver.Find<NetworkObject>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            var container = ComponentResolver.Find<WorldContainer>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] WorldContainer not found")
                .Resolve();

            if (netObj == null || container == null)
            {
                Debug.LogError(
                    "[WorldSpawnManager] SpawnWorldContainer: worldContainerPrefab thiếu NetworkObject hoặc WorldContainer!");
                Destroy(go);
                return null;
            }

            // Set data trước Spawn (tương tự WorldItem)
            container.InitializeBeforeSpawn(config.SpawnTable, config.SpawnLocked,
                config.LootableConfig,
                config.ContainerAutoReset,
                config.ContainerResetDelay);

            ServerManager.Spawn(netObj);

            Debug.Log($"[WorldSpawnManager] SpawnWorldContainer: Spawned. " +
                      $"NetId={netObj.ObjectId} Observers={netObj.Observers.Count}");

            if (sourcePoint != null)
            {
                sourcePoint.RegisterActive();
                container.OnDespawned += () => sourcePoint.UnregisterActive();
            }

            return container;
        }

        /// <summary>Roll SpawnTable và scatter WorldItem quanh centerPosition. Server-only.</summary>
        [Server]
        public void SpawnWorldItemsFromTable(
            SpawnTable table,
            Vector3 centerPosition,
            float spreadRadius = 1.5f,
            WorldItemSpawnPoint sourcePoint = null,
            LootableConfig lootableConfig = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldItemsFromTable: server-only!");
                return;
            }

            if (table == null)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldItemsFromTable: SpawnTable is null!");
                return;
            }

            var results = table.Roll();
            Debug.Log(
                $"[WorldSpawnManager] SpawnWorldItemsFromTable: rolled {results.Count} item(s) from '{table.name}'");

            foreach (var result in results)
            {
                if (result.ItemDef == null) continue;

                var instance = new ItemInstance(result.ItemDef.ItemID, result.Quantity, -1);
                var data = instance.ToData();

                Vector2 rand = Random.insideUnitCircle * spreadRadius;
                Vector3 spawnPos = centerPosition + new Vector3(rand.x, 0f, rand.y);

                SpawnWorldItem(data, spawnPos, Quaternion.identity, sourcePoint, lootableConfig);
            }
        }

        /// <summary>Spawn WorldItem từ danh sách SpawnResult đã roll sẵn. Server-only.</summary>
        [Server]
        public void SpawnWorldItemsFromResults(
            List<SpawnResult> results,
            Vector3 centerPosition,
            float spreadRadius = 1.5f,
            WorldItemSpawnPoint sourcePoint = null,
            LootableConfig lootableConfig = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldItemsFromResults: server-only!");
                return;
            }

            foreach (var result in results)
            {
                if (result.ItemDef == null) continue;

                var instance = new ItemInstance(result.ItemDef.ItemID, result.Quantity, -1);
                Vector2 rand = Random.insideUnitCircle * spreadRadius;
                Vector3 pos = centerPosition + new Vector3(rand.x, 0f, rand.y);
                SpawnWorldItem(instance.ToData(), pos, Quaternion.identity, sourcePoint, lootableConfig);
            }
        }

        // ── IDropHandler ─────────────────────────────────────────────────────────

        [Server]
        void IDropHandler.SpawnPickup(ItemInstanceData data, Vector3 position, Quaternion rotation)
            => SpawnWorldItem(data, position, rotation);

        [Server]
        void IDropHandler.SpawnPickupsFromTable(SpawnTable table, Vector3 centerPosition, float spreadRadius)
            => SpawnWorldItemsFromTable(table, centerPosition, spreadRadius);
    }
}