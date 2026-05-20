using System;
using NightHunt.Core;
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
        [Tooltip("Key sent to server (e.g. '2v2', '4v4'). Must match server GameMode values.")]
        public string modeKey;

        [Tooltip("Display name shown in the mode-selector dropdown.")]
        public string displayName;

        [Tooltip("Short description shown as a sub-label.")]
        public string description;

        [Tooltip("Number of players per team (1, 2, 4).")]
        public int playersPerTeam;

        [Tooltip("If true, server may fill empty slots with solo players.")]
        public bool allowFill;

        [Tooltip("If false, displayed as 'Coming Soon' and cannot be selected in any mode.")]
        public bool isEnabled;

        [Tooltip("If true, this mode can be queued via matchmaking (ranked / quick-play).")]
        public bool matchmakingEnabled;

        [Tooltip("Dev/test only. Shown in Editor and Development builds — never in production.")]
        public bool isDevMode;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GameModeConfig — ScriptableObject singleton.
    //
    // SETUP:
    //   1. Right-click → Create → NightHunt/Config/Game Mode Config
    //   2. Save to _Night_Hunt/Data/Configs/CoreSystem Config/Resources/GameModeConfig.asset
    //   3. Configure entries — modeKey must match server GameMode enum strings.
    //
    // USED BY:
    //   - HomeView → GameModeSelector (HorizontalSelector from Shift UI)
    //   - PartyService.QueueParty(gameMode, allowFill)
    //   - PartyRankedQueueRequest.gameMode
    // ══════════════════════════════════════════════════════════════════════════

    [CreateAssetMenu(fileName = "GameModeConfig", menuName = "NightHunt/Config/Game Mode Config")]
    public class GameModeConfig : ScriptableObjectSingleton<GameModeConfig>
    {
        private static readonly GameModeEntry[] FallbackModes =
        {
            new GameModeEntry
            {
                modeKey        = "1v1",
                displayName    = "1 vs 1",
                description    = "Duel test mode",
                playersPerTeam = 1,
                allowFill      = false,
                isEnabled      = true,
                matchmakingEnabled = false,
                isDevMode      = true
            },
            new GameModeEntry
            {
                modeKey        = "2v2",
                displayName    = "2 vs 2",
                description    = "Team of 2",
                playersPerTeam = 2,
                allowFill      = true,
                isEnabled      = true,
                matchmakingEnabled = true
            },
            new GameModeEntry
            {
                modeKey        = "4v4",
                displayName    = "4 vs 4",
                description    = "Team of 4",
                playersPerTeam = 4,
                allowFill      = true,
                isEnabled      = true,
                matchmakingEnabled = true
            }
        };

        [Header("Game Modes")]
        [Tooltip("Ordered list of game modes shown in the selector. Populated at runtime by GameConfigService.FetchAsync().")]
        [SerializeField] private GameModeEntry[] modes = new GameModeEntry[]
        {
            new GameModeEntry
            {
                modeKey        = "2v2",
                displayName    = "2 vs 2",
                description    = "Team of 2",
                playersPerTeam = 2,
                allowFill      = true,
                isEnabled      = true
            },
            new GameModeEntry
            {
                modeKey        = "3v3",
                displayName    = "3 vs 3",
                description    = "Team of 3",
                playersPerTeam = 3,
                allowFill      = true,
                isEnabled      = true
            },
            new GameModeEntry
            {
                modeKey        = "4v4",
                displayName    = "4 vs 4",
                description    = "Team of 4",
                playersPerTeam = 4,
                allowFill      = true,
                isEnabled      = true
            },
            new GameModeEntry
            {
                modeKey        = "5v5",
                displayName    = "5 vs 5",
                description    = "Team of 5",
                playersPerTeam = 5,
                allowFill      = true,
                isEnabled      = false  // Coming Soon — default disabled until server enables
            },
            new GameModeEntry
            {
                modeKey            = "1v1",
                displayName        = "1 vs 1 (Dev)",
                description        = "2-player test mode. Both players queue 1v1 → matched together.",
                playersPerTeam     = 1,
                allowFill          = false,
                isEnabled          = true,
                matchmakingEnabled = true,
                isDevMode          = true
            }
        };

        public static event System.Action OnConfigLoaded;

        private static void SafeInvokeOnConfigLoaded()
        {
            if (OnConfigLoaded == null) return;
            var invocationList = OnConfigLoaded.GetInvocationList();
            foreach (var del in invocationList)
            {
                try { del.DynamicInvoke(); }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[GameModeConfig] Error in OnConfigLoaded listener '{del.Method.DeclaringType}.{del.Method.Name}': {ex}");
                }
            }
        }

        /// <summary>
        /// Populate this config from server data (called by GameConfigService at startup).
        /// Overwrites the default Inspector entries with DB-driven values.
        /// Falls back to existing Inspector defaults if <paramref name="remoteModes"/> is null/empty.
        /// </summary>
        public static void LoadFromRemote(GameModeResponseDTO[] remoteModes)
        {
            if (Instance == null)
            {
                Debug.LogError("[GameModeConfig] Instance not found — cannot apply remote config.");
                return;
            }

            if (remoteModes == null || remoteModes.Length == 0)
            {
                Debug.LogWarning("[GameModeConfig] LoadFromRemote received empty list — keeping local defaults.");
                return;
            }

            // Preserve any local isDevMode entries that have no server counterpart
            // (e.g. future locally-added test modes not yet in the DB).
            var devEntries = new System.Collections.Generic.List<GameModeEntry>();
            if (Instance.modes != null)
                foreach (var m in Instance.modes)
                    if (m.isDevMode && !System.Array.Exists(remoteModes, r => r.modeKey == m.modeKey))
                        devEntries.Add(m);

            var entries = new GameModeEntry[remoteModes.Length + devEntries.Count];
            for (int i = 0; i < remoteModes.Length; i++)
            {
                var dto = remoteModes[i];
                entries[i] = new GameModeEntry
                {
                    modeKey        = dto.modeKey,
                    displayName    = dto.displayName,
                    description    = dto.description,
                    playersPerTeam = dto.playersPerTeam,
                    allowFill      = dto.allowFill,
                    isEnabled          = dto.modeStatus == "AVAILABLE",
                    matchmakingEnabled = dto.matchmakingEnabled,
                    isDevMode      = dto.isDevMode   // propagate dev flag from server
                };
            }
            for (int i = 0; i < devEntries.Count; i++)
                entries[remoteModes.Length + i] = devEntries[i];

            Instance.modes = entries;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[GameModeConfig] LoadFromRemote complete — {entries.Length} total entries written to Instance.modes:");
            foreach (var e in entries)
                sb.AppendLine($"  > [{(e.isEnabled ? "ON " : "OFF")}] key={e.modeKey}  display=\"{e.displayName}\"  allowFill={e.allowFill}  mmEnabled={e.matchmakingEnabled}  isDevMode={e.isDevMode}");
            Debug.Log(sb.ToString());

            SafeInvokeOnConfigLoaded();
        }

        /// <summary>All entries (enabled and disabled).</summary>
        public static GameModeEntry[] GetAll()
        {
            if (Instance == null)
            {
                Debug.LogWarning("[GameModeConfig] Instance not found. Using built-in fallback modes.");
                return FallbackModes;
            }
            if (Instance.modes == null || Instance.modes.Length == 0)
            {
                Debug.LogWarning("[GameModeConfig] No modes configured. Using built-in fallback modes.");
                return FallbackModes;
            }
            return Instance.modes;
        }

        /// <summary>
        /// Entries available for party custom mode (status=AVAILABLE) — plus dev entries in Editor/Development builds.
        /// Does NOT filter by matchmakingEnabled; use <see cref="GetMatchmakingEnabled"/> for the matchmaking queue.
        /// </summary>
        public static GameModeEntry[] GetEnabled()
        {
            // Show devMode entries in Editor AND Development builds (Debug.isDebugBuild is always
            // false in the Editor unless "Development Build" is ticked in Build Settings).
            bool showDev = Debug.isDebugBuild || Application.isEditor;
            var all   = GetAll();
            int count = 0;
            foreach (var m in all)
                if ((m.isEnabled && !m.isDevMode) || (m.isDevMode && showDev)) count++;

            var result = new GameModeEntry[count];
            int i = 0;
            foreach (var m in all)
                if ((m.isEnabled && !m.isDevMode) || (m.isDevMode && showDev)) result[i++] = m;
            return result;
        }

        /// <summary>
        /// Entries that can be queued via matchmaking (isEnabled AND matchmakingEnabled),
        /// plus dev entries in Editor/Development builds.
        /// Use this in <see cref="NightHunt.UI.PartyController"/> mode selector.
        /// </summary>
        public static GameModeEntry[] GetMatchmakingEnabled()
        {
            // Show devMode entries in Editor AND Development builds.
            bool showDev = Debug.isDebugBuild || Application.isEditor;
            var all   = GetAll();
            int count = 0;
            foreach (var m in all)
                if ((m.isEnabled && m.matchmakingEnabled && !m.isDevMode) || (m.isDevMode && showDev)) count++;

            var result = new GameModeEntry[count];
            int i = 0;
            foreach (var m in all)
                if ((m.isEnabled && m.matchmakingEnabled && !m.isDevMode) || (m.isDevMode && showDev)) result[i++] = m;
            return result;
        }

        /// <summary>
        /// Get display names of all enabled entries — for populating HorizontalSelector.
        /// Index in this array matches GetEnabled()[index].
        /// </summary>
        public static string[] GetEnabledDisplayNames()
        {
            var enabled = GetEnabled();
            var names   = new string[enabled.Length];
            for (int i = 0; i < enabled.Length; i++)
                names[i] = enabled[i].displayName;
            return names;
        }

        /// <summary>Get the first enabled entry whose modeKey matches.</summary>
        public static bool TryGetByKey(string modeKey, bool allowFill, out GameModeEntry entry)
        {
            foreach (var m in GetAll())
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

        // ── Editor validation ──────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (modes == null) return;
            foreach (var m in modes)
            {
                if (string.IsNullOrWhiteSpace(m.modeKey))
                    Debug.LogWarning("[GameModeConfig] A mode entry has an empty modeKey.");
                if (m.playersPerTeam <= 0)
                    Debug.LogWarning($"[GameModeConfig] '{m.displayName}' has playersPerTeam <= 0.");
            }
        }
#endif
    }
}
