using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Systems;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Scoring;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Server-authoritative damage receiver for player characters.
    ///
    /// Flow (hitscan):
    ///   WeaponSystem server resolver → ApplyDamageServer
    ///   → PlayerStatSystem.SetCurrentStat(Health) → CharacterLifecycleController.OnDied
    ///   → NotifyHitObserversRpc (all clients: VFX) → NotifyKillObserversRpc (kill feed)
    ///
    /// Flow (projectile):
    ///   Server physics OnTriggerEnter → ApplyDamageServer (no RPC needed, already on server)
    ///   → same chain as above
    ///
    /// Inspector setup:
    ///   Attach to the root player prefab alongside PlayerStatSystem and NetworkPlayer.
    ///   All child hitboxes should have PlayerHitboxMarker referencing this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthSystem : NetworkBehaviour, IHittable, IHealthSource
    {
        [Header("References")]
        [Tooltip("PlayerStatSystem on this player. Auto-resolved if null.")]
        [SerializeField] private PlayerStatSystem _statSystemSource;

        [Tooltip("CharacterLifecycleController for death/respawn events. Auto-resolved if null.")]
        [SerializeField] private CharacterLifecycleController _lifecycle;

        [Tooltip("MatchEndManager to track kills for win condition. Auto-resolved if null.")]
        [SerializeField] private NightHunt.Gameplay.Match.MatchEndManager _matchEndManager;
        [SerializeField] private ScoringSystem _scoringSystem;

        [Header("Settings")]
        [Tooltip("Armor damage reduction formula: final = damage * (100 / (100 + armor)).")]
        [SerializeField] private bool _applyArmorReduction = true;

        [Header("Anti-Cheat")]
        [Tooltip("Legacy fallback only. Player damage should normally come from server-authoritative weapon/projectile systems.")]
        [SerializeField] private bool _allowClientDamageRequests = false;

        [Tooltip("Maximum plausible distance (m) between shooter and target. Hits beyond this are rejected. 0 = disabled.")]
        [SerializeField] private float _maxHitDistance = 500f;

        [Tooltip("Maximum raw damage accepted from the legacy client damage RPC when it is explicitly enabled.")]
        [SerializeField] private float _maxClientRequestedDamage = 150f;

        [Tooltip("When false, same-team player damage is rejected on the server.")]
        [SerializeField] private bool _allowFriendlyFire = false;

        // ── Instance events (fire on this player's instance on all clients) ─────
        /// <summary>Fired on every client when a hit is confirmed. Use for blood / hit marker VFX.</summary>
        public event Action<DamageInfo> OnHitReceived;

        /// <summary>Fired on every client when health reaches 0. killerName may be empty for world damage.</summary>
        public event Action<string> OnPlayerDied;

        public float CurrentHealth => _statSystem != null ? _statSystem.GetStat(PlayerStatType.Health) : 0f;
        public float MaxHealth => Mathf.Max(1f, _statSystem != null ? _statSystem.GetStat(PlayerStatType.MaxHealth) : 100f);
        public bool IsDead => _statSystem != null && CurrentHealth <= 0f;
        public event Action<HealthChangeEvent> HealthChanged;

        // ── Static events (fire for ANY player across all clients) ────────────
        /// <summary>Raised on all clients whenever any player takes a hit. Use for shooter-side damage numbers.</summary>
        public static event Action<DamageInfo> OnAnyHitReceived;

        /// <summary>Raised on all clients whenever any player is killed. (victimName, killerName, weaponId)</summary>
        public static event Action<string, string, string> OnAnyPlayerDied;

        private IPlayerStatSystem _statSystem;
        private NetworkPlayer _networkPlayer;

        // ── FishNet Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkPlayer not found")
        .Resolve();

            ResolveReferences();

            if (_lifecycle == null)
                _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CharacterLifecycleController not found")
        .Resolve();

            if (_matchEndManager == null)
                _matchEndManager = FindFirstObjectByType<NightHunt.Gameplay.Match.MatchEndManager>();

            if (_scoringSystem == null)
                _scoringSystem = FindFirstObjectByType<ScoringSystem>();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveReferences();
        }
