using System.Collections.Generic;
using UnityEngine;
using NightHunt.Core; // ScriptableObjectSingleton<T>

namespace NightHunt.Config
{
    /// <summary>
    /// SceneConfig — Singleton ScriptableObject quản lý scene names.
    ///
    /// Kế thừa <see cref="ScriptableObjectSingleton{T}"/> — dùng base có sẵn,
    /// không tự implement singleton.
    ///
    /// Chỉ 2 loại scene thật trong game:
    ///   • Home      = "01_Home"         — Entry point, chứa toàn bộ UI non-gameplay
    ///   • GameMap_xx = "02_GameMap_01"… — Gameplay scenes (nhiều map)
    ///
    /// Để thêm map mới:
    ///   1. Thêm enum value vào <see cref="SceneId"/> (e.g. GameMap_03 = 102)
    ///   2. Thêm entry trong Inspector của SceneConfig.asset
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
            [Tooltip("Tên chính xác của scene file (không có .unity). " +
                     "Phải khớp với tên trong Build Settings.")]
            public string  sceneName;
        }

        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Scene Name Mapping")]
        [Tooltip("Mỗi entry map SceneId → tên scene thật.\n" +
                 "Thêm map mới: thêm enum value + thêm entry ở đây.")]
        [SerializeField]
        private SceneEntry[] scenes = new SceneEntry[]
        {
            new() { id = SceneId.Home,       sceneName = "01_Home"    },
            new() { id = SceneId.GameMap_01,  sceneName = "02_Map_01" },
            new() { id = SceneId.GameMap_02,  sceneName = "02_Map_02" },
        };

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Trả về tên scene theo SceneId.
        /// Fallback về Home scene name nếu không tìm thấy.
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

            Debug.LogWarning($"[SceneConfig] SceneId.{id} không có trong config → fallback Home.");
            return "01_Home"; // hardcoded fallback — tránh infinite recursion nếu Home không có trong scenes
        }

        /// <summary>Tên scene Home (shortcut hay dùng).</summary>
        public static string HomeSceneName => GetSceneName(SceneId.Home);

        /// <summary>
        /// Trả về tất cả SceneId là GameMap.
        /// Dùng cho UI chọn map hoặc matchmaking random map.
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
                    Debug.LogWarning($"[SceneConfig] SceneId.{entry.id} có sceneName rỗng!");
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
    ///   • GameMap bắt đầu từ 100 → không bao giờ conflict với Home
    ///   • Dùng explicit int values → thêm mới không bao giờ shift existing values
    ///   • Đặt tên theo chức năng, không theo tên file (file có thể rename tự do)
    /// </summary>
    public enum SceneId
    {
        // ── Non-gameplay ────────────────────────────────────────────────
        /// <summary>01_Home.unity — Entry point, chứa Login/Lobby/HUD panels.</summary>
        Home        = 0,

        // ── Gameplay Maps ───────────────────────────────────────────────
        /// <summary>Map 1 — tên file scene config trong Inspector.</summary>
        GameMap_01  = 100,
        /// <summary>Map 2.</summary>
        GameMap_02  = 101,
        // Thêm mới: GameMap_03 = 102, GameMap_04 = 103, ...
    }
}
