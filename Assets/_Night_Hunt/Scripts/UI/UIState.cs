
namespace NightHunt.UI
{
    /// <summary>
    /// All possible states of the in-game UI.
    /// <see cref="GameHUDController"/> is the single source of truth for transitions.
    ///
    /// State → Panel visibility matrix:
    ///
    /// | State      | WeaponHUD | ItemSel | Stats | Crosshair | Inventory | Death | Spectator | Results |
    /// |------------|-----------|---------|-------|-----------|-----------|-------|-----------|---------|
    /// | Combat     |    ✓      |   ✓     |  ✓    |    ✓      |           |       |           |         |
    /// | Inventory  |           |         |  ✓    |           |    ✓      |       |           |         |
    /// | Dead       |           |         |       |           |           |   ✓   |           |         |
    /// | Spectating |           |         |       |           |           |       |    ✓      |         |
    /// | PostMatch  |           |         |       |           |           |       |           |    ✓    |
    ///
    /// KillFeedUI and MinimapUI are always visible during active play (Combat/Spectating).
    /// MatchInfoHUD is visible in Combat and Spectating.
    /// </summary>
    public enum UIState
    {
        /// <summary>Default in-game state: combat HUD visible, inventory closed.</summary>
        Combat = 0,

        /// <summary>Inventory panel open: combat HUD hidden, cursor visible, stats panel shown.</summary>
        Inventory = 1,

        /// <summary>Local player has died: death screen visible, all personal HUD hidden.</summary>
        Dead = 2,

        /// <summary>Spectating another player: spectator HUD visible, personal HUD hidden.</summary>
        Spectating = 3,

        /// <summary>Match has ended: results screen visible, all gameplay HUD hidden.</summary>
        PostMatch = 4,

        /// <summary>Gameplay settings / pause overlay.</summary>
        Settings = 5,
    }
}
