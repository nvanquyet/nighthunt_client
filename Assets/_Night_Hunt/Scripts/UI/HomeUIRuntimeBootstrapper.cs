using System.Collections;
using NightHunt.Audio;
using NightHunt.Core;
using NightHunt.State;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Runtime safety net for the 01_Home scene.
    /// It creates/wires non-destructive UI infrastructure that must exist before play test:
    /// profile modal, action router, and centralized UI audio triggers.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class HomeUIRuntimeBootstrapper : MonoBehaviour
    {
        private const string BootstrapName = "NightHunt Home UI Runtime Bootstrapper";
        private const string DisplayCameraName = "HomeDisplayCamera_Runtime";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForLoadedScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!IsHomeScene(scene))
                return;

            if (FindFirstObjectByType<HomeUIRuntimeBootstrapper>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject(BootstrapName);
            go.AddComponent<HomeUIRuntimeBootstrapper>();
        }

        private void Awake()
        {
            EnsureDisplayCamera();
        }

        private static bool IsHomeScene(Scene scene)
        {
            string name = scene.name ?? string.Empty;
            return name.Contains("01_Home") || name.Contains("Home");
        }

        private IEnumerator Start()
        {
            yield return null;
            EnsureDisplayCamera();
            EnsureActionRouter();
            EnsureBootIntroPanel();
            EnsurePlayerProfilePanel();
            EnsureButtonAudioTriggers();

            yield return new WaitForSecondsRealtime(0.25f);
            EnsureSceneEntryRoute();
        }

        private static void EnsureDisplayCamera()
        {
#if UNITY_SERVER
            return;
#else
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (camera != null &&
                    camera.enabled &&
                    camera.targetDisplay == 0 &&
                    camera.gameObject.activeInHierarchy)
                {
                    return;
                }
            }

            foreach (var camera in cameras)
            {
                if (camera == null || camera.targetDisplay != 0)
                    continue;

                ActivateHierarchy(camera.transform);
                camera.enabled = true;
                Debug.Log($"[HomeUIRuntimeBootstrapper] Re-enabled display camera '{camera.name}'.");
                return;
            }

            var go = new GameObject(DisplayCameraName);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.045f, 0.07f, 1f);
            cam.cullingMask = 0;
            cam.depth = -100f;
            cam.targetDisplay = 0;

            if (FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) == null)
                go.AddComponent<AudioListener>();

            Debug.Log("[HomeUIRuntimeBootstrapper] Created runtime display camera for Home scene.");
#endif
        }

        private static void ActivateHierarchy(Transform transform)
        {
            if (transform == null)
                return;

            var current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);
                current = current.parent;
            }
        }

        private static void EnsureActionRouter()
        {
            if (FindFirstObjectByType<HomeUIActionRouter>(FindObjectsInactive.Include) != null)
                return;

            var host = UINavigator.Instance != null
                ? UINavigator.Instance.gameObject
                : FindFirstObjectByType<Canvas>(FindObjectsInactive.Include)?.gameObject;

            if (host == null)
            {
                Debug.LogWarning("[HomeUIRuntimeBootstrapper] Cannot create HomeUIActionRouter: no UINavigator or Canvas found.");
                return;
            }

            host.AddComponent<HomeUIActionRouter>();
            Debug.Log("[HomeUIRuntimeBootstrapper] Added HomeUIActionRouter.");
        }

        private static void EnsureBootIntroPanel()
        {
            var persistentCanvas = PersistentUICanvas.Instance
                ?? FindFirstObjectByType<PersistentUICanvas>(FindObjectsInactive.Include)
                ?? PersistentUICanvas.GetOrCreate();

            if (persistentCanvas == null)
                return;

            if (persistentCanvas.BootIntroView != null)
                return;

            var view = persistentCanvas.EnsureBootIntroView();
            if (view != null)
                Debug.Log("[HomeUIRuntimeBootstrapper] Created persistent BootIntroView runtime panel.");
        }

        private static void EnsurePlayerProfilePanel()
        {
            var panel = PlayerProfilePanel.Instance
                ?? FindFirstObjectByType<PlayerProfilePanel>(FindObjectsInactive.Include);

            if (panel == null)
            {
                var canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
                if (canvas == null)
                {
                    Debug.LogWarning("[HomeUIRuntimeBootstrapper] Cannot create PlayerProfilePanel: no Canvas found.");
                    return;
                }

                var host = new GameObject("PlayerProfilePanel_RuntimeHost", typeof(RectTransform));
                host.layer = 5;
                host.transform.SetParent(canvas.transform, false);
                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                panel = host.AddComponent<PlayerProfilePanel>();
                Debug.Log("[HomeUIRuntimeBootstrapper] Created PlayerProfilePanel runtime host.");
            }

            panel.EnsureRuntimeWiring();
        }

        private static void EnsureButtonAudioTriggers()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int addedAudio = 0;
            int addedStandard = 0;
            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                if (button.GetComponent<UIAudioTrigger>() == null)
                {
                    button.gameObject.AddComponent<UIAudioTrigger>();
                    addedAudio++;
                }

                if (button.GetComponent<NH_Button>() == null)
                {
                    button.gameObject.AddComponent<NH_Button>();
                    addedStandard++;
                }
            }

            if (addedAudio > 0 || addedStandard > 0)
                Debug.Log($"[HomeUIRuntimeBootstrapper] Added UI helpers: audio={addedAudio}, standard={addedStandard}.");
        }

        private static void EnsureSceneEntryRoute()
        {
            var navigator = UINavigator.Instance;
            if (navigator == null || navigator.CurrentPanel != PanelType.None)
                return;

            var loading = PersistentUICanvas.Instance?.LoadingManager ?? LoadingManager.Instance;
            if (loading != null && (!loading.HasCompletedInitialFlow || loading.IsShowing()))
                return;

            var target = SessionState.Instance != null && SessionState.Instance.IsAuthenticated
                ? PanelType.Home
                : PanelType.Login;

            navigator.ShowPanel(target, "HomeSceneEntry");
        }
    }
}
