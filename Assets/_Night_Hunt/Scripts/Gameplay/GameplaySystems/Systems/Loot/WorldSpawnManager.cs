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
    /// Server-authoritative manager for the full World-object spawn lifecycle.
    ///
    /// RESPONSIBILITIES:
    ///   1. On server start: discover all WorldItemSpawnPoint objects in the scene and begin spawn loops.
    ///   2. Spawn API: SpawnWorldItem, SpawnWorldContainer — uses pre-assigned prefabs.
    ///   3. Implement IDropHandler so InventorySystem can call on player drop.
    ///
    /// ═══════════════════════════════════════════════════════════════════════════
    /// CRITICAL — CORRECT SPAWN ORDER:
    ///
    ///   ❌ WRONG (old):
    ///      ServerManager.Spawn(netObj);     ← FishNet fires OnStartClient() HERE
    ///      worldItem.Initialize(data);      ← too late, OnStartClient already ran
    ///
    ///   ✅ CORRECT (new):
    ///      worldItem.InitializeBeforeSpawn(data);   ← set data directly, not yet available SyncVar
    ///      ServerManager.Spawn(netObj);              ← FishNet embeds SyncVar into spawn packet
    ///                                                   → client receives full data on first sync
    ///
    /// Reason:
    ///   - Host mode: OnStartClient() runs synchronously INSIDE ServerManager.Spawn().
    ///     If data is not set before Spawn, OnStartClient sees an empty SyncVar → no model spawned.
    ///   - Dedicated server: SyncVar value is embedded in the spawn packet when Spawn() is called.
    ///     If Initialize() is called after Spawn → client receives spawn packet with no data → misses model.
    ///
    ///   InitializeBeforeSpawn() sets data directly onto the field + SyncVar BEFORE Spawn(),
    ///   so when FishNet builds the spawn packet the SyncVar already has a value → embedded in packet.
    ///   Both host and dedicated-server clients receive correct data on first sync.
    /// ═══════════════════════════════════════════════════════════════════════════
    /// </summary>
    public class WorldSpawnManager : NetworkBehaviour, IDropHandler
    {
        // ── Singleton ────────────────────────────────────────────────────────────

        public static WorldSpawnManager Instance { get; private set; }

        // ── Prefab references ────────────────────────────────────────────────────

        [Header("Network Prefabs")]
        [Tooltip("WorldItem prefab (item on the ground). Must have NetworkObject + WorldItem components.")]
        [SerializeField]
        private GameObject worldItemPrefab;

        [Tooltip("WorldContainer prefab (crate / chest). Must have NetworkObject + WorldContainer components.")]
        [SerializeField]
        private GameObject worldContainerPrefab;

        // ── Limits ───────────────────────────────────────────────────────────────

        [Header("Spawn Limits")]
        [Tooltip("Maximum number of active WorldItem NetworkObjects allowed at once. When reached, the oldest unclaimed item is despawned before a new one is spawned.")]
        [SerializeField] private int _maxActiveItems = 100;

        // ── Internal ─────────────────────────────────────────────────────────────

        private readonly List<WorldItemSpawnPoint> _spawnPoints = new();
        // Oldest-first queue of active WorldItems for limit enforcement.
        private readonly Queue<WorldItem> _activeWorldItems = new();

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

            // Stagger initial spawns so all points don't fire in the same frame.
            yield return null;

            while (true)
            {
                if (point.IsExhausted) yield break;

                while (point.IsFull) yield return null;

                SpawnAtPoint(point);
                // Yield at least one frame after spawning to avoid same-frame re-entry.
                yield return null;

                if (!config.CanRespawn) yield break;

                // A spawn point owns a whole batch. Do not keep spawning every frame just
                // because MaxActive is larger than the batch size.
                while (point.ActiveCount > 0) yield return null;

                if (point.IsExhausted) yield break;

                // Minimum 1 s between respawns to prevent busyloop when RespawnTime == 0.
                yield return new WaitForSeconds(Mathf.Max(1f, config.RespawnTime));
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
                Debug.LogError("[WorldSpawnManager] SpawnWorldItem: worldItemPrefab is not assigned!");
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
                    "[WorldSpawnManager] SpawnWorldItem: worldItemPrefab is missing NetworkObject or WorldItem component!");
                Destroy(go);
                return null;
            }

            // ── 2. InitializeBeforeSpawn — SET DATA BEFORE SPAWN ──────────────
            //    Write data directly onto the WorldItem field + internal SyncVar.
            //    When FishNet builds the spawn packet in step 3, the SyncVar already
            //    has a value and is embedded in the packet → client receives full data
            //    on the very first sync.
            worldItem.InitializeBeforeSpawn(data, lootableConfig);

            Debug.Log($"[WorldSpawnManager] SpawnWorldItem: InitializeBeforeSpawn done. " +
                      $"defID='{data.DefinitionID}' pos={position}. About to Spawn...");

            // ── 3. Enforce active-item limit ──────────────────────────────────────
            if (_maxActiveItems > 0 && _activeWorldItems.Count >= _maxActiveItems)
            {
                if (_activeWorldItems.TryDequeue(out var oldest) && oldest != null)
                {
                    Debug.LogWarning($"[WorldSpawnManager] Active item limit ({_maxActiveItems}) reached — despawning oldest item '{oldest.name}'.");
                    ServerManager.Despawn(oldest.NetworkObject);
                }
            }

            // ── 4. Spawn — FishNet creates the NetworkObject and sends packet to clients ──
            ServerManager.Spawn(netObj);

            Debug.Log($"[WorldSpawnManager] SpawnWorldItem: Spawned. " +
                      $"NetId={netObj.ObjectId} Observers={netObj.Observers.Count} defID='{data.DefinitionID}'");

            // ── 5. Track and register sourcePoint callback ────────────────────────
            _activeWorldItems.Enqueue(worldItem);
            worldItem.OnDespawned += () =>
            {
                // Remove from queue when despawned (can't efficiently remove from middle,
                // so we just let the limit enforcement clean up stale entries).
                // Re-enqueue with a null guard is not needed — the item won't spawn again.
            };

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
            LogLootFlow($"[00][SpawnContainer.Request] config={(config != null ? config.name : "null")} pos={position:F2} source={(sourcePoint != null ? sourcePoint.name : "null")}");
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnContainer.Blocked] reason=ServerOnly");
                return null;
            }

            if (config == null)
            {
                Debug.LogError("[LOOT_FLOW] SpawnManager [00][SpawnContainer.Blocked] reason=WorldSpawnConfigNull");
                return null;
            }

            if (worldContainerPrefab == null)
            {
                Debug.LogError("[LOOT_FLOW] SpawnManager [00][SpawnContainer.Blocked] reason=WorldContainerPrefabNull");
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
                    "[LOOT_FLOW] SpawnManager [00][SpawnContainer.Blocked] reason=PrefabMissingNetworkObjectOrWorldContainer");
                Destroy(go);
                return null;
            }

            // Set data before Spawn (same pattern as WorldItem)
            container.InitializeBeforeSpawn(config.SpawnTable, config.SpawnLocked,
                config.LootableConfig,
                config.ContainerAutoReset,
                config.ContainerResetDelay,
                config.DropToWorldOnOpen);

            ServerManager.Spawn(netObj);

            LogLootFlow($"[00][SpawnContainer.Done] obj={netObj.ObjectId} table={(config?.SpawnTable != null ? config.SpawnTable.name : "null")} locked={config?.SpawnLocked} storage={container.GetStorage()?.Count ?? 0}");
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
                Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnItemsFromTable.Blocked] reason=ServerOnly");
                return;
            }

            if (table == null)
            {
                Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnItemsFromTable.Blocked] reason=SpawnTableNull");
                return;
            }

            var results = table.Roll();
            LogLootFlow($"[00][SpawnItemsFromTable.Roll] table='{table.name}' rolled={results.Count} center={centerPosition:F2} spread={spreadRadius:F1}");
            Debug.Log(
                $"[WorldSpawnManager] SpawnWorldItemsFromTable: rolled {results.Count} item(s) from '{table.name}'");

            foreach (var result in results)
            {
                if (result.ItemDef == null)
                {
                    Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnItemsFromTable.Skip] reason=ItemDefNull");
                    continue;
                }

                var data = ItemInstanceFactory.CreateData(result.ItemDef, result.Quantity, -1);

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
                Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnItemsFromResults.Blocked] reason=ServerOnly");
                return;
            }

            if (results == null)
            {
                Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnItemsFromResults.Blocked] reason=ResultsNull");
                return;
            }

            LogLootFlow($"[00][SpawnItemsFromResults.Start] count={results.Count} center={centerPosition:F2} spread={spreadRadius:F1}");
            foreach (var result in results)
            {
                if (result.ItemDef == null)
                {
                    Debug.LogWarning("[LOOT_FLOW] SpawnManager [00][SpawnItemsFromResults.Skip] reason=ItemDefNull");
                    continue;
                }

                Vector2 rand = Random.insideUnitCircle * spreadRadius;
                Vector3 pos = centerPosition + new Vector3(rand.x, 0f, rand.y);
                SpawnWorldItem(ItemInstanceFactory.CreateData(result.ItemDef, result.Quantity, -1), pos, Quaternion.identity, sourcePoint, lootableConfig);
            }
        }

        // ── IDropHandler ─────────────────────────────────────────────────────────

        [Server]
        void IDropHandler.SpawnPickup(ItemInstanceData data, Vector3 position, Quaternion rotation)
            => SpawnWorldItem(data, position, rotation);

        [Server]
        void IDropHandler.SpawnPickupsFromTable(SpawnTable table, Vector3 centerPosition, float spreadRadius)
            => SpawnWorldItemsFromTable(table, centerPosition, spreadRadius);

        private static void LogLootFlow(string message)
        {
            var cfg = NightHuntDebugConfig.Instance;
            if (cfg == null || cfg.EnableInventoryDebugLogs)
                Debug.Log($"[LOOT_FLOW] SpawnManager {message}");
        }

