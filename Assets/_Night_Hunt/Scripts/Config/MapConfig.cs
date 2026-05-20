using System;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using UnityEngine;

namespace NightHunt.Config
{
    // ══════════════════════════════════════════════════════════════════════════
    // MapEntry — per-map data (inner struct used by MapConfig)
    // ══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public struct MapEntry
    {
        [Tooltip("Unique string id, e.g. 'map_industrial'. Arbitrary, never sent to server.")]
        public string mapId;

        [Tooltip("Display name shown in the map-select UI.")]
        public string displayName;

        [Tooltip("Short description shown below the name (optional).")]
        [TextArea(1, 2)]
        public string description;

        [Tooltip("Preview / thumbnail sprite shown in the carousel.")]
        public Sprite icon;

        [Tooltip("Which scene to load when this map is played.")]
        public SceneId sceneId;

        [Tooltip("Game mode keys this map supports, e.g. '2v2', '4v4'. Leave empty = all modes.")]
        public string[] supportedModes;

        [Tooltip("If true the map is shown as 'Coming Soon' and cannot be selected.")]
        public bool isLocked;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MapConfig — ScriptableObject singleton, holds all map entries.
    //
    // SETUP:
    //   1. Right-click → Create → NightHunt/Config/Map Config
    //   2. Save to _Night_Hunt/Data/Configs/CoreSystem Config/Resources/MapConfig.asset
    //   3. Add entries: one MapEntry per playable map.
    //   4. SceneId values must match entries in SceneConfig.asset.
    //
    // ADDING A NEW MAP:
    //   1. Add SceneId.GameMap_XX to SceneConfig.cs enum (GameMap_03 = 102, etc.)
    //   2. Add SceneEntry in SceneConfig.asset inspector
    //   3. Add MapEntry here in MapConfig.asset inspector
    //   → No code changes needed anywhere else.
    // ══════════════════════════════════════════════════════════════════════════

    [CreateAssetMenu(fileName = "MapConfig", menuName = "NightHunt/Config/Map Config")]
    public class MapConfig : ScriptableObjectSingleton<MapConfig>
    {
        private static readonly MapEntry[] FallbackMaps =
        {
            new MapEntry
            {
                mapId          = "map_01",
                displayName    = "Industrial Zone",
                description    = "Urban combat in a derelict factory.",
                sceneId        = SceneId.GameMap_01,
                supportedModes = Array.Empty<string>(),
                isLocked       = false
            },
            new MapEntry
            {
                mapId          = "map_02",
                displayName    = "Arctic Base",
                description    = "Close-quarters in a frozen research facility.",
                sceneId        = SceneId.GameMap_02,
                supportedModes = Array.Empty<string>(),
                isLocked       = false
            }
        };

        [Header("Maps")]
        [Tooltip("All maps available in the game. Order determines display order in carousel.")]
        [SerializeField] private MapEntry[] maps = new MapEntry[]
        {
            new MapEntry
            {
                mapId          = "map_01",
                displayName    = "Industrial Zone",
                description    = "Urban combat in a derelict factory.",
                sceneId        = SceneId.GameMap_01,
                supportedModes = new[] { "2v2", "4v4" },
                isLocked       = false
            },
            new MapEntry
            {
                mapId          = "map_02",
                displayName    = "Arctic Base",
                description    = "Close-quarters in a frozen research facility.",
                sceneId        = SceneId.GameMap_02,
                supportedModes = new[] { "2v2", "4v4" },
                isLocked       = false
            }
        };

        // ── Public API ─────────────────────────────────────────────────────────

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
                    Debug.LogError($"[MapConfig] Error in OnConfigLoaded listener '{del.Method.DeclaringType}.{del.Method.Name}': {ex}");
                }
            }
        }

        /// <summary>
        /// Populate this config from server data (called by GameConfigService at startup).
        /// Converts backend sceneName string into SceneId enum via Enum.TryParse.
        /// Falls back to Inspector defaults if <paramref name="remoteMaps"/> is null/empty.
        /// </summary>
        public static void LoadFromRemote(GameMapResponseDTO[] remoteMaps)
        {
            if (Instance == null)
            {
                Debug.LogError("[MapConfig] Instance not found — cannot apply remote config.");
                return;
            }

            if (remoteMaps == null || remoteMaps.Length == 0)
            {
                Debug.LogWarning("[MapConfig] LoadFromRemote received empty list — keeping local defaults.");
                return;
            }

            var entries = new MapEntry[remoteMaps.Length];
            for (int i = 0; i < remoteMaps.Length; i++)
            {
                var dto = remoteMaps[i];

                // Convert sceneName string to SceneId enum.
                // The server sends the actual Unity scene file name ("02_Map_01"),
                // so first try a reverse lookup via SceneConfig (file name → enum).
                // Fall back to Enum.TryParse for backward compat if server ever sends "GameMap_01".
                SceneId sceneId = SceneId.GameMap_01; // safe fallback
                if (!string.IsNullOrEmpty(dto.sceneName))
                {
                    if (SceneConfig.TryGetSceneIdByName(dto.sceneName, out SceneId byName))
                        sceneId = byName;
                    else if (Enum.TryParse<SceneId>(dto.sceneName, ignoreCase: false, out SceneId byEnum))
                        sceneId = byEnum;
                    else
                        Debug.LogWarning($"[MapConfig] Unknown sceneName '{dto.sceneName}' for map '{dto.mapId}' — defaulting to GameMap_01.");
                }

                entries[i] = new MapEntry
                {
                    mapId          = dto.mapId,
                    displayName    = dto.displayName,
                    description    = dto.description,
                    icon           = null,  // sprites are baked into build; not served from backend
                    sceneId        = sceneId,
                    supportedModes = dto.supportedModes ?? Array.Empty<string>(),
                    isLocked       = dto.isLocked
                };
            }

            Instance.maps = entries;
            SafeInvokeOnConfigLoaded();
        }

        /// <summary>All map entries (locked and unlocked).</summary>
        public static MapEntry[] GetAll()
        {
            if (Instance == null)
            {
                Debug.LogWarning("[MapConfig] Instance not found. Using built-in fallback maps.");
                return FallbackMaps;
            }
            if (Instance.maps == null || Instance.maps.Length == 0)
            {
                Debug.LogWarning("[MapConfig] No maps configured. Using built-in fallback maps.");
                return FallbackMaps;
            }
            return Instance.maps;
        }

        /// <summary>Only maps that are not locked.</summary>
        public static MapEntry[] GetAvailable()
        {
            var all    = GetAll();
            int count  = 0;
            foreach (var m in all) if (!m.isLocked) count++;

            var result = new MapEntry[count];
            int i = 0;
            foreach (var m in all) if (!m.isLocked) result[i++] = m;
            return result;
        }

        /// <summary>Maps that support a specific game mode key (e.g. "2v2").</summary>
        public static MapEntry[] GetByMode(string modeKey)
        {
            if (string.IsNullOrEmpty(modeKey)) return GetAvailable();

            var all   = GetAll();
            int count = 0;
            foreach (var m in all)
                if (!m.isLocked && SupportsMode(m, modeKey)) count++;

            var result = new MapEntry[count];
            int i = 0;
            foreach (var m in all)
                if (!m.isLocked && SupportsMode(m, modeKey)) result[i++] = m;
            return result.Length > 0 ? result : GetAvailable();
        }

        /// <summary>Find entry by mapId. Returns false if not found.</summary>
        public static bool TryGetById(string mapId, out MapEntry entry)
        {
            foreach (var m in GetAll())
            {
                if (m.mapId == mapId)
                {
                    entry = m;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool SupportsMode(in MapEntry entry, string modeKey)
        {
            if (entry.supportedModes == null || entry.supportedModes.Length == 0)
                return true; // empty = all modes

            foreach (string m in entry.supportedModes)
                if (m == modeKey) return true;

            return false;
        }

        // ── Editor validation ──────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maps == null) return;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var m in maps)
            {
                if (string.IsNullOrWhiteSpace(m.mapId))
                    Debug.LogWarning("[MapConfig] A map entry has an empty mapId.");
                else if (!seen.Add(m.mapId))
                    Debug.LogWarning($"[MapConfig] Duplicate mapId: '{m.mapId}'");
            }
        }
#endif
    }
}
