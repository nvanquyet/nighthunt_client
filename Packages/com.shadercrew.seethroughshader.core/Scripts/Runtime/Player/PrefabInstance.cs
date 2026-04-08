using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShaderCrew.SeeThroughShader
{
    [AddComponentMenu(Strings.COMPONENTMENU_PREFAB_INSTANCE)]
    public class PrefabInstance : MonoBehaviour
    {

        private PlayersPositionManager posManager;
        private PlayerToCameraRaycastTriggerManager plrRaycastTriggerMgr;
        // Start is called before the first frame update
        void Start()
        {
            InitializeSTSPlayerManagers();
            SceneManager.activeSceneChanged += ChangedActiveScene;
        }

        // Update is called once per frame
        void Update()
        {
        }

        private void OnEnable()
        {
            InitializeSTSPlayerManagers();
        }

        private void InitializeSTSPlayerManagers()
        {
            if (posManager == null)
            {
#if UNITY_2023_2_OR_NEWER
                posManager = FindFirstObjectByType<PlayersPositionManager>();
#else
                posManager = FindObjectOfType<PlayersPositionManager>();
#endif
            }

            if (plrRaycastTriggerMgr == null)
            {
#if UNITY_2023_2_OR_NEWER
                plrRaycastTriggerMgr = FindFirstObjectByType<PlayerToCameraRaycastTriggerManager>();
#else
                plrRaycastTriggerMgr = FindObjectOfType<PlayerToCameraRaycastTriggerManager>();
#endif
            }

            if (posManager != null)
            {
                //posManager.playableCharacters.Add(this.gameObject);
                //posManager.isInitialized = false;
                //posManager.init();
                posManager.AddPlayerAtRuntime(this.gameObject);
            }

            if (plrRaycastTriggerMgr != null)
            {
                plrRaycastTriggerMgr.AddPlayerAtRuntime(this.gameObject);
            }
        }

        private void ChangedActiveScene(Scene current, Scene next)
        {
            InitializeSTSPlayerManagers();
        }

        /*
        private void OnDisable()
        {
            if (posManager != null && posManager.playableCharacters.Contains(this.gameObject))
            {
                posManager.playableCharacters.Remove(this.gameObject);
                posManager.isInitialized = false;
                posManager.init();
            }
        }
        */
    }

}
