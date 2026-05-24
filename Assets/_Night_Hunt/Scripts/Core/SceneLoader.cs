using UnityEngine;
using UnityEngine.SceneManagement;
using NightHunt.Config;
using System.Collections;

namespace NightHunt.Core
{
    /// <summary>
    /// SceneLoader — Manages chuyển đổi scene thật sự (load/unload Unity scenes).
    ///
    /// Nguyên tắc rõ ràng:
    ///   • SceneLoader CHỈ dùng when needed load scene thật:
    ///       - LoadHome()    : Trở về 01_Home từ gameplay scene
    ///       - LoadGame(id)  : Vào gameplay scene (GameMap_xx)
    ///   • Điều hướng UI bên trong 01_Home → dùng <see cref="UINavigator"/> trực tiếp
    ///
    /// Async loading flow (Phase 1 fix — prevents overlay freeze):
    ///   LoadGame() starts LoadSceneAsync with allowSceneActivation=false.
    ///   Scene loads in the background without blocking the main thread, so
    ///   MatchLoadingOverlay coroutines (FadeIn, countdown) run normally.
    ///   After FadeIn completes, MatchLoadingOverlay calls ActivateLoadedScene()
    ///   which sets allowSceneActivation=true → scene transitions.
    ///   FishNet connects, players spawn, AllPlayersReadyEvent → overlay hides.
    /// </summary>
    public static class SceneLoader
    {
        // ── Safe names via SceneConfig ─────────────────────────────────────────

        private static string HomeName => SceneConfig.GetSceneName(SceneId.Home);

        // ── Async load state ───────────────────────────────────────────────────

        /// <summary>
        /// Pending async operation from LoadGame(). Held at 90% until
        /// ActivateLoadedScene() is called by MatchLoadingOverlay.
        /// </summary>
        private static AsyncOperation _pendingSceneOp;
        private static bool _returnHomeAfterPendingLoad;

        // ── Core: Load scene thật ──────────────────────────────────────────────

        /// <summary>
        /// Load về 01_Home từ gameplay scene.
        /// Uses khi match kết thúc (ResultsView) hoặc when needed restart flow từ gameplay.
        /// </summary>
        public static void LoadHome()
        {
            Debug.Log("[SceneLoader] LoadHome → " + HomeName);
            // Home loads synchronously — no overlay animation during this transition.
            _pendingSceneOp = null;
            _returnHomeAfterPendingLoad = false;
            SceneManager.LoadScene(HomeName);
        }

        /// <summary>
        /// Starts async background load of the gameplay scene.
        /// Holds at 90% (allowSceneActivation=false) so MatchLoadingOverlay can
        /// display its FadeIn animation before the scene transitions.
        /// Call <see cref="ActivateLoadedScene"/> from MatchLoadingOverlay to proceed.
        /// </summary>
        public static void LoadGame(SceneId mapId = SceneId.GameMap_01)
        {
            string sceneName = SceneConfig.GetSceneName(mapId);
            Debug.Log($"[SceneLoader] LoadGame (async) → {mapId} ({sceneName})");
            _returnHomeAfterPendingLoad = false;
            _pendingSceneOp = SceneManager.LoadSceneAsync(sceneName);
            if (_pendingSceneOp == null)
            {
                Debug.LogError($"[SceneLoader] LoadGame FAILED — scene '{sceneName}' (SceneId.{mapId}) is not in the active build profile. " +
                               "Add it via File → Build Profiles, then rebuild.");
                return;
            }
            _pendingSceneOp.allowSceneActivation = false;
        }

        /// <summary>
        /// Allow the scene started by LoadGame() to activate.
        /// Called by MatchLoadingOverlay after its FadeIn animation completes,
        /// ensuring the overlay is fully visible before the scene switches.
        /// Safe to call even if no pending op exists (no-op).
        /// </summary>
        public static void ActivateLoadedScene()
        {
            if (_pendingSceneOp == null)
            {
                Debug.LogWarning("[SceneLoader] ActivateLoadedScene: no pending async op — LoadGame() may not have been called, or was already activated.");
                return;
            }
            Debug.Log("[SceneLoader] ActivateLoadedScene — allowing scene activation.");
            _pendingSceneOp.allowSceneActivation = true;
            _pendingSceneOp = null;
        }

        /// <summary>
        /// Handles cancellation/failure while a gameplay scene is already loading or active.
        /// Unity cannot truly cancel LoadSceneAsync at 90%, so we activate the pending scene
        /// and immediately load Home once activation completes.
        /// </summary>
        public static void ReturnHomeFromGameplayFlow()
        {
            if (_returnHomeAfterPendingLoad)
                return;

            if (_pendingSceneOp == null)
            {
                LoadHome();
                return;
            }

            Debug.LogWarning("[SceneLoader] Returning Home while game scene is pending — activating pending load first.");
            _returnHomeAfterPendingLoad = true;

            var op = _pendingSceneOp;
            op.allowSceneActivation = true;
            _pendingSceneOp = null;
            SceneLoaderHost.Instance.StartCoroutine(ReturnHomeAfterPendingLoad(op));
        }

        private static IEnumerator ReturnHomeAfterPendingLoad(AsyncOperation op)
        {
            while (op != null && !op.isDone)
                yield return null;

            _returnHomeAfterPendingLoad = false;
            SceneManager.LoadScene(HomeName);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>True nếu scene đang active là Home scene.</summary>
        public static bool IsInHomeScene =>
            SceneManager.GetActiveScene().name == HomeName;

        /// <summary>True nếu scene đang active là bất kỳ gameplay map nào.</summary>
        public static bool IsInGameplayScene => !IsInHomeScene;

        /// <summary>True while LoadGame() is waiting for overlay fade-in activation.</summary>
        public static bool HasPendingSceneLoad => _pendingSceneOp != null;
    }
}
