using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;
using NightHunt.Gameplay.Core.State;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Server-authoritative damage receiver for player characters.
    ///
    /// Flow (hitscan):
    ///   Owner client raycast → RequestDamageServerRpc → ApplyDamageServer
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
    public sealed class PlayerHealthSystem : NetworkBehaviour, IHittable
    {
        [Header("References")]
        [Tooltip("PlayerStatSystem on this player. Auto-resolved if null.")]
        [SerializeField] private MonoBehaviour _statSystemSource;

        [Tooltip("CharacterLifecycleController for death/respawn events. Auto-resolved if null.")]
        [SerializeField] private CharacterLifecycleController _lifecycle;

        [Header("Settings")]
        [Tooltip("Armor damage reduction formula: final = damage * (100 / (100 + armor)).")]
        [SerializeField] private bool _applyArmorReduction = true;

        [Header("Anti-Cheat")]
        [Tooltip("Maximum plausible distance (m) between shooter and target. Hits beyond this are rejected. 0 = disabled.")]
        [SerializeField] private float _maxHitDistance = 500f;

        // ── Instance events (fire on this player's instance on all clients) ─────
        /// <summary>Fired on every client when a hit is confirmed. Use for blood / hit marker VFX.</summary>
        public event Action<DamageInfo> OnHitReceived;

        /// <summary>Fired on every client when health reaches 0. killerName may be empty for world damage.</summary>
        public event Action<string> OnPlayerDied;

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

            if (_statSystemSource != null)
                _statSystem = _statSystemSource as IPlayerStatSystem;

            if (_statSystem == null)
                _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] IPlayerStatSystem not found")
        .Resolve();

            if (_lifecycle == null)
                _lifecycle = ComponentResolver.Find<CharacterLifecycleController>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CharacterLifecycleController not found")
        .Resolve();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_statSystem == null)
                Debug.LogError("[PlayerHealthSystem] IPlayerStatSystem not found. Damage will not apply.");

            if (_lifecycle == null)
                Debug.LogError("[PlayerHealthSystem] CharacterLifecycleController not found.");
        }

        // ── IHittable (owner-client call gateway) ─────────────────────────────

        /// <summary>
        /// Called by the shooter's owner client after a local hit confirmation.
        /// Routes to server for authoritative damage application.
        /// </summary>
        public void RequestDamage(DamageInfo info)
        {
            RequestDamageServerRpc(info);
        }

        // ── Server RPC (any client may call — RequireOwnership = false) ───────

        /// <summary>
        /// Receives damage request from ANY client.
        /// Server validates and applies if the hit is legitimate.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(DamageInfo info)
        {
            if (!ValidateHit(info))
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
            float finalDamage = ComputeFinalDamage(info);
            ApplyDamageServer(finalDamage, info);
        }

        // ── Private server logic ──────────────────────────────────────────────

        [Server]
        private bool ValidateHit(DamageInfo info)
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

            // Broadcast death event with killer name to all clients (kill feed, death screen).
            NotifyKillObserversRpc(killerName, info.WeaponId);
        }

        [Server]
        private string ResolveKillerName(int shooterNetObjId)
        {
            if (shooterNetObjId < 0)
                return string.Empty;

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

        // ── Observer RPCs (server → all clients) ──────────────────────────────

        /// <summary>Runs on all clients: spawns blood / hit-marker VFX at the hit point.</summary>
        [ObserversRpc]
        private void NotifyHitObserversRpc(DamageInfo info)
        {
            Debug.Log($"[PlayerHealthSystem] Hit received on client — damage: {info.Damage:F1}" +
                      $" at {info.HitPoint}" + (info.IsHeadshot ? " [HEADSHOT]" : ""));

            OnHitReceived?.Invoke(info);
            OnAnyHitReceived?.Invoke(info);
        }

        /// <summary>Runs on all clients: triggers death screen, kill feed, etc.</summary>
        [ObserversRpc]
        private void NotifyKillObserversRpc(string killerName, string weaponId)
        {
            string victimName = _networkPlayer?.DisplayName ?? string.Empty;

            Debug.Log($"[PlayerHealthSystem] DEATH event on client — victim: {victimName}" +
                      $", killed by: {killerName}, weapon: {weaponId}");

            OnPlayerDied?.Invoke(killerName);
            OnAnyPlayerDied?.Invoke(victimName, killerName, weaponId);
        }
    }
}
