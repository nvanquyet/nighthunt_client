using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Match;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// Spawns boss(es) when Phase 2 (Hunt) starts, based on
    /// Spawns boss(es) when Phase 2 (Hunt) starts.
    /// Prefab-driven architecture heavily utilizes the World Container SpawnTable natively.
    /// Map designers tag boss spawn points with the configured SpawnPointTag (default "BossSpawn").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossSpawnManager : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchPhaseManager _phaseManager;

        [Header("Boss Spawns Configuration")]
        [Tooltip("Config data map for boss spawns. Map designers provide prefab and spawn points.")]
        [SerializeField] private List<BossSpawnEntry> _bossSpawns = new List<BossSpawnEntry>();

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly List<BossController> _spawnedBosses = new();

        // ──────────────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            if (_phaseManager == null)
            {
                _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
                if (_phaseManager == null)
                    Debug.LogWarning("[BossSpawnManager] MatchPhaseManager not found — assign it in the Inspector.");
            }
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
            SpawnBossesForPhase(phase);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Spawn

        [Server]
        private void SpawnBossesForPhase(MatchPhaseState currentPhase)
        {
            if (_bossSpawns == null || _bossSpawns.Count == 0) return;

            foreach (var cfg in _bossSpawns)
            {
                if (cfg.SpawnPhase == currentPhase)
                {
                    SpawnBoss(cfg);
                }
            }
        }

        [Server]
        private void SpawnBoss(BossSpawnEntry entry)
        {
            if (entry.BossPrefab == null)
            {
                Debug.LogError("[BossSpawnManager] Spawn entry is missing BossPrefab!");
                return;
            }

            // Random pick 1 spawn point from the assigned list
            if (entry.SpawnPoints == null || entry.SpawnPoints.Count == 0)
            {
                Debug.LogWarning($"[BossSpawnManager] Spawn entry for '{entry.BossPrefab.name}' has no SpawnPoints assigned!");
                return;
            }

            BossSpawnPoint spawnPoint = entry.SpawnPoints[Random.Range(0, entry.SpawnPoints.Count)];
            if (spawnPoint == null)
            {
                Debug.LogError($"[BossSpawnManager] A SpawnPoint for '{entry.BossPrefab.name}' is null/missing!");
                return;
            }

            Vector3 pos  = spawnPoint.Position;
            Quaternion rot = spawnPoint.Rotation;

            // Instantiate + network-spawn
            var go   = Instantiate(entry.BossPrefab, pos, rot);
            var boss = ComponentResolver.Find<BossController>(go)
                                        .OnSelf()
                                        .InChildren()
                                        .OrLogWarning("[Auto] BossController not found")
                                        .Resolve();
            if (boss == null)
            {
                Debug.LogError($"[BossSpawnManager] Prefab for '{entry.BossPrefab.name}' missing BossController!");
                Destroy(go);
                return;
            }

            // Inject the reward config from the map's spawn point into the Boss
            if (spawnPoint.RewardConfig != null)
            {
                boss.SetDynamicRewardConfig(spawnPoint.RewardConfig);
            }

            InstanceFinder.ServerManager.Spawn(go);
            boss.Died += OnBossDied;

            _spawnedBosses.Add(boss);

            Debug.Log($"[BossSpawnManager] Spawned boss '{boss.BossId}' at {pos}.");

            // Notify all clients so UI (BossHUDPanel) can show the HP bar.
            RpcNotifyBossSpawned(boss.BossId, pos);
        }

        [ObserversRpc]
        private void RpcNotifyBossSpawned(string bossId, Vector3 position)
        {
            GameplayEventBus.Instance?.Publish(new BossSpawnedEvent
            {
                BossId   = bossId,
                Position = position,
            });
        }

        private void OnBossDied(BossController boss)
        {
            _spawnedBosses.Remove(boss);
            Debug.Log($"[BossSpawnManager] Boss '{boss.BossId}' removed from tracking. Remaining: {_spawnedBosses.Count}");
        }

        #endregion
    }

    [System.Serializable]
    public struct BossSpawnEntry
    {
        [Tooltip("Boss prefab containing a BossController component.")]
        public GameObject BossPrefab;
        [Tooltip("List of spawn points from which one will be selected at random.")]
        public List<BossSpawnPoint> SpawnPoints;
        [Tooltip("Phase in which this boss spawns (typically Phase 2 - Hunt).")]
        public MatchPhaseState SpawnPhase;
    }
}
