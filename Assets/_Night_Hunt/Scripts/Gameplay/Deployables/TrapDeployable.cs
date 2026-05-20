using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using NightHunt.Audio;
using NightHunt.Core;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Network deployable runtime for trap prefabs.
    /// Placement is handled elsewhere; this component owns armed mine and field effects.
    /// </summary>
    public sealed class TrapDeployable : BaseDeployable
    {
        [Header("Trap Runtime")]
        [SerializeField] private bool _enableTrapRuntime = true;
        [SerializeField, Min(0f)] private float _armingDelay = 0.75f;
        [SerializeField] private LayerMask _triggerLayers;
        [Tooltip("Off = affect enemies only. On = owner/team/any player can trigger or be damaged.")]
        [SerializeField] private bool _allowFriendlyFire = false;

        [Header("Mine")]
        [SerializeField, Min(0.1f)] private float _mineTriggerRadius = 2f;
        [SerializeField, Min(0f)] private float _mineExplosionRadius = 4f;
        [SerializeField, Min(0f)] private float _mineDamage = 120f;
        [SerializeField] private bool _detonateMineWhenDestroyed;

        [Header("Shock / Slow Field")]
        [SerializeField, Min(0.1f)] private float _fieldRadius = 4f;
        [SerializeField, Min(0.1f)] private float _fieldDuration = 8f;
        [SerializeField, Min(0.05f)] private float _fieldTickInterval = 0.25f;
        [SerializeField] private PlayerStatModifier[] _fieldModifiers =
        {
            new PlayerStatModifier
            {
                StatType = PlayerStatType.MovementSpeed,
                ModifierType = ModifierType.Percentage,
                Value = -40f,
                Description = "Shock field slow"
            }
        };
        [SerializeField] private float _fieldHealthDeltaPerSecond;
        [SerializeField] private float _fieldStaminaDeltaPerSecond = -8f;

        [Header("Feedback")]
        [SerializeField] private GameObject _trapTriggeredVfxPrefab;
        [SerializeField] private AudioClip _trapTriggeredSound;

        private static readonly Collider[] s_overlapBuffer = new Collider[96];

        private readonly Dictionary<PlayerHealthSystem, string> _fieldSources = new(16);
        private readonly HashSet<PlayerHealthSystem> _playersInField = new(16);
        private readonly HashSet<PlayerHealthSystem> _damagedPlayers = new(16);
        private readonly HashSet<IHittable> _damagedHittables = new(32);

        private DeployableKind _kind;
        private string _definitionId;
        private Coroutine _runtimeCoroutine;
        private bool _mineTriggered;

        [Server]
        public void Initialize(int teamId, int maxHP, DeployableKind kind, string definitionId, int ownerNetworkObjectId = 0)
        {
            _kind = kind;
            _definitionId = definitionId;
            base.Initialize(teamId, maxHP, ownerNetworkObjectId);
        }

        public override void OnStopNetwork()
        {
            if (IsServerStarted)
            {
                StopRuntime();
                RemoveAllFieldModifiers();
            }

            base.OnStopNetwork();
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            Debug.Log($"[TrapDeployable] Placed {_definitionId} kind={_kind} team={OwnerTeamId} hp={CurrentHP}");

            if (!_enableTrapRuntime)
                return;

            switch (_kind)
            {
                case DeployableKind.ExplosiveMine:
                    _runtimeCoroutine = StartCoroutine(MineLoop());
                    break;
                case DeployableKind.ShockField:
                    _runtimeCoroutine = StartCoroutine(FieldLoop());
                    break;
            }
        }

        [Server]
        protected override void OnDeployableDestroyed()
        {
            StopRuntime();

            if (_kind == DeployableKind.ExplosiveMine && _detonateMineWhenDestroyed && !_mineTriggered)
            {
                DetonateMine();
                return;
            }

            RemoveAllFieldModifiers();
            base.OnDeployableDestroyed();
        }

        [Server]
        private IEnumerator MineLoop()
        {
            if (_armingDelay > 0f)
                yield return new WaitForSeconds(_armingDelay);

            var wait = new WaitForSeconds(0.1f);
            while (IsActive && IsPlaced && !_mineTriggered)
            {
                if (FindAnyTriggerTarget(_mineTriggerRadius))
                {
                    DetonateMine();
                    yield break;
                }

                yield return wait;
            }
        }

        [Server]
        private void DetonateMine()
        {
            if (_mineTriggered)
                return;

            _mineTriggered = true;
            StopRuntime();

            if (_mineExplosionRadius > 0f && _mineDamage > 0f)
                DealAoeDamage(transform.position, _mineExplosionRadius, _mineDamage);

            RpcPlayTrapTriggered(transform.position);
            _isActive.Value = false;
            Invoke(nameof(DespawnDeployable), 1.25f);
        }

        [Server]
        private IEnumerator FieldLoop()
        {
            if (_armingDelay > 0f)
                yield return new WaitForSeconds(_armingDelay);

            RpcPlayTrapTriggered(transform.position);

            float endTime = Time.time + _fieldDuration;
            while (IsActive && IsPlaced && Time.time < endTime)
            {
                TickField(_fieldTickInterval);
                yield return new WaitForSeconds(_fieldTickInterval);
            }

            RemoveAllFieldModifiers();
            _isActive.Value = false;
            Invoke(nameof(DespawnDeployable), 0.25f);
        }

        [Server]
        private void TickField(float deltaTime)
        {
            _playersInField.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _fieldRadius,
                s_overlapBuffer,
                ResolveTriggerMask(),
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (!TryResolvePlayer(s_overlapBuffer[i], out var playerHealth))
                    continue;

                if (!ShouldAffectPlayer(playerHealth))
                    continue;

                _playersInField.Add(playerHealth);
                ApplyFieldModifiers(playerHealth);
                ApplyFieldTick(playerHealth, deltaTime);
            }

            RemoveModifiersForExitedPlayers();
        }

        [Server]
        private bool FindAnyTriggerTarget(float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                s_overlapBuffer,
                ResolveTriggerMask(),
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (!TryResolvePlayer(s_overlapBuffer[i], out var playerHealth))
                    continue;

                if (ShouldAffectPlayer(playerHealth))
                    return true;
            }

            return false;
        }

        [Server]
        private void DealAoeDamage(Vector3 origin, float radius, float damage)
        {
            _damagedPlayers.Clear();
            _damagedHittables.Clear();

            int count = Physics.OverlapSphereNonAlloc(origin, radius, s_overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = s_overlapBuffer[i];
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                Vector3 hitPoint = hit.ClosestPoint(origin);
                float distance = Vector3.Distance(origin, hitPoint);
                float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, radius));
                float finalDamage = damage * Mathf.Max(0.15f, falloff);

                var info = new DamageInfo
                {
                    Damage = finalDamage,
                    IsHeadshot = false,
                    HitPoint = hitPoint,
                    HitNormal = (hitPoint - origin).sqrMagnitude > 0.001f ? (hitPoint - origin).normalized : Vector3.up,
                    ShooterNetworkObjectId = ResolveDamageSourceNetworkObjectId(),
                    WeaponId = string.IsNullOrEmpty(_definitionId) ? _kind.ToString() : _definitionId,
                };

                if (TryResolvePlayer(hit, out var playerHealth))
                {
                    if (!_damagedPlayers.Add(playerHealth) || !ShouldAffectPlayer(playerHealth))
                        continue;

                    if (!playerHealth.IsDead)
                        playerHealth.ApplyDamageServer(info);
                    continue;
                }

                var hittable = hit.GetComponentInParent<IHittable>();
                if (hittable == null
                    || ReferenceEquals(hittable, this)
                    || !ShouldAffectHittable(hittable)
                    || !_damagedHittables.Add(hittable))
                    continue;

                if (hittable is BaseDeployable deployable)
                    deployable.TakeDamage(info);
                else
                    hittable.RequestDamage(info);
            }
        }

        [Server]
        private bool ShouldAffectPlayer(PlayerHealthSystem playerHealth)
        {
            if (playerHealth == null || playerHealth.IsDead)
                return false;

            if (_allowFriendlyFire)
                return true;

            var targetTeam = playerHealth.GetComponentInParent<NightHunt.Networking.Player.NetworkPlayer>()?.TeamId ?? -1;
            return targetTeam < 0 || OwnerTeamId < 0 || targetTeam != OwnerTeamId;
        }

        [Server]
        private bool ShouldAffectHittable(IHittable hittable)
        {
            if (hittable == null)
                return false;

            if (_allowFriendlyFire)
                return true;

            if (hittable is BaseDeployable deployable)
            {
                int targetTeam = deployable.OwnerTeamId;
                return targetTeam < 0 || OwnerTeamId < 0 || targetTeam != OwnerTeamId;
            }

            return true;
        }

        [Server]
        private void ApplyFieldModifiers(PlayerHealthSystem playerHealth)
        {
            if (_fieldSources.ContainsKey(playerHealth) || _fieldModifiers == null || _fieldModifiers.Length == 0)
                return;

            var stats = ResolveStats(playerHealth);
            if (stats == null)
                return;

            string sourceId = $"trap:{ObjectId}:{_definitionId}:{playerHealth.ObjectId}";
            for (int i = 0; i < _fieldModifiers.Length; i++)
            {
                var mod = _fieldModifiers[i];
                stats.AddModifier(mod.StatType, CreateRuntimeModifier(sourceId, mod));
            }

            _fieldSources[playerHealth] = sourceId;
        }

        [Server]
        private void ApplyFieldTick(PlayerHealthSystem playerHealth, float deltaTime)
        {
            var stats = ResolveStats(playerHealth);
            if (stats == null)
                return;

            if (!Mathf.Approximately(_fieldHealthDeltaPerSecond, 0f))
            {
                float delta = _fieldHealthDeltaPerSecond * deltaTime;
                if (delta < 0f)
                {
                    var info = new DamageInfo
                    {
                        Damage = -delta,
                        IsHeadshot = false,
                        HitPoint = playerHealth.transform.position,
                        HitNormal = Vector3.up,
                        ShooterNetworkObjectId = ResolveDamageSourceNetworkObjectId(),
                        WeaponId = string.IsNullOrEmpty(_definitionId) ? _kind.ToString() : _definitionId,
                    };
                    playerHealth.ApplyDamageServer(info);
                }
                else
                {
                    float health = stats.GetStat(PlayerStatType.Health);
                    float maxHealth = Mathf.Max(1f, stats.GetStat(PlayerStatType.MaxHealth));
                    stats.SetCurrentStat(PlayerStatType.Health, Mathf.Min(maxHealth, health + delta));
                }
            }

            if (!Mathf.Approximately(_fieldStaminaDeltaPerSecond, 0f))
            {
                float stamina = stats.GetStat(PlayerStatType.Stamina);
                float maxStamina = Mathf.Max(1f, stats.GetStat(PlayerStatType.MaxStamina));
                stats.SetCurrentStat(
                    PlayerStatType.Stamina,
                    Mathf.Clamp(stamina + _fieldStaminaDeltaPerSecond * deltaTime, 0f, maxStamina));
            }
        }

        [Server]
        private void RemoveModifiersForExitedPlayers()
        {
            s_removeList.Clear();
            foreach (var kv in _fieldSources)
            {
                if (!_playersInField.Contains(kv.Key))
                    s_removeList.Add(kv.Key);
            }

            for (int i = 0; i < s_removeList.Count; i++)
                RemoveFieldModifiers(s_removeList[i]);
        }

        private static readonly List<PlayerHealthSystem> s_removeList = new(16);

        [Server]
        private void RemoveAllFieldModifiers()
        {
            s_removeList.Clear();
            foreach (var kv in _fieldSources)
                s_removeList.Add(kv.Key);

            for (int i = 0; i < s_removeList.Count; i++)
                RemoveFieldModifiers(s_removeList[i]);
        }

        [Server]
        private void RemoveFieldModifiers(PlayerHealthSystem playerHealth)
        {
            if (playerHealth == null || !_fieldSources.TryGetValue(playerHealth, out string sourceId))
                return;

            ResolveStats(playerHealth)?.RemoveAllModifiersFromSource(sourceId);
            _fieldSources.Remove(playerHealth);
        }

        private LayerMask ResolveTriggerMask()
        {
            if (_triggerLayers.value != 0)
                return _triggerLayers;

            return LayerMask.GetMask(NightHuntLayers.PlayerHitBox, NightHuntLayers.Player);
        }

        private int ResolveDamageSourceNetworkObjectId()
            => OwnerNetworkObjectId > 0 ? OwnerNetworkObjectId : (int)ObjectId;

        private static bool TryResolvePlayer(Collider hit, out PlayerHealthSystem playerHealth)
        {
            playerHealth = null;
            if (hit == null)
                return false;

            if (hit.TryGetComponent<PlayerHitboxMarker>(out var marker) && marker.HealthSystem != null)
            {
                playerHealth = marker.HealthSystem;
                return true;
            }

            playerHealth = hit.GetComponentInParent<PlayerHealthSystem>();
            return playerHealth != null;
        }

        private static IPlayerStatSystem ResolveStats(PlayerHealthSystem playerHealth)
        {
            if (playerHealth == null)
                return null;

            return playerHealth.GetComponent<IPlayerStatSystem>()
                   ?? playerHealth.GetComponentInChildren<IPlayerStatSystem>(true)
                   ?? playerHealth.GetComponentInParent<IPlayerStatSystem>();
        }

        private static StatModifier CreateRuntimeModifier(string sourceId, PlayerStatModifier mod)
        {
            return mod.ModifierType switch
            {
                ModifierType.Percentage => StatModifier.CreatePercentage(sourceId, mod.Value, -100, mod.Description),
                ModifierType.Override => StatModifier.CreateOverride(sourceId, mod.Value, mod.Description),
                _ => StatModifier.CreateFlat(sourceId, mod.Value, -100, mod.Description)
            };
        }

        [Server]
        private void StopRuntime()
        {
            if (_runtimeCoroutine == null)
                return;

            StopCoroutine(_runtimeCoroutine);
            _runtimeCoroutine = null;
        }

        [ObserversRpc]
        private void RpcPlayTrapTriggered(Vector3 position)
        {
            if (_trapTriggeredVfxPrefab != null)
            {
                var vfx = Instantiate(_trapTriggeredVfxPrefab, position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            AudioClip clip = _trapTriggeredSound;
            if (clip == null && AudioManager.HasInstance)
                clip = AudioManager.Instance.Library?.explosionGrenade;

            if (clip == null)
                return;

            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayExplosion3D(clip, position);
            else
                AudioSource.PlayClipAtPoint(clip, position);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _mineTriggerRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.18f);
            Gizmos.DrawWireSphere(transform.position, _mineExplosionRadius);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _fieldRadius);
        }
#endif
    }
}
