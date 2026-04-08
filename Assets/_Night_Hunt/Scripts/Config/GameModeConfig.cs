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

        [Tooltip("If false, displayed as 'Coming Soon' and cannot be selected.")]
        public bool isEnabled;
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
    //   - PartyMatchmakingRequest.gameMode
    // ══════════════════════════════════════════════════════════════════════════

    [CreateAssetMenu(fileName = "GameModeConfig", menuName = "NightHunt/Config/Game Mode Config")]
    public class GameModeConfig : ScriptableObjectSingleton<GameModeConfig>
    {
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
            }
        };

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

            var entries = new GameModeEntry[remoteModes.Length];
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
                    isEnabled      = dto.matchmakingEnabled && dto.modeStatus == "AVAILABLE"
                };
            }

            Instance.modes = entries;
        }

        /// <summary>All entries (enabled and disabled).</summary>
        public static GameModeEntry[] GetAll()
        {
            if (Instance == null)
            {
                Debug.LogError("[GameModeConfig] Instance not found! Place GameModeConfig.asset in Resources/.");
                return Array.Empty<GameModeEntry>();
            }
            return Instance.modes;
        }

        /// <summary>Only enabled entries.</summary>
        public static GameModeEntry[] GetEnabled()
        {
            var all   = GetAll();
            int count = 0;
            foreach (var m in all) if (m.isEnabled) count++;

            var result = new GameModeEntry[count];
            int i = 0;
            foreach (var m in all) if (m.isEnabled) result[i++] = m;
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
