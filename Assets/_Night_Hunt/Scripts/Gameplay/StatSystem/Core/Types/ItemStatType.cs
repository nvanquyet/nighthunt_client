
namespace NightHunt.Gameplay.StatSystem.Core.Types
{
    /// <summary>
    /// All numeric stats that belong to an item (weapon, equipment, attachment).
    ///
    /// DESIGN RULES:
    ///   • These stats describe the ITEM, not the player.
    ///   • Attachments buff these via ItemStatModifier[].
    ///   • WeaponSystem reads computed values when firing / reloading.
    ///   • Stats that modify the PLAYER (VisionRange, Armor granted to player …)
    ///     use PlayerStatModifier, not entries here.
    ///   • Never reorder or renumber existing entries — values are serialised in
    ///     ScriptableObject assets and network packets.
    /// </summary>
    public enum ItemStatType
    {
        // ── Weapon: Combat ─────────────────────────────────────────────────────

        /// <summary>Damage per bullet / hit (body shot baseline).</summary>
        Damage          = 10,

        /// <summary>Rounds per minute.</summary>
        FireRate        = 11,

        /// <summary>Base accuracy 0–100. Higher value = tighter effective cone.</summary>
        Accuracy        = 12,

        /// <summary>
        /// Base spread half-angle in degrees (circle radius on unit sphere).
        /// Spread is applied radially using Random.insideUnitCircle — both horizontal
        /// and vertical offset are random within this cone, not just horizontal.
        /// </summary>
        SpreadBase      = 13,

        /// <summary>
        /// Spread radius added per shot fired in quick succession (degrees / shot).
        /// Accumulated in the weapon model component; recovers at SpreadRecovery rate.
        /// </summary>
        SpreadPenalty   = 14,

        /// <summary>
        /// Spread radius recovered per second when the trigger is released (degrees / s).
        /// The spread never drops below SpreadBase.
        /// </summary>
        SpreadRecovery  = 15,

        /// <summary>
        /// Hard cap on the accumulated spread radius (degrees).
        /// When 0 the cap defaults to SpreadBase × 4 at runtime.
        /// </summary>
        SpreadMax       = 16,

        /// <summary>
        /// Horizontal recoil half-angle in degrees. Applied around the vertical axis.
        /// Lower is easier to control. Attachments such as grips can reduce this.
        /// </summary>
        RecoilHorizontal = 17,

        /// <summary>
        /// Vertical recoil kick in degrees. Positive values push shots upward.
        /// Lower is easier to control. Attachments such as grips can reduce this.
        /// </summary>
        RecoilVertical   = 18,

        // ── Weapon: Handling ────────────────────────────────────────────────────

        /// <summary>Magazine capacity. Buffable by Magazine-type attachments.</summary>
        MagazineSize    = 20,

        /// <summary>
        /// Seconds to draw or holster this weapon.
        /// Lower = faster transitions when swapping slots.
        /// </summary>
        DrawSpeed       = 21,

        /// <summary>Seconds to fully reload an empty magazine.</summary>
        ReloadSpeed     = 22,

        /// <summary>
        /// Total ammo reserve capacity (e.g. 300 rounds).
        /// instance.CurrentResource = current reserve at runtime (decreases on reload).
        /// </summary>
        MaxAmmo         = 23,

        // ── Equipment ──────────────────────────────────────────────────────────

        /// <summary>
        /// Base armour value of this equipment piece.
        /// StatApplyOrchestrator pushes the computed value → PlayerStatType.Armor.
        /// Buffable by armour-plate attachments.
        /// </summary>
        ArmorValue      = 30,

        /// <summary>
        /// Maximum durability of this equipment piece.
        /// instance.CurrentResource = current durability at runtime (decreases with damage).
        /// Buffable by plate attachments.
        /// </summary>
        MaxDurability   = 31,

        // ── Attachment ─────────────────────────────────────────────────────────

        /// <summary>
        /// Maximum battery / energy capacity for powered attachments (flashlight, NVGs).
        /// instance.CurrentResource = remaining charge at runtime.
        /// </summary>
        BatteryCapacity = 40,
    }

    /// <summary>
    /// How a tracked current value is adjusted when the corresponding max stat changes
    /// (e.g. an attachment is equipped that increases MaxDurability or MagazineSize).
    /// </summary>
    public enum CurrentValueAdjustMode
    {
        /// <summary>
        /// current = Mathf.Min(current, newMax).
        /// Does NOT scale up when the max increases.
        /// Use for ammo and magazine rounds (equipping an Ext-Mag doesn't auto-fill bullets).
        /// </summary>
        ClampOnly,

        /// <summary>
        /// current = current / oldMax × newMax  (proportional scaling).
        /// Scales both up and down so the ratio stays constant.
        /// Use for durability and battery (slotting an Armour Plate increases current HP proportionally).
        /// </summary>
        Proportional,
    }
}