#endif

        private void ResolveReferences()
        {
            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
        .UseExisting(_statSystemSource)
        .OnSelf()
        .InChildren()
        .InParent()
        .InRootChildren()
        .OrLogWarning("[Auto] IPlayerStatSystem not found")
        .Resolve();

            if (_statSystem is PlayerStatSystem statConcrete)
                _statSystemSource = statConcrete;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            ResolveReferences();

            if (_statSystem != null)
                _statSystem.OnStatChanged += HandleStatChangedForHealthSource;
        }

        public override void OnStopNetwork()
        {
            if (_statSystem != null)
                _statSystem.OnStatChanged -= HandleStatChangedForHealthSource;

            base.OnStopNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_statSystem == null)
                Debug.LogError("[PlayerHealthSystem] IPlayerStatSystem not found. Damage will not apply.");

            if (_lifecycle == null)
                Debug.LogError("[PlayerHealthSystem] CharacterLifecycleController not found.");

            if (_scoringSystem == null)
                _scoringSystem = FindFirstObjectByType<ScoringSystem>();

        }

        private void HandleStatChangedForHealthSource(PlayerStatType type, float oldValue, float newValue)
        {
            if (type == PlayerStatType.Health)
            {
                HealthChanged?.Invoke(new HealthChangeEvent(oldValue, newValue, MaxHealth));
                return;
            }

            if (type == PlayerStatType.MaxHealth)
                HealthChanged?.Invoke(new HealthChangeEvent(CurrentHealth, CurrentHealth, MaxHealth));
        }
        // ── IHittable (owner-client call gateway) ─────────────────────────────

        /// <summary>
        /// Called by the shooter's owner client after a local hit confirmation.
        /// Routes to server for authoritative damage application.
        /// </summary>
        public void RequestDamage(DamageInfo info)
        {
            if (IsServerStarted)
            {
                ApplyDamageServer(info);
                return;
            }

            RequestDamageServerRpc(info);
        }

        // ── Server RPC (any client may call — RequireOwnership = false) ───────

        /// <summary>
        /// Receives damage request from ANY client.
        /// Server validates and applies if the hit is legitimate.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(DamageInfo info, NetworkConnection conn = null)
        {
            if (!ValidateHit(info, conn))
                return;

            float finalDamage = ComputeFinalDamage(info);
            ApplyDamageServer(finalDamage, info);
        }

        /// <summary>
        /// Direct server-side damage entry point used by server-authoritative systems
        /// (projectile physics, zone damage, etc.) that are already running on the server.
        /// </summary>
        [Server]
        public void ApplyDamageServer(DamageInfo info)
        {
            // Guard: dead players cannot receive further damage from any source.
            // (RequestDamageServerRpc already rejects via ValidateHit; this covers the direct server path.)
            if (_statSystem != null && _statSystem.GetStat(PlayerStatType.Health) <= 0f)
                return;

            float finalDamage = ComputeFinalDamage(info);
            ApplyDamageServer(finalDamage, info);
        }

        // ── Private server logic ──────────────────────────────────────────────

        [Server]
        private bool ValidateHit(DamageInfo info, NetworkConnection sender = null)
        {
            if (_statSystem == null)
            {
                Debug.LogWarning("[PlayerHealthSystem] ValidateHit: _statSystem is null.");
                return false;
            }

            // Reject damage against an already-dead player.
            if (_statSystem.GetStat(PlayerStatType.Health) <= 0f)
            {
                Debug.Log($"[PlayerHealthSystem] ValidateHit rejected: {_networkPlayer?.DisplayName} is already dead.");
                return false;
            }

            if (sender != null)
            {
                if (!_allowClientDamageRequests)
                {
                    Debug.LogWarning($"[PlayerHealthSystem] Client damage RPC rejected: legacy client damage is disabled. sender={sender.ClientId} weapon={info.WeaponId}");
                    return false;
                }

                if (info.ShooterNetworkObjectId <= 0)
                {
                    Debug.LogWarning($"[PlayerHealthSystem] Client damage RPC rejected: missing shooter object id. sender={sender.ClientId}");
                    return false;
                }

                if (!FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(info.ShooterNetworkObjectId, out var shooterNob))
                {
                    Debug.LogWarning($"[PlayerHealthSystem] Client damage RPC rejected: shooter object {info.ShooterNetworkObjectId} not spawned. sender={sender.ClientId}");
                    return false;
                }

                if (shooterNob.Owner != sender)
                {
                    Debug.LogWarning($"[PlayerHealthSystem] Client damage RPC rejected: sender={sender.ClientId} does not own shooter={info.ShooterNetworkObjectId}.");
                    return false;
                }

                if (info.Damage <= 0f || info.Damage > _maxClientRequestedDamage)
                {
                    Debug.LogWarning($"[PlayerHealthSystem] Client damage RPC rejected: damage={info.Damage:F1} outside allowed range.");
                    return false;
                }
            }

            NetworkPlayer shooterPlayer = ResolvePlayerByNetworkObjectId(info.ShooterNetworkObjectId);
            if (shooterPlayer != null && !shooterPlayer.IsAlive)
            {
                Debug.LogWarning($"[PlayerHealthSystem] ValidateHit rejected: shooter '{shooterPlayer.DisplayName}' is dead.");
                return false;
            }

            if (!_allowFriendlyFire && shooterPlayer != null && _networkPlayer != null &&
                shooterPlayer.ObjectId != _networkPlayer.ObjectId &&
                shooterPlayer.TeamId >= 0 && _networkPlayer.TeamId >= 0 &&
                shooterPlayer.TeamId == _networkPlayer.TeamId)
            {
                Debug.LogWarning($"[PlayerHealthSystem] ValidateHit rejected: friendly fire {shooterPlayer.DisplayName} -> {_networkPlayer.DisplayName}.");
                return false;
            }

            // Distance plausibility check: reject shots from impossibly far away.
            if (_maxHitDistance > 0f && info.ShooterNetworkObjectId > 0)
            {
                if (FishNet.InstanceFinder.ServerManager.Objects.Spawned
                        .TryGetValue(info.ShooterNetworkObjectId, out var shooterNob))
                {
                    float dist = Vector3.Distance(transform.position, shooterNob.transform.position);
                    if (dist > _maxHitDistance)
                    {
                        Debug.LogWarning(
                            $"[PlayerHealthSystem] ValidateHit REJECTED: distance {dist:F1} m > max {_maxHitDistance} m" +
                            $" (weapon: {info.WeaponId})");
                        return false;
                    }
                }
            }

            return true;
        }

        [Server]
        private float ComputeFinalDamage(DamageInfo info)
        {
            // DamageInfo.Damage already includes headshot multiplier (applied by WeaponBase on the client).
            // Server only applies armor reduction on top of the pre-computed value.
            float damage = info.Damage;

            if (info.IsHeadshot)
                Debug.Log($"[PlayerHealthSystem] HEADSHOT on {_networkPlayer?.DisplayName} — damage: {damage:F1}");

            if (_applyArmorReduction)
            {
                float armor = _statSystem.GetStat(PlayerStatType.Armor);
                if (armor > 0f)
                {
                    // Standard armor formula: effective = raw * (100 / (100 + armor))
                    damage *= 100f / (100f + armor);
                    Debug.Log($"[PlayerHealthSystem] Armor reduction (armor={armor:F1}) → effective damage: {damage:F1}");
                }
            }

            return damage;
        }

        [Server]
        private void ApplyDamageServer(float damage, DamageInfo info)
        {
            float current = _statSystem.GetStat(PlayerStatType.Health);
            float newHealth = Mathf.Max(0f, current - damage);

            Debug.Log($"[PlayerHealthSystem] {_networkPlayer?.DisplayName} hit — " +
                      $"damage: {damage:F1}, HP: {current:F1} → {newHealth:F1}" +
                      (info.IsHeadshot ? " [HEADSHOT]" : "") +
                      $" (weapon: {info.WeaponId})");

            // Propagate killer info and broadcast kill event BEFORE health stat change so that
            // CharacterLifecycleController.OnDied fires after LastKillerName is already set.
            if (newHealth <= 0f)
                HandleKillServer(info);

            _statSystem.SetCurrentStat(PlayerStatType.Health, newHealth);

            // Broadcast hit VFX to all clients (blood, hit marker).
            NotifyHitObserversRpc(info);
        }

        [Server]
        private void HandleKillServer(DamageInfo info)
        {
            string killerName = ResolveKillerName(info.ShooterNetworkObjectId);

            Debug.Log($"[PlayerHealthSystem] PLAYER KILLED — victim: {_networkPlayer?.DisplayName}" +
                      $", killer: {killerName}, weapon: {info.WeaponId}");

            // Tell CharacterLifecycleController who the killer is BEFORE health event fires.
            _lifecycle?.SetKillerInfo(killerName);

            uint killerObjId = info.ShooterNetworkObjectId > 0 ? (uint)info.ShooterNetworkObjectId : 0u;
            uint victimObjId = _networkPlayer != null ? (uint)_networkPlayer.ObjectId : 0u;
            int  killerTeamId = ResolveKillerTeamId(info.ShooterNetworkObjectId);
            string killerBackendPlayerId = ResolveBackendPlayerId(info.ShooterNetworkObjectId);

            // Track kill for match-end win condition
            if (killerTeamId >= 0)
                _matchEndManager?.AddKill(killerTeamId, killerBackendPlayerId);

            if (killerObjId != 0u)
                _scoringSystem?.AwardKill(killerObjId, victimObjId);

            // Broadcast death event with killer name to all clients (kill feed, death screen).
            NotifyKillObserversRpc(killerName, info.WeaponId, killerObjId, victimObjId, killerTeamId);
        }

        [Server]
        private string ResolveKillerName(int shooterNetObjId)
        {
            if (shooterNetObjId < 0)
                return string.Empty;

            NetworkPlayer shooter = ResolvePlayerByNetworkObjectId(shooterNetObjId);
            if (shooter != null)
                return shooter.DisplayName;

            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null)
                return string.Empty;

            foreach (var player in players)
            {
                if (player != null && (int)player.ObjectId == shooterNetObjId)
                    return player.DisplayName;
            }

            return string.Empty;
        }

        [Server]
        private int ResolveKillerTeamId(int shooterNetObjId)
        {
            if (shooterNetObjId < 0) return -1;

            NetworkPlayer shooter = ResolvePlayerByNetworkObjectId(shooterNetObjId);
            if (shooter != null)
                return shooter.TeamId;

            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null) return -1;

            foreach (var player in players)
            {
                if (player != null && (int)player.ObjectId == shooterNetObjId)
                    return player.TeamId;
            }

            return -1;
        }

        [Server]
        private string ResolveBackendPlayerId(int shooterNetObjId)
        {
            if (shooterNetObjId < 0)
                return string.Empty;

            NetworkPlayer shooter = ResolvePlayerByNetworkObjectId(shooterNetObjId);
            if (shooter != null)
            {
                var data = RegistryService.Instance?.GetPrivateDataByFishNetId(shooter.OwnerId);
                return data?.BackendPlayerId ?? string.Empty;
            }

            var players = PlayerPublicRegistry.Instance?.GetAllPlayers();
            if (players == null)
                return string.Empty;

            foreach (var player in players)
            {
                if (player != null && (int)player.ObjectId == shooterNetObjId)
                {
                    var data = RegistryService.Instance?.GetPrivateDataByFishNetId(player.OwnerId);
                    return data?.BackendPlayerId ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static NetworkPlayer ResolvePlayerByNetworkObjectId(int shooterNetObjId)
        {
            if (shooterNetObjId <= 0)
                return null;

            if (FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(shooterNetObjId, out var nob))
            {
                return nob.GetComponent<NetworkPlayer>()
                    ?? nob.GetComponentInChildren<NetworkPlayer>(true)
                    ?? nob.GetComponentInParent<NetworkPlayer>(true);
            }

            return null;
        }

        // ── Observer RPCs (server → all clients) ──────────────────────────────

        /// <summary>Runs on all clients: spawns blood / hit-marker VFX at the hit point.</summary>
        [ObserversRpc]
        private void NotifyHitObserversRpc(DamageInfo info)
        {
            Debug.Log($"[PlayerHealthSystem] Hit received on client — damage: {info.Damage:F1}" +
                      $" at {info.HitPoint}" + (info.IsHeadshot ? " [HEADSHOT]" : ""));

            PublishHitHealthReveal(info);
            OnHitReceived?.Invoke(info);
            OnAnyHitReceived?.Invoke(info);
        }

        private void PublishHitHealthReveal(DamageInfo info)
        {
            float current = CurrentHealth;
            float max = MaxHealth;
            float previous = Mathf.Min(max, current + Mathf.Max(1f, info.Damage));

            HealthChanged?.Invoke(new HealthChangeEvent(
                previous,
                current,
                max,
                info.ShooterNetworkObjectId,
                forceReveal: true));
        }

        /// <summary>Runs on all clients: triggers death screen, kill feed, etc.</summary>
        [ObserversRpc]
        private void NotifyKillObserversRpc(string killerName, string weaponId,
            uint killerNetObjId, uint victimNetObjId, int killerTeamId)
        {
            string victimName = _networkPlayer?.DisplayName ?? string.Empty;
            int victimTeam = _networkPlayer != null ? _networkPlayer.TeamId : -1;

            Debug.Log($"[PlayerHealthSystem] DEATH event on client — victim: {victimName}" +
                      $", killed by: {killerName}, weapon: {weaponId}");

            OnPlayerDied?.Invoke(killerName);
            OnAnyPlayerDied?.Invoke(victimName, killerName, weaponId);

            // GLOBAL EVENT FOR KILL FEED / MATCH LOGIC / SCORING:
            NightHunt.Gameplay.Core.Events.GameplayEventBus.Instance?.Publish(new NightHunt.Gameplay.Core.Events.PlayerKilledEvent
            {
                VictimName = victimName,
                KillerName = killerName,
                WeaponId = weaponId,
                VictimTeamId = victimTeam,
                KillerNetworkObjectId = killerNetObjId,
                VictimNetworkObjectId = victimNetObjId,
                KillerTeamId = killerTeamId,
            });
        }
    }
}
