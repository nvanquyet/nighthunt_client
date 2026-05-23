using System;
using NightHunt.Data.DTOs;
using UnityEngine;

namespace NightHunt.Config
{
    // ══════════════════════════════════════════════════════════════════════════
    // GameModeEntry — per-mode data
    // ══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public struct GameModeEntry
    {
        public string modeKey;
        public string displayName;
        public string description;
        public int    playersPerTeam;
        public bool   allowFill;
        public bool   isEnabled;
        public bool   matchmakingEnabled;
        public bool   isDevMode;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GameModeConfig — static class. Source of truth is the server DB.
    // Populated at startup by GameConfigService.FetchAsync() → LoadFromRemote().
    // No .asset file, no ScriptableObject, no Inspector data.
    //
    // USED BY:
    //   - HomeView → GameModeSelector
    //   - PartyService.QueueParty(gameMode, allowFill)
    //   - PartyRankedQueueRequest.gameMode
    // ══════════════════════════════════════════════════════════════════════════

    public static class GameModeConfig
    {
        private static GameModeEntry[] _modes = Array.Empty<GameModeEntry>();

        public static event Action OnConfigLoaded;

        private static void SafeInvokeOnConfigLoaded()
        {
            if (OnConfigLoaded == null) return;
            var invocationList = OnConfigLoaded.GetInvocationList();
            foreach (var del in invocationList)
            {
                try { del.DynamicInvoke(); }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameModeConfig] Error in OnConfigLoaded listener '{del.Method.DeclaringType}.{del.Method.Name}': {ex}");
                }
            }
        }

        /// <summary>
        /// Populate from server data (called by GameConfigService at startup).
        /// Replaces current entries entirely. Ignored if remoteModes is null or empty.
        /// </summary>
        public static void LoadFromRemote(GameModeResponseDTO[] remoteModes)
        {
            if (remoteModes == null || remoteModes.Length == 0)
            {
                Debug.LogWarning("[GameModeConfig] LoadFromRemote received empty list — no data loaded.");
                return;
            }

            var entries = new GameModeEntry[remoteModes.Length];
            for (int i = 0; i < remoteModes.Length; i++)
            {
                var dto = remoteModes[i];
                entries[i] = new GameModeEntry
                {
                    modeKey            = dto.modeKey,
                    displayName        = dto.displayName,
                    description        = dto.description,
                    playersPerTeam     = dto.playersPerTeam,
                    allowFill          = dto.allowFill,
                    isEnabled          = dto.modeStatus == "AVAILABLE",
                    matchmakingEnabled = dto.matchmakingEnabled,
                    isDevMode          = dto.isDevMode
                };
            }

            _modes = entries;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[GameModeConfig] LoadFromRemote — {entries.Length} entries:");
            foreach (var e in entries)
                sb.AppendLine($"  > [{(e.isEnabled ? "ON " : "OFF")}] {e.modeKey}  \"{e.displayName}\"  fill={e.allowFill}  mm={e.matchmakingEnabled}  dev={e.isDevMode}");
            Debug.Log(sb.ToString());

            SafeInvokeOnConfigLoaded();
        }

        /// <summary>All entries (enabled and disabled). Empty until LoadFromRemote() is called.</summary>
        public static GameModeEntry[] GetAll() => _modes;

        /// <summary>AVAILABLE entries shown in party mode selector — plus dev entries in Editor/Development builds.</summary>
        public static GameModeEntry[] GetEnabled()
        {
            bool showDev = Debug.isDebugBuild || Application.isEditor;
            int  count   = 0;
            foreach (var m in _modes)
                if ((m.isEnabled && !m.isDevMode) || (m.isDevMode && showDev)) count++;

            var result = new GameModeEntry[count];
            int i = 0;
            foreach (var m in _modes)
                if ((m.isEnabled && !m.isDevMode) || (m.isDevMode && showDev)) result[i++] = m;
            return result;
        }

        /// <summary>Entries that can be queued via matchmaking, plus dev entries in Editor/Development builds.</summary>
        public static GameModeEntry[] GetMatchmakingEnabled()
        {
            bool showDev = Debug.isDebugBuild || Application.isEditor;
            int  count   = 0;
            foreach (var m in _modes)
                if ((m.isEnabled && m.matchmakingEnabled && !m.isDevMode) || (m.isDevMode && showDev)) count++;

            var result = new GameModeEntry[count];
            int i = 0;
            foreach (var m in _modes)
                if ((m.isEnabled && m.matchmakingEnabled && !m.isDevMode) || (m.isDevMode && showDev)) result[i++] = m;
            return result;
        }

        /// <summary>Display names of all enabled entries — for populating HorizontalSelector.</summary>
        public static string[] GetEnabledDisplayNames()
        {
            var enabled = GetEnabled();
            var names   = new string[enabled.Length];
            for (int i = 0; i < enabled.Length; i++)
                names[i] = enabled[i].displayName;
            return names;
        }

        /// <summary>Get the first enabled entry whose modeKey and allowFill match.</summary>
        public static bool TryGetByKey(string modeKey, bool allowFill, out GameModeEntry entry)
        {
            foreach (var m in _modes)
            {
                if (m.isEnabled && m.modeKey == modeKey && m.allowFill == allowFill)
                {
                    entry = m;
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
