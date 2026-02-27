namespace NightHunt.StatSystem.Core.Types
{
    /// <summary>
    /// Defines all item stat types — intrinsic to the item itself.
    ///
    /// DESIGN RULES:
    /// - These stats belong to the ITEM (weapon/equipment), NOT the player.
    /// - Attachments modify these via ItemStatModifier[].
    /// - WeaponSystem reads ComputedStats when firing/reloading.
    /// - Stats that affect the PLAYER (Armor, VisionRange, etc.) use PlayerStatModifier, NOT here.
    ///
    /// REMOVED (do not add back):
    /// - Range       : projectile lifetime governed by PlayerStat.VisionRange
    /// - Recoil      : top-down; no vertical recoil — use SpreadPenalty instead
    /// - AimSpeed    : FPS concept — replaced by DrawSpeed
    /// - Weight      : item weight lives on ItemDefinition.Weight field
    /// - ResourceType / MaxResource / DefaultResource : removed from ItemDefinition;
    ///                  now MaxAmmo / MaxDurability / BatteryCapacity live HERE in StatConfig.Stats[]
    ///                  → attachment-buffable, inspectable, unified stat pipeline.
    /// </summary>
    public enum ItemStatType
    {
        // ── Weapon: Combat ────────────────────────────────────────────────────
        /// <summary>Damage per bullet/hit.</summary>
        Damage          = 10,

        /// <summary>Rounds per minute.</summary>
        FireRate        = 11,

        /// <summary>Base accuracy 0-100. Higher = tighter cone.</summary>
        Accuracy        = 12,

        /// <summary>Initial horizontal spread cone (degrees).</summary>
        SpreadBase      = 13,

        /// <summary>Spread added per shot when firing continuously (degrees/shot).</summary>
        SpreadPenalty   = 14,

        /// <summary>Spread recovered per second after stopping fire (degrees/s).</summary>
        SpreadRecovery  = 15,

        // ── Weapon: Handling ─────────────────────────────────────────────────
        /// <summary>Magazine capacity. Attachment (Magazine type) can buff this.</summary>
        MagazineSize    = 16,

        /// <summary>Time to draw/holster weapon (seconds).</summary>
        DrawSpeed       = 17,

        /// <summary>Time to reload a full magazine (seconds).</summary>
        ReloadSpeed     = 18,

        /// <summary>
        /// Total ammo reserve capacity (e.g. 300 rounds).S
        /// Replaces ItemDefinition.MaxResource for weapons.
        /// Attachment (ExtMag, MagPouch) can modify this.
        /// instance.CurrentResource = current reserve ammo (runtime, decreases on reload).
        /// </summary>
        MaxAmmo         = 20,

        // ── Equipment ────────────────────────────────────────────────────────
        /// <summary>
        /// Base armor value of this equipment piece.
        /// Stored in StatConfig.Stats[] → can be buffed by armor plate attachments.
        /// StatApplyOrchestrator reads the computed stat and pushes → PlayerStatType.Armor.
        /// </summary>
        ArmorValue      = 30,

        /// <summary>
        /// Maximum durability of this equipment piece.
        /// Replaces EquipmentDefinition.MaxDurability field.
        /// Stored in StatConfig.Stats[] → can be buffed by armor plate attachments.
        /// instance.CurrentResource = current durability (runtime, decreases on damage).
        /// </summary>
        MaxDurability   = 31,

        // ── Attachment ───────────────────────────────────────────────────────
        /// <summary>
        /// Max battery/energy capacity for powered attachments (flashlight, night-vision).
        /// Replaces attachment.MaxResource = 3600f.
        /// instance.CurrentResource = remaining battery (runtime, decreases while active).
        /// </summary>
        BatteryCapacity = 40,
    }

    /// <summary>
    /// Controls how a current value is adjusted when the corresponding max stat changes
    /// (e.g. an attachment is equipped / unequipped that increases MaxDurability or MagazineSize).
    ///
    /// RULES:
    /// - <see cref="ClampOnly"/>    — never exceed new max; never scale up.
    ///   Use for ammo / magazine rounds (equipping an Ext-Mag doesn't auto-fill bullets).
    /// - <see cref="Proportional"/> — scale current proportionally: newCurrent = current / oldMax * newMax.
    ///   Use for durability / battery (slotting an Armor Plate increases max AND current HP proportionally).
    /// </summary>
    public enum CurrentValueAdjustMode
    {
        /// <summary>
        /// current = Mathf.Min(current, newMax).
        /// Does NOT scale up when max increases.
        /// </summary>
        ClampOnly,

        /// <summary>
        /// current = current / oldMax * newMax  (proportional scaling).
        /// Scales both up and down so the fraction stays the same.
        /// </summary>
        Proportional,
    }
}
