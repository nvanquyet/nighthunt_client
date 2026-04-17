using System.Collections.Generic;
using UnityEngine;
using NightHunt.Core; // ScriptableObjectSingleton<T>

namespace NightHunt.Config
{
    /// <summary>
    /// SceneConfig — Singleton ScriptableObject manage scene names.
    ///
    /// Kế thừa <see cref="ScriptableObjectSingleton{T}"/> — dùng base có sẵn,
    /// không tự implement singleton.
    ///
    /// Chỉ 2 loại scene thật trong game:
    ///   • Home      = "01_Home"         — Entry point, chứa toàn bộ UI non-gameplay
    ///   • GameMap_xx = "02_GameMap_01"… — Gameplay scenes (nhiều map)
    ///
    /// Để thêm map mới:
    ///   1. Add enum value vào <see cref="SceneId"/> (e.g. GameMap_03 = 102)
    ///   2. Add entry trong Inspector của SceneConfig.asset
    ///   → Không cần sửa code ở bất kỳ đâu khác.
    ///
    /// SETUP: Đặt SceneConfig.asset trong thư mục Resources/ và
    /// right-click → "Cache Resources Path" (một lần mỗi Editor session).
    /// </summary>
    [CreateAssetMenu(fileName = "SceneConfig", menuName = "NightHunt/Config/Scene Config")]
    public class SceneConfig : ScriptableObjectSingleton<SceneConfig>
    {
        // ── Entry type ─────────────────────────────────────────────────────────

        [System.Serializable]
        public struct SceneEntry
        {
            public SceneId id;
            [Tooltip("Tên chính xác của scene file (not available .unity). " +
                     "Phải khớp với tên trong Build Settings.")]
            public string  sceneName;
        }

        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Scene Name Mapping")]
        [Tooltip("Mỗi entry map SceneId → tên scene thật.\n" +
                 "Add map mới: thêm enum value + thêm entry ở đây.")]
        [SerializeField]
        private SceneEntry[] scenes = new SceneEntry[]
        {
            new() { id = SceneId.Home,       sceneName = "01_Home"    },
            new() { id = SceneId.GameMap_01, sceneName = "02_Map_01"  },
            new() { id = SceneId.GameMap_02, sceneName = "02_Map_02"  },
            // Pre-reserved slots. Scene files must be created + added to Build Settings
            // before enabling the corresponding map entry in the admin dashboard.
            new() { id = SceneId.GameMap_03, sceneName = "02_Map_03"  },
            new() { id = SceneId.GameMap_04, sceneName = "02_Map_04"  },
            new() { id = SceneId.GameMap_05, sceneName = "02_Map_05"  },
            new() { id = SceneId.GameMap_06, sceneName = "02_Map_06"  },
            new() { id = SceneId.GameMap_07, sceneName = "02_Map_07"  },
            new() { id = SceneId.GameMap_08, sceneName = "02_Map_08"  },
            new() { id = SceneId.GameMap_09, sceneName = "02_Map_09"  },
            new() { id = SceneId.GameMap_10, sceneName = "02_Map_10"  },
        };

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Trả về tên scene theo SceneId.
        /// Fallback về Home scene name nếu not found.
        /// </summary>
        public static string GetSceneName(SceneId id)
        {
            if (Instance == null)
            {
                Debug.LogError("[SceneConfig] Instance not found! " +
                               "Đặt SceneConfig.asset trong Resources/ folder.");
                return "01_Home";
            }

            foreach (var entry in Instance.scenes)
                if (entry.id == id)
                    return entry.sceneName;

            Debug.LogWarning($"[SceneConfig] SceneId.{id} not available trong config → fallback Home.");
            return "01_Home"; // hardcoded fallback — tránh infinite recursion nếu Home not available trong scenes
        }

        /// <summary>Tên scene Home (shortcut hay dùng).</summary>
        public static string HomeSceneName => GetSceneName(SceneId.Home);

        /// <summary>
        /// Reverse lookup: find SceneId by Unity scene file name (e.g. "02_Map_01" → SceneId.GameMap_01).
        /// Returns false if no matching entry — caller should fallback.
        /// </summary>
        public static bool TryGetSceneIdByName(string sceneName, out SceneId id)
        {
            id = SceneId.Home;
            if (Instance == null) return false;
            foreach (var entry in Instance.scenes)
                if (entry.sceneName == sceneName)
                { id = entry.id; return true; }
            return false;
        }

        /// <summary>
        /// Trả về tất cả SceneId là GameMap.
        /// Uses cho UI chọn map hoặc matchmaking random map.
        /// </summary>
        public static SceneId[] GetAllGameMapIds()
        {
            if (Instance == null) return System.Array.Empty<SceneId>();

            var result = new List<SceneId>();
            foreach (var entry in Instance.scenes)
                if (entry.id != SceneId.Home)
                    result.Add(entry.id);
            return result.ToArray();
        }

        // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<SceneId>();
            foreach (var entry in scenes)
            {
                if (!seen.Add(entry.id))
                    Debug.LogWarning($"[SceneConfig] Duplicate SceneId.{entry.id} " +
                                     "trong scenes array!");
                if (string.IsNullOrWhiteSpace(entry.sceneName))
                    Debug.LogWarning($"[SceneConfig] SceneId.{entry.id} có sceneName empty!");
            }
        }
#endif
    }

    // ── SceneId Enum ───────────────────────────────────────────────────────────

    /// <summary>
    /// Định danh cho từng scene/map.
    ///
    /// Quy tắc:
    ///   • Home = 0  (luôn cố định)
    ///   • GameMap start từ 100 → never conflict với Home
    ///   • Uses explicit int values → thêm mới never shift existing values
    ///   • Đặt tên theo chức năng, không theo tên file (file có thể rename tự do)
    /// </summary>
    public enum SceneId
    {
        // ── Non-gameplay ────────────────────────────────────────────────
        /// <summary>01_Home.unity — Entry point, chứa Login/Lobby/HUD panels.</summary>
        Home        = 0,

        // ── Gameplay Maps ───────────────────────────────────────────────
        /// <summary>Map 1 — Industrial Zone.</summary>
        GameMap_01  = 100,
        /// <summary>Map 2 — Arctic Base.</summary>
        GameMap_02  = 101,
        // Pre-reserved slots — scenes baked into every build.
        // Enable new maps at runtime via backend admin panel
        // without releasing a client update.
        // Just POST /api/admin/config/maps with sceneName = "GameMap_0X".
        /// <summary>Map 3 — pre-reserved (scene must be in Build Settings).</summary>
        GameMap_03  = 102,
        /// <summary>Map 4 — pre-reserved.</summary>
        GameMap_04  = 103,
        /// <summary>Map 5 — pre-reserved.</summary>
        GameMap_05  = 104,
        /// <summary>Map 6 — pre-reserved.</summary>
        GameMap_06  = 105,
        /// <summary>Map 7 — pre-reserved.</summary>
        GameMap_07  = 106,
        /// <summary>Map 8 — pre-reserved.</summary>
        GameMap_08  = 107,
        /// <summary>Map 9 — pre-reserved.</summary>
        GameMap_09  = 108,
        /// <summary>Map 10 — pre-reserved.</summary>
        GameMap_10  = 109,
    }
}
