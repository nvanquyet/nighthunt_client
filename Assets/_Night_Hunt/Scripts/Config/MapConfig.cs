using System;
using NightHunt.Data.DTOs;
using UnityEngine;

namespace NightHunt.Config
{
    // ══════════════════════════════════════════════════════════════════════════
    // MapEntry — per-map data
    // ══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public struct MapEntry
    {
        public string   mapId;
        public string   displayName;
        public string   description;
        public Sprite   icon;           // baked into build; always null when loaded from server
        public SceneId  sceneId;
        public string[] supportedModes;
        public bool     isLocked;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MapConfig — static class. Source of truth is the server DB.
    // Populated at startup by GameConfigService.FetchAsync() → LoadFromRemote().
    // No .asset file, no ScriptableObject, no Inspector data.
    //
    // ADDING A NEW MAP (no client rebuild needed):
    //   1. Add SceneId.GameMap_XX enum value in SceneConfig.cs (compile once)
    //   2. Add SceneEntry in SceneConfig.asset (Inspector)
    //   3. POST /api/admin/config/maps from dashboard
    //   → Client picks it up on next startup via LoadFromRemote()
    // ══════════════════════════════════════════════════════════════════════════

    public static class MapConfig
    {
        private static MapEntry[] _maps = Array.Empty<MapEntry>();

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
                    Debug.LogError($"[MapConfig] Error in OnConfigLoaded listener '{del.Method.DeclaringType}.{del.Method.Name}': {ex}");
                }
            }
        }

        /// <summary>
        /// Populate from server data (called by GameConfigService at startup).
        /// Converts server sceneName string → SceneId via SceneConfig reverse lookup.
        /// </summary>
        public static void LoadFromRemote(GameMapResponseDTO[] remoteMaps)
        {
            if (remoteMaps == null || remoteMaps.Length == 0)
            {
                Debug.LogWarning("[MapConfig] LoadFromRemote received empty list — no data loaded.");
                return;
            }

            var entries = new MapEntry[remoteMaps.Length];
            for (int i = 0; i < remoteMaps.Length; i++)
            {
                var dto = remoteMaps[i];

                SceneId sceneId = SceneId.GameMap_01;
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
                    icon           = null,
                    sceneId        = sceneId,
                    supportedModes = dto.supportedModes ?? Array.Empty<string>(),
                    isLocked       = dto.isLocked
                };
            }

            _maps = entries;
            SafeInvokeOnConfigLoaded();
        }

        /// <summary>All map entries. Empty until LoadFromRemote() is called.</summary>
        public static MapEntry[] GetAll() => _maps;

        /// <summary>Only maps that are not locked.</summary>
        public static MapEntry[] GetAvailable()
        {
            int count = 0;
            foreach (var m in _maps) if (!m.isLocked) count++;
            var result = new MapEntry[count];
            int i = 0;
            foreach (var m in _maps) if (!m.isLocked) result[i++] = m;
            return result;
        }

        /// <summary>Maps that support a specific game mode key. Falls back to GetAvailable() if none match.</summary>
        public static MapEntry[] GetByMode(string modeKey)
        {
            if (string.IsNullOrEmpty(modeKey)) return GetAvailable();
            int count = 0;
            foreach (var m in _maps)
                if (!m.isLocked && SupportsMode(m, modeKey)) count++;
            var result = new MapEntry[count];
            int i = 0;
            foreach (var m in _maps)
                if (!m.isLocked && SupportsMode(m, modeKey)) result[i++] = m;
            return result.Length > 0 ? result : GetAvailable();
        }

        /// <summary>Find entry by mapId. Returns false if not found.</summary>
        public static bool TryGetById(string mapId, out MapEntry entry)
        {
            foreach (var m in _maps)
            {
                if (m.mapId == mapId) { entry = m; return true; }
            }
            entry = default;
            return false;
        }

        private static bool SupportsMode(in MapEntry entry, string modeKey)
        {
            if (entry.supportedModes == null || entry.supportedModes.Length == 0)
                return true;
            foreach (string m in entry.supportedModes)
                if (m == modeKey) return true;
            return false;
        }
    }
}
