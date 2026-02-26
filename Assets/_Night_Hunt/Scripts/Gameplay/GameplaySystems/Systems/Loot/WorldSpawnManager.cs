using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.World;

namespace NightHunt.GameplaySystems.Loot
{
    /// <summary>
    /// Server-authoritative manager quản lý toàn bộ World object spawn lifecycle.
    ///
    /// TRÁCH NHIỆM:
    ///   1. Khi server start: tìm tất cả <see cref="WorldItemSpawnPoint"/> trong scene
    ///      và khởi động spawn-loop cho từng điểm.
    ///   2. API spawn: <see cref="SpawnWorldItem"/>, <see cref="SpawnWorldContainer"/>
    ///      — dùng prefab có sẵn, không tạo GO động.
    ///   3. Implement <see cref="IDropHandler"/> để InventorySystem gọi khi player drop item.
    ///
    /// NAMING CONVENTION (prefix "World"):
    ///   WorldItem      — item rơi trên đất (pickup)
    ///   WorldContainer — thùng / crate / rương / chest (tất cả dùng 1 prefab)
    /// </summary>
    public class WorldSpawnManager : NetworkBehaviour, IDropHandler
    {
        // ── Singleton ────────────────────────────────────────────────────────────

        public static WorldSpawnManager Instance { get; private set; }

        // ── Prefab references ────────────────────────────────────────────────────

        [Header("Network Prefabs — gán prefab có NetworkObject component")]
        [Tooltip("Prefab WorldItem (item rơi đất). Phải có NetworkObject + WorldItem component.")]
        [SerializeField] private GameObject worldItemPrefab;

        [Tooltip("Prefab WorldContainer (thùng / crate / rương / chest). Phải có NetworkObject + WorldContainer component.")]
        [SerializeField] private GameObject worldContainerPrefab;

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

            Debug.Log($"[WorldSpawnManager] Found {_spawnPoints.Count} WorldItemSpawnPoint(s). Starting spawn loops.");

            foreach (var point in _spawnPoints)
            {
                if (point.SpawnConfig != null)
                    StartCoroutine(SpawnLoop(point));
            }
        }

        // ── Spawn Loop (server) ──────────────────────────────────────────────────

        [Server]
        private IEnumerator SpawnLoop(WorldItemSpawnPoint point)
        {
            var config = point.SpawnConfig;

            while (true)
            {
                // Điểm đã hết lượt spawn → dừng vĩnh viễn
                if (point.IsExhausted) yield break;

                // Chờ đến khi có chỗ trống
                while (point.IsFull) yield return null;

                SpawnAtPoint(point);

                // One-shot: không có respawn → dừng sau lần spawn đầu tiên
                if (!config.CanRespawn) yield break;

                // Chờ đến khi object bị loot/despawn (slot trống lại)
                while (point.IsFull) yield return null;

                // Kiểm tra đã hết số lần respawn chưa
                if (point.IsExhausted) yield break;

                // Chờ thời gian respawn
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
                    SpawnWorldItemsFromTable(config.SpawnTable, point.GetSpawnPosition(), config.ScatterRadius, point, config.LootableConfig);
                    break;
                case WorldSpawnType.Container:
                    SpawnWorldContainer(config, point.transform.position, point);
                    break;
            }
        }

        // ── Public Spawn API ─────────────────────────────────────────────────────

        /// <summary>Spawn một WorldItem (item rơi đất). Server-only.</summary>
        [Server]
        public WorldItem SpawnWorldItem(ItemInstanceData data, Vector3 position, Quaternion rotation, WorldItemSpawnPoint sourcePoint = null, LootableConfig lootableConfig = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldItem: server-only!");
                return null;
            }

            if (worldItemPrefab == null)
            {
                Debug.LogError("[WorldSpawnManager] worldItemPrefab is not assigned!");
                return null;
            }

            var go = Instantiate(worldItemPrefab, position, rotation);
            var netObj = go.GetComponent<NetworkObject>();
            var worldItem = go.GetComponent<WorldItem>();

            if (netObj == null || worldItem == null)
            {
                Debug.LogError("[WorldSpawnManager] worldItemPrefab thiếu NetworkObject hoặc WorldItem component!");
                Destroy(go);
                return null;
            }

            ServerManager.Spawn(netObj);
            worldItem.Initialize(data, lootableConfig);

            if (sourcePoint != null)
            {
                sourcePoint.RegisterActive();
                worldItem.OnDespawned += () => sourcePoint.UnregisterActive();
            }

            return worldItem;
        }

        /// <summary>Spawn một WorldContainer (thùng/crate). Server-only.</summary>
        [Server]
        public WorldContainer SpawnWorldContainer(WorldSpawnConfig config, Vector3 position, WorldItemSpawnPoint sourcePoint = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WorldSpawnManager] SpawnWorldContainer: server-only!");
                return null;
            }

            if (worldContainerPrefab == null)
            {
                Debug.LogError("[WorldSpawnManager] worldContainerPrefab is not assigned!");
                return null;
            }

            var go = Instantiate(worldContainerPrefab, position, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            var container = go.GetComponent<WorldContainer>();

            if (netObj == null || container == null)
            {
                Debug.LogError("[WorldSpawnManager] worldContainerPrefab thiếu NetworkObject hoặc WorldContainer component!");
                Destroy(go);
                return null;
            }

            ServerManager.Spawn(netObj);
            container.Initialize(config.SpawnTable, config.SpawnLocked, config.LootableConfig,
                                  config.ContainerAutoReset, config.ContainerResetDelay);

            if (sourcePoint != null)
            {
                sourcePoint.RegisterActive();
                container.OnDespawned += () => sourcePoint.UnregisterActive();
            }

            return container;
        }

        /// <summary>Roll SpawnTable và scatter WorldItem quanh centerPosition. Server-only.</summary>
        [Server]
        public void SpawnWorldItemsFromTable(SpawnTable table, Vector3 centerPosition, float spreadRadius = 1.5f, WorldItemSpawnPoint sourcePoint = null, LootableConfig lootableConfig = null)
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

        /// <summary>
        /// Spawn WorldItem từ danh sách SpawnResult đã roll sẵn.
        /// Server-only.
        /// </summary>
        [Server]
        public void SpawnWorldItemsFromResults(List<SpawnResult> results, Vector3 centerPosition, float spreadRadius = 1.5f, WorldItemSpawnPoint sourcePoint = null, LootableConfig lootableConfig = null)
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
