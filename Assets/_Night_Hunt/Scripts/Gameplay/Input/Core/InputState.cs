namespace NightHunt.Gameplay.Input
{
    // ──────────────────────────────────────────────────────────────────────────────
    //  InputLayer – Flags enum, mỗi bit = 1 ActionMap
    //  Uses để OR các map lại thành context preset
    // ──────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Các tầng input tương ứng với từng ActionMap trong InputSystem_Actions.
    /// Có thể combine bằng bitwise OR.
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
    //  InputState – Context của game, ánh xạ tới tổ hợp InputLayer
    // ──────────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Trạng thái gameplay tổng thể, mỗi state có một preset InputLayer.
    /// <para>Uses <see cref="Core.InputLayerManager.PushContext"/> /
    /// <see cref="Core.InputLayerManager.PopContext"/> để chuyển đổi.</para>
    /// </summary>
    public enum InputState
    {
        None,

        /// <summary>Toàn bộ gameplay bình thường (Player + Combat + Camera + ...)</summary>
        PlayerAlive,

        /// <summary>Đang mở Inventory / Equipment – tắt Combat + Player movement</summary>
        InventoryOpen,

        /// <summary>Đang xem bản đồ – chỉ Camera + UI</summary>
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
        PlayerDead,

        /// <summary>Cutscene / loading — no input accepted</summary>
        Cinematic = 8,

        /// <summary>Scout mode (move + camera, không combat)</summary>
        ScoutMode = 9,

        /// <summary>Chỉ camera controls</summary>
        Camera = 10,

        /// <summary>Dialogue – chỉ UI</summary>
        InDialogue = 11,

        // ── Legacy aliases (giữ lại để tránh break code cũ) ──────────────────
        // ⚠️ PHẢI đặt CUỐI, SAU tất cả value thực, để tránh C# auto-increment collision
        /// <inheritdoc cref="Paused"/>
        MenuOpen = Paused,
    }
}

