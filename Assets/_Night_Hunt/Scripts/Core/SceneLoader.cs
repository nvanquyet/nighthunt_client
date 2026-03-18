using UnityEngine;
using UnityEngine.SceneManagement;
using NightHunt.Config;

namespace NightHunt.Core
{
    /// <summary>
    /// SceneLoader — Quản lý chuyển đổi scene thật sự (load/unload Unity scenes).
    ///
    /// Nguyên tắc rõ ràng:
    ///   • SceneLoader CHỈ dùng khi cần load scene thật:
    ///       - LoadHome()    : Trở về 01_Home từ gameplay scene
    ///       - LoadGame(id)  : Vào gameplay scene (GameMap_xx)
    ///   • Điều hướng UI bên trong 01_Home → dùng <see cref="UINavigator"/> trực tiếp
    ///
    /// Flow chính:
    ///   [01_Home] → UINavigator.ShowPanel(Lobby) ... match found
    ///            → MatchLoadingOverlay.Show()
    ///            → SceneLoader.LoadGame(GameMap_01)  ← SceneLoader được dùng ở đây
    ///   [GameMap] → match kết thúc → ResultsView
    ///            → SceneLoader.LoadHome()             ← và ở đây
    ///   [01_Home] → UINavigator.ShowPanel(Home)
    /// </summary>
    public static class SceneLoader
    {
        // ── Safe names via SceneConfig ─────────────────────────────────────────

        private static string HomeName    => SceneConfig.GetSceneName(SceneId.Home);

        // ── Core: Load scene thật ──────────────────────────────────────────────

        /// <summary>
        /// Load về 01_Home từ gameplay scene.
        /// Dùng khi match kết thúc (ResultsView) hoặc khi cần restart flow từ gameplay.
        /// </summary>
        public static void LoadHome()
        {
            Debug.Log("[SceneLoader] LoadHome → " + HomeName);
            SceneManager.LoadScene(HomeName);
        }

        /// <summary>
        /// Load gameplay scene theo SceneId.
        /// Gọi sau khi MatchLoadingOverlay xác nhận tất cả player đã ready.
        /// </summary>
        public static void LoadGame(SceneId mapId = SceneId.GameMap_01)
        {
            string sceneName = SceneConfig.GetSceneName(mapId);
            Debug.Log($"[SceneLoader] LoadGame → {mapId} ({sceneName})");
            SceneManager.LoadScene(sceneName);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>True nếu scene đang active là Home scene.</summary>
        public static bool IsInHomeScene =>
            SceneManager.GetActiveScene().name == HomeName;

        /// <summary>True nếu scene đang active là bất kỳ gameplay map nào.</summary>
        public static bool IsInGameplayScene => !IsInHomeScene;
    }
}
