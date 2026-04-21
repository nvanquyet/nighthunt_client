namespace NightHunt.Gameplay.Input
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  InputLayer – Flags enum; each bit represents one ActionMap.
    //  Combine with bitwise OR to form context presets.
    // ──────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Input layers corresponding to each ActionMap in InputSystem_Actions.
    /// Combine with bitwise OR.
    /// </summary>
    [System.Flags]
    public enum InputLayer
    {
        None        = 0,
        Player      = 1 << 0,   // Move, Sprint, Crouch, Interact, Pickup, LogNearby
        Combat      = 1 << 1,   // Fire, AimDownSights, Reload, SwitchWeapon, WeaponSlots, Grenade
        Camera      = 1 << 2,   // RotateLeft/Right, Look, Zoom
        Inventory   = 1 << 3,   // OpenInventory, DropItem, ItemSelection, UseConsumable
        Team        = 1 << 4,   // Ping, RequestHelp, Revive, VoiceChat, Scoreboard
        UI          = 1 << 5,   // Navigate, Submit, Cancel, Point, Click, OpenMenu
        Spectator   = 1 << 6,   // NextPlayer, PreviousPlayer, FreeCamera
        Objectives  = 1 << 7,   // InteractObjective, CaptureZone, HackRadar
        Devices     = 1 << 8,   // PlaceTrap, Drone, VisionDevice, DetonateTrap
        Debug       = 1 << 9,   // ToggleDebugUI, SpawnTestItem, GodMode
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  InputState – Current game context, mapped to a combination of InputLayer flags.
    // ──────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Overall gameplay state; each state maps to a preset InputLayer combination.
    /// <para>Use <see cref="Core.InputLayerManager.PushContext"/> /
    /// <see cref="Core.InputLayerManager.PopContext"/> to switch states.</para>
    /// </summary>
    public enum InputState
    {
        None,

        /// <summary>Normal gameplay (Player + Combat + Camera + ...).</summary>
        PlayerAlive,

        /// <summary>Inventory / Equipment open — Combat and Player movement disabled.</summary>
        InventoryOpen,

        /// <summary>Map view — Camera + UI only.</summary>
        MapOpen,

        /// <summary>Pause menu — UI only</summary>
        Paused,

        /// <summary>Controlling a drone / remote-sensing device. Reserved — no DroneInputHandler exists yet.</summary>
        DroneControl,

        /// <summary>Spectating after death</summary>
        Spectating,

        /// <summary>Free-fly spectator camera — Camera ON, Spectator ON, UI ON. Everything else OFF.</summary>
        SpectatorFreeCamera,

        /// <summary>Dead, waiting for respawn — UI + Team only</summary>
        PlayerDead,   // implicit = 8

        // ── Explicit-numbered states (must be > 8 to avoid collision with PlayerDead) ──
        /// <summary>Cutscene / loading — no input accepted</summary>
        Cinematic = 12,

        /// <summary>Scout mode (move + camera, no combat).</summary>
        ScoutMode = 13,

        /// <summary>Camera controls only.</summary>
        Camera = 14,

        /// <summary>Dialogue — UI only.</summary>
        InDialogue = 15,

        // ── Legacy aliases (kept to avoid breaking existing code) ───────────────
    // ⚠️ MUST be placed LAST, after all real values, to avoid C# auto-increment collisions.
        /// <inheritdoc cref="Paused"/>
        MenuOpen = Paused,
    }
}

