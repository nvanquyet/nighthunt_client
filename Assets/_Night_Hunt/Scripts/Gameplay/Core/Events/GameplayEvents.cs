using NightHunt.Gameplay.Match;
using UnityEngine;

namespace NightHunt.Gameplay.Core.Events
{
    // ── Match Flow ────────────────────────────────────────────────────────────

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
    }

    /// <summary>A player has respawned.</summary>
    public struct PlayerRespawnedEvent : IGameplayEvent
    {
        public float Timestamp => Time.time;
        public string PlayerName;
        public int TeamId;
        public UnityEngine.Vector3 Position;
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
}
