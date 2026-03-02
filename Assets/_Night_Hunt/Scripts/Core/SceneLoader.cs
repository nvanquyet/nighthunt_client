using UnityEngine;
using UnityEngine.SceneManagement;
using NightHunt.Config;

namespace NightHunt.Core
{
    /// <summary>
    /// SceneLoader - Quản lý scene transitions
    /// Flow: FirstLoading → Login/Home → Waiting → Game
    /// FirstLoading chứa GameManager và PersistentUICanvas (DontDestroyOnLoad)
    /// Scene names được load từ SceneConfig
    /// </summary>
    public static class SceneLoader
    {
        // Scene names (load từ SceneConfig)
        public static string SCENE_FIRST_LOADING  => SceneConfig.GetSceneName(SceneType.FirstLoading);
        public static string SCENE_LOGIN           => SceneConfig.GetSceneName(SceneType.Login);
        public static string SCENE_HOME            => SceneConfig.GetSceneName(SceneType.Home);
        public static string SCENE_WAITING         => SceneConfig.GetSceneName(SceneType.Waiting);
        public static string SCENE_CUSTOM_LOBBY    => SceneConfig.GetSceneName(SceneType.CustomLobby);
        public static string SCENE_MATCH_LOADING   => SceneConfig.GetSceneName(SceneType.MatchLoading);
        public static string SCENE_GAME            => SceneConfig.GetSceneName(SceneType.Game);

        // Track if GameManager đã được khởi tạo (từ FirstLoading scene)
        private static bool gameManagerInitialized = false;

        /// <summary>
        /// Đánh dấu GameManager đã được khởi tạo (gọi từ GameManager.Awake)
        /// </summary>
        public static void MarkGameManagerInitialized()
        {
            gameManagerInitialized = true;
        }

        /// <summary>
        /// Check if GameManager đã được khởi tạo
        /// </summary>
        public static bool IsGameManagerInitialized()
        {
            return gameManagerInitialized && GameManager.Instance != null;
        }

        /// <summary>
        /// Load FirstLoading scene (first scene - chứa GameManager và PersistentUICanvas)
        /// </summary>
        public static void LoadFirstLoading()
        {
            gameManagerInitialized = false; // Reset khi load lại FirstLoading
            SceneManager.LoadScene(SCENE_FIRST_LOADING);
        }

        /// <summary>
        /// Load Login scene
        /// Nếu GameManager chưa khởi tạo, load FirstLoading với target = Login
        /// Nếu đã khởi tạo, load trực tiếp Login
        /// </summary>
        public static void LoadLogin()
        {
            if (IsGameManagerInitialized())
            {
                SceneManager.LoadScene(SCENE_LOGIN);
            }
            else
            {
                LoadingManager.SetTargetScene(SCENE_LOGIN);
                SceneManager.LoadScene(SCENE_FIRST_LOADING);
            }
        }

        /// <summary>
        /// Load Home scene
        /// </summary>
        public static void LoadHome()
        {
            if (IsGameManagerInitialized())
            {
                SceneManager.LoadScene(SCENE_HOME);
            }
            else
            {
                LoadingManager.SetTargetScene(SCENE_HOME);
                SceneManager.LoadScene(SCENE_FIRST_LOADING);
            }
        }

        /// <summary>
        /// Load Waiting scene (lobby) — OBSOLETE: scene 04_Waiting removed.
        /// Use <see cref="LoadCustomLobby"/> instead.
        /// </summary>
        [System.Obsolete("Scene 04_Waiting has been removed from the flow. Use LoadCustomLobby() instead.", false)]
        public static void LoadWaiting()
        {
            if (IsGameManagerInitialized())
            {
                SceneManager.LoadScene(SCENE_WAITING);
            }
            else
            {
                LoadingManager.SetTargetScene(SCENE_WAITING);
                SceneManager.LoadScene(SCENE_FIRST_LOADING);
            }
        }

        /// <summary>Load Custom Lobby scene (friend / custom mode).</summary>
        public static void LoadCustomLobby()
        {
            if (IsGameManagerInitialized())
            {
                SceneManager.LoadScene(SCENE_CUSTOM_LOBBY);
            }
            else
            {
                LoadingManager.SetTargetScene(SCENE_CUSTOM_LOBBY);
                SceneManager.LoadScene(SCENE_FIRST_LOADING);
            }
        }

        /// <summary>Load Match Loading scene (shown while connecting to DS / Relay and spawning players).</summary>
        public static void LoadMatchLoading()
        {
            if (IsGameManagerInitialized())
            {
                SceneManager.LoadScene(SCENE_MATCH_LOADING);
            }
            else
            {
                LoadingManager.SetTargetScene(SCENE_MATCH_LOADING);
                SceneManager.LoadScene(SCENE_FIRST_LOADING);
            }
        }

        /// <summary>
        /// Load Game scene
        /// </summary>
        public static void LoadGame()
        {
            if (IsGameManagerInitialized())
            {
                SceneManager.LoadScene(SCENE_GAME);
            }
            else
            {
                LoadingManager.SetTargetScene(SCENE_GAME);
                SceneManager.LoadScene(SCENE_FIRST_LOADING);
            }
        }
    }
}

