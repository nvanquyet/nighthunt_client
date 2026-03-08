using UnityEngine;

namespace NightHunt.Config
{
    /// <summary>
    /// SceneConfig - Configuration cho scene names
    /// Tạo ScriptableObject instance trong Unity Editor để config scene names
    /// </summary>
    [CreateAssetMenu(fileName = "SceneConfig", menuName = "NightHunt/Config/Scene Config")]
    public class SceneConfig : ScriptableObject
    {
        [Header("Scene Names")]
        [Tooltip("Scene name: 01_FirstLoading")]
        public string firstLoadingScene = "01_FirstLoading";
        
        [Tooltip("Scene name: 02_Login")]
        public string loginScene = "02_Login";
        
        [Tooltip("Scene name: 03_Home")]
        public string homeScene = "03_Home";
        
        [Tooltip("Scene name: 04_Waiting")]
        public string waitingScene = "04_Waiting";

        [Tooltip("Scene name: 05_CustomLobby")]
        public string customLobbyScene = "05_CustomLobby";

        [Tooltip("Scene name: 06_MatchLoading")]
        public string matchLoadingScene = "06_MatchLoading";
        
        [Tooltip("Scene name: 07_Game")]
        public string gameScene = "07_Game";
        
        // Singleton instance (set trong Unity Editor)
        private static SceneConfig instance;
        
        /// <summary>
        /// Get SceneConfig instance (load từ Resources hoặc tìm trong project)
        /// </summary>
        public static SceneConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to load from Resources first
                    instance = Resources.Load<SceneConfig>("SceneConfig");
                    
                    // If not found, try to find in project
                    if (instance == null)
                    {
                        #if UNITY_EDITOR
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:SceneConfig");
                        if (guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            instance = UnityEditor.AssetDatabase.LoadAssetAtPath<SceneConfig>(path);
                        }
                        #endif
                    }
                    
                    // Fallback to default values if still null
                    if (instance == null)
                    {
                        Debug.LogWarning("SceneConfig not found! Using default scene names. Please create SceneConfig asset.");
                        instance = CreateInstance<SceneConfig>();
                    }
                }
                return instance;
            }
        }
        
        /// <summary>
        /// Get scene name by type
        /// </summary>
        public static string GetSceneName(SceneType sceneType)
        {
            return sceneType switch
            {
                SceneType.FirstLoading  => Instance.firstLoadingScene,
                SceneType.Login         => Instance.loginScene,
                SceneType.Home          => Instance.homeScene,
                SceneType.Waiting       => Instance.waitingScene,
                SceneType.CustomLobby   => Instance.customLobbyScene,
                SceneType.MatchLoading  => Instance.matchLoadingScene,
                SceneType.Game          => Instance.gameScene,
                _                       => Instance.loginScene
            };
        }
    }
    
    /// <summary>
    /// Scene type enum
    /// </summary>
    public enum SceneType
    {
        FirstLoading,
        Login,
        Home,
        Waiting,
        CustomLobby,   // Custom / friend mode lobby
        MatchLoading,  // Pre-game loading screen (team info + progress)
        Game
    }
}

