using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Match;
using UnityEngine;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// Spawns boss(es) when Phase 2 (Hunt) starts, based on
    /// <see cref="BossSpawnConfigData"/> entries. TODO: wire in new data source.
    ///
    /// Map designers tag boss spawn points with the tag defined in each entry's
    /// <c>SpawnPointTag</c> field (default "BossSpawn").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossSpawnManager : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        [Header("Boss Prefabs")]
        [Tooltip("Map each BossId string to a prefab. Order must match config BossId values.")]
        [SerializeField] private List<BossPrefabEntry> _bossPrefabs = new();

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly List<BossController> _spawnedBosses = new();

        // ──────────────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            if (_phaseManager == null)
                _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _phaseManager.OnPhaseStarted += OnPhaseStarted;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_phaseManager != null)
                _phaseManager.OnPhaseStarted -= OnPhaseStarted;
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Phase handler

        private void OnPhaseStarted(MatchPhaseState phase, string phaseName)
        {
            if (phase == MatchPhaseState.Hunt)
                SpawnAllBosses();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Spawn

        [Server]
        private void SpawnAllBosses()
        {
            List<BossSpawnConfigData> configs = null; // TODO: load from new data source
            if (configs == null || configs.Count == 0)
            {
                Debug.LogWarning("[BossSpawnManager] No boss configs found.");
                return;
            }

            foreach (var cfg in configs)
            {
                SpawnBoss(cfg);
            }
        }

        [Server]
        private void SpawnBoss(BossSpawnConfigData cfg)
        {
            // Find a matching spawn point by tag
            string spawnTag = string.IsNullOrEmpty(cfg.SpawnPointTag) ? "BossSpawn" : cfg.SpawnPointTag;
            var spawnPoints = GameObject.FindGameObjectsWithTag(spawnTag);

            if (spawnPoints.Length == 0)
            {
                Debug.LogWarning($"[BossSpawnManager] No spawn point with tag '{spawnTag}' for boss '{cfg.BossId}'.");
                return;
            }

            // Pick a random spawn point
            var spawnGO = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector3 pos  = spawnGO.transform.position;
            Quaternion rot = spawnGO.transform.rotation;

            // Resolve prefab
            GameObject prefab = GetPrefabForBossId(cfg.BossId);
            if (prefab == null)
            {
                Debug.LogError($"[BossSpawnManager] No prefab registered for boss '{cfg.BossId}'.");
                return;
            }

            // Instantiate + network-spawn
            var go   = Instantiate(prefab, pos, rot);
            var boss = go.GetComponent<BossController>();
            if (boss == null)
            {
                Debug.LogError($"[BossSpawnManager] Prefab for '{cfg.BossId}' missing BossController!");
                Destroy(go);
                return;
            }

            InstanceFinder.ServerManager.Spawn(go);
            boss.Initialize(cfg);
            boss.Died += OnBossDied;

            _spawnedBosses.Add(boss);

            Debug.Log($"[BossSpawnManager] Spawned boss '{cfg.BossId}' at {pos}.");

            GameplayEventBus.Instance?.Publish(new BossSpawnedEvent { BossId = cfg.BossId });
        }

        private void OnBossDied(BossController boss)
        {
            _spawnedBosses.Remove(boss);
            Debug.Log($"[BossSpawnManager] Boss '{boss.BossId}' removed from tracking. Remaining: {_spawnedBosses.Count}");
        }

        private GameObject GetPrefabForBossId(string bossId)
        {
            foreach (var entry in _bossPrefabs)
                if (entry.BossId == bossId)
                    return entry.Prefab;
            return _bossPrefabs.Count > 0 ? _bossPrefabs[0].Prefab : null;
        }

        #endregion
    }

    [System.Serializable]
    public struct BossPrefabEntry
    {
        public string     BossId;
        public GameObject Prefab;
    }
}
