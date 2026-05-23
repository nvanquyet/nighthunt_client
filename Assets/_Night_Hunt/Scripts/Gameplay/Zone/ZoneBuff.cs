using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;

namespace NightHunt.Gameplay.Zone
{
    /// <summary>
    /// Trigger-zone that applies a stat modifier when a player enters and removes it on exit.
    ///
    /// Architecture:
    ///   • Client-friendly: reads IPlayerStatSystem from the player hierarchy via GetComponentInParent
    ///   • Server-authoritative: AddModifier/RemoveAllModifiersFromSource live on PlayerStatSystem server-side;
    ///     the trigger fires on whichever host runs the physics (server-side rigidbody in FishNet).
    ///
    /// Setup in scene:
    ///   • Add a Trigger Collider (IsTrigger = true) — SphereCollider recommended
    ///   • Assign StatType, ModType, Value in Inspector
    ///   • GameObject Layer → "Zone" (not "Player")
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class ZoneBuff : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Zone Identity")]
        [SerializeField] private string _zoneId = "zone_buff";

        [Header("Stat Effect")]
        [SerializeField] private PlayerStatType _statType = PlayerStatType.MovementSpeed;
        [SerializeField] private ModifierType   _modType  = ModifierType.Percentage;
        [SerializeField] private float          _value    = 0.20f;   // +20% by default

        [Header("Visual / Gizmo")]
        [SerializeField] private float _radius = 10f;

        // ── Unique source ID per instance (stable across OnEnable/Disable) ────────
        private string _sourceId;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _sourceId = $"zonebuff_{_zoneId}_{gameObject.GetInstanceID()}";
        }

        // ── Trigger callbacks ─────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            // ── Raw trigger log: fires on every peer that runs physics ──────────
            // If you see this ONLY on client (not server), triggers aren't reaching server-side physics.
            Debug.Log($"[ZoneBuff] {_zoneId}: OnTriggerEnter fired \u2014 collider='{other.name}' layer={LayerMask.LayerToName(other.gameObject.layer)} (T={Time.time:F2})");

            var statSystem = FindStatSystem(other);
            if (statSystem == null)
            {
                Debug.Log($"[ZoneBuff] {_zoneId}: \u26a0 No IPlayerStatSystem found on '{other.name}' \u2014 zone stat won't apply (check player hierarchy).");
                return;
            }

            // NOTE: Percentage modifier formula = baseSpeed * (1 + value/100).
            // value=5 → +5%, NOT +500%. For +5 m/s flat use ModifierType.Flat.
            Debug.Log($"[ZoneBuff] {_zoneId}: {other.name} ENTERED → applying {_statType} {_modType} {_value:+0.###;-0.###} src={_sourceId}");
            var modifier = BuildModifier();
            statSystem.AddModifier(_statType, modifier);
        }

        private void OnTriggerExit(Collider other)
        {
            var statSystem = FindStatSystem(other);
            if (statSystem != null)
                Debug.Log($"[ZoneBuff] {_zoneId}: {other.name} EXITED → removing modifiers src={_sourceId} (T={Time.time:F2})");
            statSystem?.RemoveAllModifiersFromSource(_sourceId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private IPlayerStatSystem FindStatSystem(Collider other)
            => other.GetComponentInParent<IPlayerStatSystem>()
            ?? other.GetComponent<IPlayerStatSystem>();

        private StatModifier BuildModifier()
        {
            return _modType switch
            {
                ModifierType.Flat       => StatModifier.CreateFlat(      _sourceId, _value, 0, $"Zone:{_zoneId}"),
                ModifierType.Percentage => StatModifier.CreatePercentage( _sourceId, _value, 0, $"Zone:{_zoneId}"),
                ModifierType.Override   => StatModifier.CreateOverride(   _sourceId, _value,    $"Zone:{_zoneId}"),
                _                       => StatModifier.CreateFlat(       _sourceId, _value, 0, $"Zone:{_zoneId}"),
            };
        }

        // ── Editor gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            Gizmos.DrawSphere(transform.position, _radius);
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, _radius);
            UnityEditor.Handles.color = new Color(0f, 1f, 0.5f, 1f);
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (_radius + 0.5f),
                $"{_zoneId}\n{_modType} {_statType} {_value:+0.##;-0.##;0}");
        }
#endif
    }
}
