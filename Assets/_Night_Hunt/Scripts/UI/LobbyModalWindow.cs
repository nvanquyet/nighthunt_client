// Deprecated — use GameModalWindow instead.
// This file only exists to prevent compilation errors during migration.
// Delete after all callers are updated.
using System;

namespace NightHunt.UI
{
    /// <summary>
    /// Obsolete alias. Use <see cref="GameModalWindow"/> instead.
    /// </summary>
    [System.Obsolete("Use GameModalWindow.Instance instead of LobbyModalWindow.Instance")]
    public sealed class LobbyModalWindow
    {
        // Redirect to GameModalWindow
        public static GameModalWindow Instance => GameModalWindow.Instance;
    }
}