#if UNITY_EDITOR
        // ── Editor — Context Menu: Auto-assign / Create Prefab References ────

        [ContextMenu("NightHunt/Auto-Assign World Prefabs")]
        private void Editor_AutoAssignWorldPrefabs()
        {
            bool changed = false;

            if (worldItemPrefab == null)
            {
                const string itemPath = "Assets/_Night_Hunt/Prefabs/LootItem/Prefab_WorldItem.prefab";
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(itemPath);
                if (found != null) { worldItemPrefab = found; changed = true; Debug.Log($"[WorldSpawnManager] Auto-assigned worldItemPrefab from {itemPath}"); }
                else Debug.LogWarning($"[WorldSpawnManager] worldItemPrefab not found at {itemPath} — create it first.");
            }

            if (worldContainerPrefab == null)
            {
                const string containerPath = "Assets/_Night_Hunt/Prefabs/LootItem/Prefab_WorldContainer.prefab";
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(containerPath);
                if (found != null) { worldContainerPrefab = found; changed = true; Debug.Log($"[WorldSpawnManager] Auto-assigned worldContainerPrefab from {containerPath}"); }
                else Debug.LogWarning($"[WorldSpawnManager] worldContainerPrefab not found at {containerPath} — create it first.");
            }

            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("NightHunt/Create WorldItem Template Prefab")]
        private void Editor_CreateWorldItemPrefab()
        {
            const string dir  = "Assets/_Night_Hunt/Prefabs/LootItem";
            const string path = dir + "/Prefab_WorldItem.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[WorldSpawnManager] Prefab_WorldItem already exists at {path}");
                return;
            }

            var go = new GameObject("Prefab_WorldItem");
            // WorldItem requires NetworkObject — add stub components for reference
            go.AddComponent<SphereCollider>().radius = 0.5f;
            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            worldItemPrefab = saved;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[WorldSpawnManager] Created Prefab_WorldItem template at {path}. " +
                      "Add NetworkObject + WorldItem components manually.");
        }

        [ContextMenu("NightHunt/Create WorldContainer Template Prefab")]
        private void Editor_CreateWorldContainerPrefab()
        {
            const string dir  = "Assets/_Night_Hunt/Prefabs/LootItem";
            const string path = dir + "/Prefab_WorldContainer.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[WorldSpawnManager] Prefab_WorldContainer already exists at {path}");
                return;
            }

            var go = new GameObject("Prefab_WorldContainer");
            go.AddComponent<BoxCollider>();
            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            worldContainerPrefab = saved;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[WorldSpawnManager] Created Prefab_WorldContainer template at {path}. " +
                      "Add NetworkObject + WorldContainer components manually.");
        }
#endif
    }
}
