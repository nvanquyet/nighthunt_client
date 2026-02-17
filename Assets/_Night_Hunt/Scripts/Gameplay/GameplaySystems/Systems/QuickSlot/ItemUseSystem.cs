using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using GameplaySystems.Core.Data;
using GameplaySystems.Inventory;
using GameplaySystems.Stat;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Handles using QuickSlot items (consumables and throwables).
    ///
    /// ── Consumable flow ────────────────────────────────────────────────────────
    ///   UseItem()  →  HolsterWeapon  →  countdown timer  →  ApplyEffects
    ///             →  ConsumeItem  →  OnItemUseCompleted  →  RestoreWeapon
    ///
    /// ── Throwable flow ─────────────────────────────────────────────────────────
    ///   UseItem()  →  HolsterWeapon  →  [wait for Fire input via ExecuteThrow()]
    ///             →  SpawnProjectile  →  ConsumeItem  →  OnItemUseCompleted  →  RestoreWeapon
    ///
    /// ── Cancel ─────────────────────────────────────────────────────────────────
    ///   CancelUse()  →  item NOT consumed  →  OnItemUseCancelled  →  RestoreWeapon
    ///
    /// Attach to the same GameObject as WeaponSystem, PlayerStatSystem, InventorySystem.
    /// QuickSlotSystem auto-resolves a reference to this component.
    /// </summary>
    public class ItemUseSystem : NetworkBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("References (auto-resolved from same GameObject)")]
        [SerializeField] private WeaponSystem     _weaponSystem;
        [SerializeField] private PlayerStatSystem _statSystem;
        [SerializeField] private InventorySystem  _inventorySystem;

        [Header("Settings")]
        [Tooltip("Fallback use-duration when ConsumableDefinition.UsageDuration == 0.")]
        [SerializeField] private float _defaultUseTime = 3.5f;

        // ── Runtime state ──────────────────────────────────────────────────────
        private bool            _isUsingItem;
        private ItemInstance    _currentItem;
        private WeaponSlotType? _previousWeaponSlot;
        private Coroutine       _useCoroutine;

        // ── Public read-only ───────────────────────────────────────────────────
        public bool         IsUsingItem => _isUsingItem;
        public ItemInstance CurrentItem => _currentItem;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired the moment item-use begins (weapon already holstered).</summary>
        public event Action<ItemInstance> OnItemUseStarted;

        /// <summary>Fired when item-use completes successfully (effects applied, weapon restored).</summary>
        public event Action<ItemInstance> OnItemUseCompleted;

        /// <summary>Fired when item-use is cancelled (item NOT consumed, weapon restored).</summary>
        public event Action<ItemInstance> OnItemUseCancelled;

        /// <summary>0→1 progress tick during consumable countdown. Not fired for throwables.</summary>
        public event Action<ItemInstance, float> OnItemUseProgress;

        // ──────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (!_weaponSystem)    _weaponSystem    = GetComponent<WeaponSystem>();
            if (!_statSystem)      _statSystem      = GetComponent<PlayerStatSystem>();
            if (!_inventorySystem) _inventorySystem = GetComponent<InventorySystem>();
        }

        // ══════════════════════════════════════════════════════════════════════
        #region Public API

        /// <summary>
        /// Main entry point – route item to consumable or throwable handler.
        /// Called by QuickSlotSystem (server-side).
        /// </summary>
        [Server]
        public bool UseItem(ItemInstance item)
        {
            if (item == null)  { Debug.LogWarning("[ItemUseSystem] UseItem: item is null.");           return false; }
            if (_isUsingItem)  { Debug.LogWarning("[ItemUseSystem] UseItem: already using an item.");  return false; }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)   { Debug.LogError($"[ItemUseSystem] No definition for: {item.DefinitionID}"); return false; }

            if (def is ConsumableDefinition cd) return BeginConsumable(item, cd);
            if (def is ThrowableDefinition  td) return BeginThrowable(item, td);

            Debug.LogWarning($"[ItemUseSystem] Item type not handled by ItemUseSystem: {def.GetType().Name}");
            return false;
        }

        /// <summary>
        /// Call when the player presses the Fire button while in throw-mode.
        /// Must be server-side.
        /// </summary>
        [Server]
        public void ExecuteThrow()
        {
            if (!_isUsingItem || _currentItem == null)
            {
                Debug.LogWarning("[ItemUseSystem] ExecuteThrow: no active throwable.");
                return;
            }

            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID) as ThrowableDefinition;
            if (def == null) { Debug.LogError("[ItemUseSystem] ExecuteThrow: current item is not throwable."); return; }

            SpawnProjectile(def);
            ConsumeItem(_currentItem);
            CompleteUse(_currentItem);
        }

        /// <summary>
        /// Cancel an in-progress use. Item is NOT consumed.
        /// Fires OnItemUseCancelled and restores weapon.
        /// </summary>
        [Server]
        public void CancelUse()
        {
            if (!_isUsingItem) return;

            // Check if this consumable allows cancellation
            var def = ItemDatabase.GetDefinition(_currentItem?.DefinitionID);
            if (def is ConsumableDefinition cd && !cd.CanCancelUsage)
            {
                Debug.Log("[ItemUseSystem] CancelUse: this item cannot be cancelled.");
                return;
            }

            if (_useCoroutine != null) { StopCoroutine(_useCoroutine); _useCoroutine = null; }

            var item     = _currentItem;
            _isUsingItem = false;
            _currentItem = null;

            OnItemUseCancelled?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Cancelled use of '{item?.DefinitionID}'.");
            RestoreWeapon();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Consumable

        private bool BeginConsumable(ItemInstance item, ConsumableDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;
            float dur    = def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime;
            _useCoroutine = StartCoroutine(ConsumableRoutine(item, def, dur));
            OnItemUseStarted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Consumable started: '{def.DisplayName}' ({dur} s).");
            return true;
        }

        private IEnumerator ConsumableRoutine(ItemInstance item, ConsumableDefinition def, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                OnItemUseProgress?.Invoke(item, Mathf.Clamp01(elapsed / dur));
                yield return null;
            }
            ApplyAllEffects(def);
            ConsumeItem(item);
            CompleteUse(item);
        }

        private void ApplyAllEffects(ConsumableDefinition def)
        {
            if (def.Effects == null || def.Effects.Length == 0)
            {
                Debug.LogWarning($"[ItemUseSystem] '{def.DisplayName}' has no effects defined.");
                return;
            }
            foreach (var fx in def.Effects)
                ApplySingleEffect(fx);
        }

        private void ApplySingleEffect(ConsumableEffect fx)
        {
            switch (fx.EffectType)
            {
                // ── Instant stat restore ───────────────────────────────────────
                case ConsumableEffectType.RestoreHealth:
                case ConsumableEffectType.InstantHeal:
                    AdjustStat(PlayerStatType.Health, PlayerStatType.MaxHealth, fx.Value);
                    Debug.Log($"[ItemUseSystem] RestoreHealth +{fx.Value}");
                    break;

                case ConsumableEffectType.RestoreStamina:
                    AdjustStat(PlayerStatType.Stamina, PlayerStatType.MaxStamina, fx.Value);
                    Debug.Log($"[ItemUseSystem] RestoreStamina +{fx.Value}");
                    break;

                // ── Over-time ─────────────────────────────────────────────────
                case ConsumableEffectType.HealOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Health, PlayerStatType.MaxHealth, +fx.Value, fx.Duration));
                    Debug.Log($"[ItemUseSystem] HealOverTime +{fx.Value} over {fx.Duration} s");
                    break;

                case ConsumableEffectType.StaminaOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Stamina, PlayerStatType.MaxStamina, +fx.Value, fx.Duration));
                    Debug.Log($"[ItemUseSystem] StaminaOverTime +{fx.Value} over {fx.Duration} s");
                    break;

                case ConsumableEffectType.DamageOverTime:
                    StartCoroutine(TickStat(PlayerStatType.Health, PlayerStatType.MaxHealth, -fx.Value, fx.Duration));
                    Debug.Log($"[ItemUseSystem] DamageOverTime -{fx.Value} over {fx.Duration} s");
                    break;

                // ── Temporary stat modifiers ──────────────────────────────────
                case ConsumableEffectType.SpeedBoost:
                    AddTempMod(PlayerStatType.MovementSpeed, fx, percentage: true);
                    break;

                case ConsumableEffectType.ArmorBoost:
                    AddTempMod(PlayerStatType.Armor, fx, percentage: false);
                    break;

                case ConsumableEffectType.DamageBoost:
                    // Wire into combat system: fire event or add modifier to attack stat
                    Debug.Log($"[ItemUseSystem] DamageBoost +{fx.Value}% for {fx.Duration} s → hook into combat system.");
                    break;

                case ConsumableEffectType.IncreaseMaxHealth:
                    AddTempMod(PlayerStatType.MaxHealth, fx, percentage: false);
                    break;

                case ConsumableEffectType.IncreaseMaxStamina:
                    AddTempMod(PlayerStatType.MaxStamina, fx, percentage: false);
                    break;

                // ── Buff / debuff hooks ───────────────────────────────────────
                case ConsumableEffectType.ApplyBuff:
                    Debug.Log($"[ItemUseSystem] ApplyBuff '{fx.BuffID}' for {fx.Duration} s → hook into buff system.");
                    break;

                case ConsumableEffectType.ApplyDebuff:
                    Debug.Log($"[ItemUseSystem] ApplyDebuff '{fx.BuffID}' for {fx.Duration} s → hook into debuff system.");
                    break;

                case ConsumableEffectType.Cure:
                    Debug.Log("[ItemUseSystem] Cure → hook into buff/debuff system to remove all debuffs.");
                    break;

                case ConsumableEffectType.Revive:
                    Debug.Log("[ItemUseSystem] Revive → hook into respawn/downed system.");
                    break;

                default:
                    Debug.LogWarning($"[ItemUseSystem] Unhandled effect type: {fx.EffectType}");
                    break;
            }
        }

        // ── Stat helpers ───────────────────────────────────────────────────────

        /// <summary>Add delta to stat, clamped to [0, maxStat].</summary>
        private void AdjustStat(PlayerStatType stat, PlayerStatType maxStat, float delta)
        {
            if (_statSystem == null) return;
            float cur  = _statSystem.GetStat(stat);
            float max  = _statSystem.GetStat(maxStat);
            _statSystem.SetCurrentStat(stat, Mathf.Clamp(cur + delta, 0f, max));
        }

        /// <summary>Spread total delta over duration in 1-second ticks.</summary>
        private IEnumerator TickStat(PlayerStatType stat, PlayerStatType maxStat, float total, float dur)
        {
            int   ticks = Mathf.Max(1, Mathf.CeilToInt(dur));
            float step  = total / ticks;
            float tickInterval = dur / ticks;
            for (int i = 0; i < ticks; i++)
            {
                AdjustStat(stat, maxStat, step);
                yield return new WaitForSeconds(tickInterval);
            }
        }

        /// <summary>Add a named StatModifier and auto-remove it after Duration.</summary>
        private void AddTempMod(PlayerStatType stat, ConsumableEffect fx, bool percentage)
        {
            if (_statSystem == null) return;
            string src = $"consumable_{stat}_{Time.time:F0}";
            var mod = percentage
                ? StatModifier.CreatePercentage(src, fx.Value, 0, fx.Description)
                : StatModifier.CreateFlat(src, fx.Value, 0, fx.Description);
            _statSystem.AddModifier(stat, mod);
            Debug.Log($"[ItemUseSystem] Temp modifier on {stat}: {(percentage ? $"+{fx.Value}%" : $"+{fx.Value}")} for {fx.Duration} s.");
            if (fx.Duration > 0f)
                StartCoroutine(ExpireMod(stat, src, fx.Duration));
        }

        private IEnumerator ExpireMod(PlayerStatType stat, string src, float delay)
        {
            yield return new WaitForSeconds(delay);
            _statSystem?.RemoveModifier(stat, src);
            Debug.Log($"[ItemUseSystem] Modifier expired: {src}");
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Throwable

        private bool BeginThrowable(ItemInstance item, ThrowableDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;
            OnItemUseStarted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Throw mode: '{def.DisplayName}'. Press Fire to throw, press slot again to cancel.");
            return true;
        }

        private void SpawnProjectile(ThrowableDefinition def)
        {
            if (def.ProjectilePrefab == null)
            {
                Debug.LogError($"[ItemUseSystem] '{def.DisplayName}' has no ProjectilePrefab assigned!");
                return;
            }

            Vector3    pos = transform.position + transform.forward * 0.5f + Vector3.up * 1.5f;
            Quaternion rot = Quaternion.LookRotation(transform.forward);
            var        go  = Instantiate(def.ProjectilePrefab, pos, rot);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = transform.forward * def.ThrowForce;

            var proj = go.GetComponent<Projectile>();
            if (proj != null) proj.Initialize(def);

            Debug.Log($"[ItemUseSystem] Spawned projectile '{def.DisplayName}' (force {def.ThrowForce}).");
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Weapon Holster / Restore

        private void HolsterAndSave()
        {
            _previousWeaponSlot = _weaponSystem?.GetActiveWeaponSlot();
            if (_previousWeaponSlot.HasValue)
            {
                _weaponSystem.HolsterWeapon();
                Debug.Log($"[ItemUseSystem] Holstered: {_previousWeaponSlot}.");
            }
        }

        private void RestoreWeapon()
        {
            if (_previousWeaponSlot.HasValue)
            {
                _weaponSystem?.SelectWeapon(_previousWeaponSlot.Value);
                Debug.Log($"[ItemUseSystem] Restored: {_previousWeaponSlot}.");
            }
            _previousWeaponSlot = null;
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Shared Helpers

        private void ConsumeItem(ItemInstance item)
        {
            _inventorySystem?.RemoveItem(item.InstanceID, 1);
            Debug.Log($"[ItemUseSystem] Consumed 1× '{item.DefinitionID}'.");
        }

        private void CompleteUse(ItemInstance item)
        {
            _isUsingItem  = false;
            _currentItem  = null;
            _useCoroutine = null;
            OnItemUseCompleted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Use completed: '{item?.DefinitionID}'.");
            RestoreWeapon();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Debug

        [ContextMenu("Log State")]
        public void LogState() =>
            Debug.Log($"[ItemUseSystem] Using={_isUsingItem} | Item={_currentItem?.DefinitionID ?? "none"} | PrevSlot={_previousWeaponSlot}");

        #endregion
    }
}