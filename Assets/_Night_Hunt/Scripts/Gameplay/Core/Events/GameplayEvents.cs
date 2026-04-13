using NightHunt.Gameplay.Match;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    // ── Match Flow ────────────────────────────────────────────────────────────

    /// <summary>First player has spawned on the server. Clients should advance to the "Spawning" loading stage.</summary>
    public struct SpawningStartedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
    }

    /// <summary>
    /// One player has successfully spawned on the server.
    /// Fired after EVERY individual spawn so clients can show per-player progress
    /// (e.g. "2 / 4 players joined").
    /// </summary>
    public struct PlayerSpawnedEvent : IGameplayEvent
    {
        public float  Timestamp    => Time.time;
        /// <summary>Display name of the player who just spawned.</summary>
        public string DisplayName;
        /// <summary>How many players have spawned so far (including this one).</summary>
        public int    SpawnedCount;
        /// <summary>Total expected players for this match.</summary>
        public int    ExpectedCount;
    }

    /// <summary>All expected players have spawned. Clients should dismiss loading screen.</summary>
    public struct AllPlayersReadyEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
    }

    /// <summary>
    /// Phase transition is approaching.
    /// Fired by MatchPhaseManager <see cref="WarningTime"/> seconds before the phase ends.
    /// </summary>
    public struct PhaseWarningEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>The phase that is about to end.</summary>
        public MatchPhaseState CurrentPhase;
        /// <summary>Seconds remaining until the phase transitions.</summary>
        public float SecondsRemaining;
    }

    /// <summary>A team has been fully eliminated (no alive members and no beacons).</summary>
    public struct TeamEliminatedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public int EliminatedTeamId;
        public int WinnerTeamId;
        /// <summary>Phase in which elimination happened.</summary>
        public MatchPhaseState Phase;
    }

    /// <summary>Match has ended. Broadcast to all clients with final result.</summary>
    public struct MatchEndedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>-1 means DRAW.</summary>
        public int WinnerTeamId;
        public MatchEndReason Reason;
        public MatchResult[] PlayerResults;
    }

    public enum MatchEndReason
    {
        TeamEliminated,   // A team was cleared in Phase 1/2/3
        TimerExpired,     // Phase 3 timer ran out → tiebreak resolved
        Draw              // Both teams tied on all tiebreak criteria
    }

    /// <summary>Per-player result data sent at match end.</summary>
    [System.Serializable]
    public struct MatchResult
    {
        public string BackendPlayerId;
        public string DisplayName;
        public int TeamId;
        public int Kills;
        public int Deaths;
        public int Score;
        /// <summary>Change in ELO (positive = gain, negative = loss). 0 for Custom mode.</summary>
        public int EloChange;
    }

    // ── Beacon ────────────────────────────────────────────────────────────────

    /// <summary>A beacon has been destroyed by an enemy or expired.</summary>
    public struct BeaconDestroyedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>Team that owned the beacon.</summary>
        public int OwnerTeamId;
        /// <summary>Remaining active beacons for that team after destruction.</summary>
        public int RemainingBeaconCount;
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on all clients whenever the server syncs score data.
    /// Consumers (e.g. ScoreboardHUD) should deserialize <see cref="ScoreDataJson"/>.
    /// </summary>
    public struct ScoreDataSyncedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public string ScoreDataJson;
    }

    // ── Boss ─────────────────────────────────────────────────────────────────

    /// <summary>A boss has been spawned on the map.</summary>
    public struct BossSpawnedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public string BossId;
        public UnityEngine.Vector3 Position;
    }

    /// <summary>A boss has been killed. A chest has been spawned at its position.</summary>
    public struct BossKilledEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public string BossId;
        public UnityEngine.Vector3 ChestPosition;
        public int KillerTeamId;
    }

    // ── Player Lifecycle ─────────────────────────────────────────────────────

    /// <summary>A player has died/been killed. Useful for global KillFeed or notifications.</summary>
    public struct PlayerKilledEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public string VictimName;
        public string KillerName;
        public string WeaponId;
        public int VictimTeamId;
        /// <summary>NetworkObject.ObjectId of the killer. 0 = world/environment kill.</summary>
        public uint KillerNetworkObjectId;
        /// <summary>NetworkObject.ObjectId of the victim.</summary>
        public uint VictimNetworkObjectId;
        /// <summary>TeamId of the killer. -1 = environment/unknown.</summary>
        public int KillerTeamId;
    }

    /// <summary>A player has respawned.</summary>
    public struct PlayerRespawnedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public string PlayerName;
        public int TeamId;
        public UnityEngine.Vector3 Position;
    }

    // ── Match Countdown ──────────────────────────────────────────────────────

    /// <summary>
    /// Broadcast each second during the pre-match countdown from MatchPhaseManager.BeginMatch().
    /// SecondsRemaining == 0 means "GO!" — the first phase starts immediately after this tick.
    /// Subscribe in HUD/UI to play a beep sound and display the number on screen.
    /// </summary>
    public struct MatchCountdownEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>Seconds remaining before the first phase starts. 0 = starting now.</summary>
        public int SecondsRemaining;
    }

    // ── Respawn UI ───────────────────────────────────────────────────

    /// <summary>Server started a respawn countdown. HUD shows a timer.</summary>
    public struct RespawnTimerEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>Total delay in seconds. UI counts down from this value.</summary>
        public float DelaySeconds;
    }

    /// <summary>Respawn was cancelled (e.g. beacon destroyed during timer).</summary>
    public struct RespawnCancelledEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>"beacon_destroyed" or other reason string.</summary>
        public string Reason;
    }

    /// <summary>
    /// Fired (server + all clients via eventual sync) when an objective is fully captured.
    /// KillFeedUI subscribes to show "[Team 0] captured Radar Station".
    /// </summary>
    public struct ObjectiveCapturedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        /// <summary>Unique objective identifier (e.g. "CAPTURE_ZONE", "RADAR_STATION").</summary>
        public string ObjectiveId;
        /// <summary>Human-readable display name shown in KillFeed.</summary>
        public string ObjectiveName;
        /// <summary>Team ID that completed the capture.</summary>
        public int CapturingTeamId;
    }

    /// <summary>
    /// Fired when a LockdownZone debuff is applied to a player.
    /// UI can show a zone warning overlay.
    /// </summary>
    public struct ZoneDebuffAppliedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public int PlayerNetObjId;
        public string ZoneId;
        public float DamagePerSecond;
    }

    /// <summary>
    /// Fired when local player enters/exits spectate mode.
    /// GameHUD subscribes to hide/show combat panels.
    /// </summary>
    public struct SpectatorModeChangedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public bool IsSpectating;
    }
}
