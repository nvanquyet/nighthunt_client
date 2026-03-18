using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Runtime representation of an item instance
    /// 
    /// RESPONSIBILITIES:
    /// - Contains all data for a specific item in player's possession
    /// - Stores runtime state (quantity, resource, attachments)
    /// - Can be converted to/from ItemInstanceData for network sync
    /// 
    /// DESIGN:
    /// - Each item has unique InstanceID (GUID)
    /// - References ItemDefinition via DefinitionID
    /// - Stores runtime data (quantity, resource, attachments)
    /// - Can be converted to/from ItemInstanceData for network sync
    /// </summary>
    [System.Serializable]
    public class ItemInstance
    {
        #region Core Fields
        
        /// <summary>
        /// Unique identifier for this specific item instance
        /// Generated on creation, never changes
        /// </summary>
        public string InstanceID;
        
        /// <summary>
        /// Reference to ItemDefinition (ItemID from ScriptableObject)
        /// Used to lookup item properties from ItemDatabase
        /// </summary>
        public string DefinitionID;
        
        /// <summary>
        /// Stack quantity (1 for non-stackable items)
        /// Max determined by ItemDefinition.MaxStackSize
        /// </summary>
        public int Quantity;
        
        /// <summary>
        /// Position in inventory grid (-1 if in equipment/weapon slot)
        /// Can have gaps (e.g., items at index 0, 2, 5)
        /// </summary>
        public int InventoryIndex;
        
        #endregion
        
        #region Current Values (Generic Runtime Tracking)

        // ── DESIGN ──────────────────────────────────────────────────────────
        // _currentValues is the SINGLE SOURCE OF TRUTH at runtime.
        // It is [NonSerialized] — reconstructed from the backing fields on first access.
        //
        // KNOWN STAT-TO-FIELD MAPPINGS (for backward compat with ItemInstanceData):
        //   ItemStatType.MagazineSize                    → CurrentMagazine backing field
        //   ItemStatType.MaxAmmo / MaxDurability
        //                        / BatteryCapacity       → CurrentResource  backing field
        //
        // NEW CODE should use GetCurrentValue / SetCurrentValue / AdjustCurrentValue.
        // OLD CODE (that references .CurrentMagazine / .CurrentResource directly) continues
        // to work because those fields are kept in sync by SetCurrentValue.

        // ── Serialized backing (kept for ItemInstanceData compat & Inspector) ──
        /// <summary>[Backing] Rounds in magazine. Prefer GetCurrentValue(MagazineSize).</summary>
        public int CurrentMagazine;

        /// <summary>[Backing] Generic capacity resource (ammo reserve / durability / battery).
        /// Prefer GetCurrentValue(MaxAmmo | MaxDurability | BatteryCapacity).</summary>
        public float CurrentResource;

        // ── Runtime dictionary ───────────────────────────────────────────────
        [NonSerialized] private Dictionary<ItemStatType, float> _currentValues;

        private Dictionary<ItemStatType, float> EnsureCurrentValues()
        {
            if (_currentValues == null)
            {
                _currentValues = new Dictionary<ItemStatType, float>(4);
                // Seed from backing fields so first-access reflects persisted state.
                _currentValues[ItemStatType.MagazineSize] = CurrentMagazine;
                // We don't know which capacity type this instance uses without the definition,
                // so we do NOT pre-populate MaxAmmo/MaxDurability/BatteryCapacity here.
                // GetCurrentValue falls back to CurrentResource for those types.
            }
            return _currentValues;
        }

        // ── RESOURCE-STAT TYPES (order: first match wins in CurrentResource fallback) ─
        private static readonly ItemStatType[] s_resourceStatTypes =
            { ItemStatType.MaxAmmo, ItemStatType.MaxDurability, ItemStatType.BatteryCapacity };

        /// <summary>
        /// Returns the current value for <paramref name="type"/>.
        /// Falls back to backing fields when the dictionary has not been set yet.
        /// Returns the full computed max when no current value has been initialised
        /// (e.g. for stats that aren't durability/ammo).
        /// </summary>
        public float GetCurrentValue(ItemStatType type)
        {
            var dict = EnsureCurrentValues();

            if (dict.TryGetValue(type, out var v))
                return v;

            // ── Fallback to legacy backing fields ────────────────────────────
            if (type == ItemStatType.MagazineSize)
                return CurrentMagazine;

            foreach (var rt in s_resourceStatTypes)
                if (type == rt) return CurrentResource;

            // ── For un-tracked stats default to full computed max ─────────────
            return GetComputedStat(type);
        }

        /// <summary>
        /// Sets the current value for <paramref name="type"/>, clamped to [0, computedMax].
        /// Keeps legacy backing fields in sync for network serialisation.
        /// </summary>
        public void SetCurrentValue(ItemStatType type, float value)
        {
            float max = GetComputedStat(type, float.MaxValue);
            float clamped = Mathf.Clamp(value, 0f, max == float.MaxValue ? value : max);

            EnsureCurrentValues()[type] = clamped;

            // ── Sync legacy backing fields ─────────────────────────────────
            if (type == ItemStatType.MagazineSize)
            {
                CurrentMagazine = Mathf.RoundToInt(clamped);
                return;
            }
            foreach (var rt in s_resourceStatTypes)
            {
                if (type == rt) { CurrentResource = clamped; return; }
            }
        }

        /// <summary>Adjusts the current value by <paramref name="delta"/> (can be negative).</summary>
        public void AdjustCurrentValue(ItemStatType type, float delta)
            => SetCurrentValue(type, GetCurrentValue(type) + delta);

        /// <summary>
        /// Re-clamps (or proportionally scales) all tracked current values after the
        /// computed stats change (e.g. attachment equip / unequip).
        ///
        /// <paramref name="oldStats"/> — stat snapshot BEFORE the change (NULL = treat as all-zero).
        /// <paramref name="newStats"/> — stat snapshot AFTER  the change.
        /// <paramref name="adjustMode"/> — how to handle max increases:
        ///   <see cref="CurrentValueAdjustMode.ClampOnly"/>    → ammo / magazine (no auto-fill).
        ///   <see cref="CurrentValueAdjustMode.Proportional"/> → durability / battery (scale HP).
        /// </summary>
        public void ClampCurrentValuesToNewMax(
            Dictionary<ItemStatType, float> oldStats,
            Dictionary<ItemStatType, float> newStats,
            CurrentValueAdjustMode adjustMode)
        {
            if (newStats == null) return;

            var dict = EnsureCurrentValues();
            // Snapshot keys to avoid modifying during iteration.
            var keys = dict.Keys.ToArray();

            foreach (var type in keys)
            {
                if (!newStats.TryGetValue(type, out var newMax)) continue;
                if (newMax <= 0f) continue;

                float currentVal = dict[type];
                float newVal;

                if (adjustMode == CurrentValueAdjustMode.Proportional)
                {
                    float oldMax = (oldStats != null && oldStats.TryGetValue(type, out var om)) ? om : 0f;
                    newVal = (oldMax > 0f) ? (currentVal / oldMax * newMax) : newMax;
                }
                else // ClampOnly
                {
                    newVal = Mathf.Min(currentVal, newMax);
                }

                SetCurrentValue(type, newVal);
            }
        }

        /// <summary>
        /// Initialises current values to full capacity (call after computing stats for a new item).
        /// Only fills stats that are present in the computed stats dictionary.
        /// </summary>
        public void InitCurrentValuesToMax()
        {
            if (_computedStats == null) return;
            foreach (var kv in _computedStats)
            {
                // Only initialise "capacity" stats (don't override Damage, FireRate, etc.)
                bool isCapacity = kv.Key == ItemStatType.MagazineSize
                               || kv.Key == ItemStatType.MaxAmmo
                               || kv.Key == ItemStatType.MaxDurability
                               || kv.Key == ItemStatType.BatteryCapacity;
                if (isCapacity)
                    SetCurrentValue(kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Seeds <c>_currentValues</c> from the legacy backing fields.
        /// Called by <c>ItemStatComputer</c> on the FIRST compute, so that subsequent
        /// attachment changes can correctly clamp / scale the existing amounts.
        /// </summary>
        internal void SeedCurrentValuesFromBackingFields(Dictionary<ItemStatType, float> computedStats)
        {
            if (computedStats == null) return;

            // Magazine ammo → MagazineSize entry
            if (computedStats.ContainsKey(ItemStatType.MagazineSize))
                EnsureCurrentValues()[ItemStatType.MagazineSize] = CurrentMagazine;

            // Generic resource (ammo reserve / durability / battery) → first matching type
            foreach (var rt in s_resourceStatTypes)
            {
                if (computedStats.ContainsKey(rt))
                {
                    EnsureCurrentValues()[rt] = CurrentResource;
                    break; // Only one resource stat type per item
                }
            }
        }

        #endregion
        
        #region Attachments
        
        /// <summary>
        /// Array of attached item instance IDs
        /// Length = ItemDefinition.AttachmentSlots.Length
        /// null or empty string = no attachment in slot
        /// 
        /// Example: AK-47 with [Optic, Grip, Magazine, Barrel]
        /// AttachedItems = ["scope_instance_01", null, "mag_instance_02", "suppressor_01"]
        ///                  [Red Dot Sight]      [empty] [Ext. Mag]         [Suppressor]
        /// </summary>
        public string[] AttachedItems;
        
        #endregion
        
        #region Metadata
        
        /// <summary>
        /// Custom data string for future extensibility
        /// Can store JSON for quest data, crafting results, etc.
        /// </summary>
        public string CustomData;
        
        /// <summary>
        /// Unix timestamp when item was created
        /// Used for sorting (remove oldest first)
        /// </summary>
        public long CreatedTimestamp;
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Default constructor for FishNet serialization
        /// DO NOT USE directly - use other constructor
        /// </summary>
        public ItemInstance()
        {
            InstanceID = Guid.NewGuid().ToString();
            DefinitionID = string.Empty;
            Quantity = 1;
            InventoryIndex = -1;
            CurrentResource = 0f;
            CurrentMagazine = 0;
            AttachedItems = null;
            CustomData = string.Empty;
            CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        /// <summary>
        /// Create new item instance
        /// 
        /// PARAMETERS:
        /// - definitionID: ItemDefinition ID
        /// - quantity: Stack quantity
        /// - inventoryIndex: Position in inventory (-1 if equipped)
        /// </summary>
        public ItemInstance(string definitionID, int quantity = 1, int inventoryIndex = -1)
        {
            InstanceID = Guid.NewGuid().ToString();
            DefinitionID = definitionID;
            Quantity = Mathf.Max(1, quantity);
            InventoryIndex = inventoryIndex;
            CurrentResource = 0f;
            CurrentMagazine = 0;
            AttachedItems = null;
            CustomData = string.Empty;
            CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        #endregion
        
        #region Serialization
        
        /// <summary>
        /// Convert to network-syncable struct
        /// Used by SyncList in InventorySystem
        /// </summary>
        public ItemInstanceData ToData()
        {
            return new ItemInstanceData
            {
                InstanceID = this.InstanceID,
                DefinitionID = this.DefinitionID,
                Quantity = this.Quantity,
                InventoryIndex = this.InventoryIndex,
                CurrentResource = this.CurrentResource,
                CurrentMagazine = this.CurrentMagazine,
                AttachedItems = this.AttachedItems,
                CustomData = this.CustomData,
                CreatedTimestamp = this.CreatedTimestamp
            };
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Clone this item (new InstanceID)
        /// Used for splitting stacks
        /// </summary>
        public ItemInstance Clone()
        {
            return new ItemInstance(DefinitionID, Quantity, InventoryIndex)
            {
                InstanceID = Guid.NewGuid().ToString(),
                CurrentResource = this.CurrentResource,
                CurrentMagazine = this.CurrentMagazine,
                AttachedItems = this.AttachedItems != null ? (string[])this.AttachedItems.Clone() : null,
                CustomData = this.CustomData,
                CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
        
        /// <summary>
        /// Check if has attachment in specific slot
        /// </summary>
        public bool HasAttachment(int slotIndex)
        {
            if (AttachedItems == null || slotIndex < 0 || slotIndex >= AttachedItems.Length)
                return false;
            
            return !string.IsNullOrEmpty(AttachedItems[slotIndex]);
        }
        
        /// <summary>
        /// Get attachment instance ID in slot
        /// </summary>
        public string GetAttachment(int slotIndex)
        {
            if (AttachedItems == null || slotIndex < 0 || slotIndex >= AttachedItems.Length)
                return null;
            
            return AttachedItems[slotIndex];
        }
        
        /// <summary>
        /// Set attachment in slot
        /// </summary>
        public void SetAttachment(int slotIndex, string attachmentInstanceID)
        {
            if (AttachedItems == null)
                return;
            
            if (slotIndex >= 0 && slotIndex < AttachedItems.Length)
            {
                AttachedItems[slotIndex] = attachmentInstanceID;
            }
        }
        
        /// <summary>
        /// Clear attachment from slot
        /// </summary>
        public void ClearAttachment(int slotIndex)
        {
            SetAttachment(slotIndex, null);
        }
        
        /// <summary>
        /// Get number of attached items
        /// </summary>
        public int GetAttachmentCount()
        {
            if (AttachedItems == null)
                return 0;
            
            int count = 0;
            foreach (var item in AttachedItems)
            {
                if (!string.IsNullOrEmpty(item))
                    count++;
            }
            return count;
        }
        
        #endregion

        #region Computed Item Stats (Runtime - Non-Serialized)

        // ── Lazy-computed stats: base + all attachment modifiers ─────────────────
        // These are NOT synced over network — recomputed locally from authoritative data.
        // StatApplyOrchestrator calls SetComputedStats() after recalculation.

        [NonSerialized] private Dictionary<ItemStatType, float> _computedStats;
        [NonSerialized] private bool _statsDirty = true;

        /// <summary>True when computed stats need to be recalculated (e.g. after attachment change).</summary>
        public bool IsStatsDirty => _statsDirty;

        /// <summary>Mark computed stats as stale. Call after any attachment add/remove.</summary>
        public void MarkStatsDirty() => _statsDirty = true;

        /// <summary>
        /// Set precomputed stats. Called by <c>ItemStatComputer</c> inside StatApplyOrchestrator.
        /// Clears dirty flag.
        /// </summary>
        public void SetComputedStats(Dictionary<ItemStatType, float> stats)
        {
            _computedStats = stats;
            _statsDirty = false;
        }

        /// <summary>
        /// Get a computed stat value (base + attachment modifiers).
        /// Returns 0 if stats haven't been computed yet.
        /// </summary>
        public float GetComputedStat(ItemStatType type)
        {
            if (_computedStats == null) return 0f;
            return _computedStats.TryGetValue(type, out var v) ? v : 0f;
        }

        /// <summary>
        /// Get a computed stat value with an explicit fallback default.
        /// Returns <paramref name="defaultValue"/> if the stat is missing or
        /// computed stats have not been populated yet.
        /// </summary>
        public float GetComputedStat(ItemStatType type, float defaultValue)
        {
            if (_computedStats == null) return defaultValue;
            return _computedStats.TryGetValue(type, out var v) ? v : defaultValue;
        }

        /// <summary>True when computed stats are available and up-to-date.</summary>
        public bool HasValidComputedStats => _computedStats != null && !_statsDirty;

        /// <summary>
        /// Returns a shallow copy of the computed stats dictionary.
        /// Used by <c>ItemStatComputer</c> to take a before/after snapshot for
        /// <see cref="ClampCurrentValuesToNewMax"/>.
        /// Returns null when stats have not been computed yet.
        /// </summary>
        public Dictionary<ItemStatType, float> GetComputedStatsSnapshot()
            => _computedStats != null ? new Dictionary<ItemStatType, float>(_computedStats) : null;

        #endregion

        #region Debug
        
        public override string ToString()
        {
            return $"ItemInstance[{InstanceID.Substring(0, 8)}...] {DefinitionID} x{Quantity} @{InventoryIndex}";
        }
        
        #endregion
    }
}
