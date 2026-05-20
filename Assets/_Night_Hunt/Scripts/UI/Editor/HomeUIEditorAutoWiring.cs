#if UNITY_EDITOR
using NightHunt.Audio;
using NightHunt.Core;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NightHunt.UI.Editor
{
    /// <summary>
    /// Non-interactive scene repair pass for the Home UI.
    /// Runs after script reload / scene open so missing Inspector wiring is fixed
    /// before Play, not only by runtime fallbacks.
    /// </summary>
    [InitializeOnLoad]
    public static class HomeUIEditorAutoWiring
    {
        private const string HomeScenePath = "Assets/_Night_Hunt/Scenes/01_Home.unity";
        private const string HomeScenePathSuffix = HomeScenePath;
        private const string BootIntroHostName = "BootIntroPanel_AutoHost";
        private const string BootIntroAssetFolder = "Assets/_Night_Hunt/Animations/UI";
        private const string BootIntroControllerPath = BootIntroAssetFolder + "/BootIntro.controller";
        private const string BootIntroInClipPath = BootIntroAssetFolder + "/BootIntro_In.anim";
        private const string BootIntroOutClipPath = BootIntroAssetFolder + "/BootIntro_Out.anim";
        private const string ProfileHostName = "PlayerProfilePanel_AutoHost";
        private static bool _scheduled;

        static HomeUIEditorAutoWiring()
        {
            EditorSceneManager.sceneOpened += (_, _) => Schedule();
            EditorApplication.delayCall += Schedule;
        }

        [MenuItem("Tools/NightHunt/Auto Wire Loaded Home UI")]
        public static void WireLoadedHomeUIFromMenu()
        {
            int changed = WireLoadedHomeUI();
            EditorUtility.DisplayDialog(
                "Home UI Auto Wiring",
                changed == 0
                    ? "No missing Home UI wiring was found."
                    : $"Applied {changed} Home UI wiring fix(es). Save the scene if prompted.",
                "OK");
        }

        private static void Schedule()
        {
            if (_scheduled)
                return;

            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                WireLoadedHomeUI();
            };
        }

        public static int WireLoadedHomeUI()
        {
            var scene = SceneManager.GetActiveScene();
            if (!IsHomeScene(scene))
                return 0;

            int changed = 0;
            changed += RemoveMissingScripts();
            changed += ClearLegacyLoginSuccessEvents();
            changed += EnsureActionRouter();
            changed += EnsureBootIntroPanel();
            changed += EnsurePlayerProfilePanel();
            changed += EnsureFriendPanelRouting();
            changed += EnsureButtonAudioTriggers();

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[HomeUIEditorAutoWiring] Applied {changed} Home UI wiring fix(es) in '{scene.name}'.");
            }

            return changed;
        }

        public static void WireHomeSceneForBatch()
        {
            var scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
            int changed = WireLoadedHomeUI();
            if (changed > 0)
                EditorSceneManager.SaveScene(scene);

            Debug.Log($"[HomeUIEditorAutoWiring] Batch wiring complete. changed={changed}");
        }

        private static bool IsHomeScene(Scene scene)
        {
            if (!scene.IsValid())
                return false;

            string path = (scene.path ?? string.Empty).Replace('\\', '/');
            return path.EndsWith(HomeScenePathSuffix, System.StringComparison.OrdinalIgnoreCase)
                || scene.name.Contains("01_Home")
                || scene.name.Contains("Home");
        }

        private static int EnsureActionRouter()
        {
            var existing = Object.FindFirstObjectByType<HomeUIActionRouter>(FindObjectsInactive.Include);
            if (existing != null)
                return WireRouterReferences(existing);

            var navigator = Object.FindFirstObjectByType<UINavigator>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var host = navigator != null ? navigator.gameObject : canvas != null ? canvas.gameObject : null;
            if (host == null)
                return 0;

            var router = Undo.AddComponent<HomeUIActionRouter>(host);
            WireRouterReferences(router);
            EditorUtility.SetDirty(host);
            return 1;
        }

        private static int WireRouterReferences(HomeUIActionRouter router)
        {
            if (router == null)
                return 0;

            int changed = 0;
            var so = new SerializedObject(router);
            changed += SetObjectReferenceIfMissing(so, "navigator",
                Object.FindFirstObjectByType<UINavigator>(FindObjectsInactive.Include));
            changed += SetObjectReferenceIfMissing(so, "friendPanelView",
                Object.FindFirstObjectByType<FriendPanelView>(FindObjectsInactive.Include));
            if (changed > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(router);
            }

            return changed;
        }

        private static int SetObjectReferenceIfMissing(SerializedObject so, string propertyName, Object value)
        {
            var property = so.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null || value == null)
                return 0;

            property.objectReferenceValue = value;
            return 1;
        }

        private static int ClearLegacyLoginSuccessEvents()
        {
            int changed = 0;
            var loginViews = Object.FindObjectsByType<LoginView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var loginView in loginViews)
            {
                if (loginView == null)
                    continue;

                var so = new SerializedObject(loginView);
                bool viewChanged = false;

                var invokeLegacy = so.FindProperty("invokeLegacyLoginSuccessEvents");
                if (invokeLegacy != null && invokeLegacy.boolValue)
                {
                    invokeLegacy.boolValue = false;
                    viewChanged = true;
                }

                var calls = so.FindProperty("onLoginSuccess.m_PersistentCalls.m_Calls");
                if (calls != null && calls.arraySize > 0)
                {
                    calls.ClearArray();
                    viewChanged = true;
                }

                var registerCalls = so.FindProperty("onRegisterSuccess.m_PersistentCalls.m_Calls");
                Animator registerAnimator = null;
                string registerState = null;
                if (registerCalls != null)
                {
                    for (int i = 0; i < registerCalls.arraySize; i++)
                    {
                        var call = registerCalls.GetArrayElementAtIndex(i);
                        if (call.FindPropertyRelative("m_MethodName")?.stringValue != "Play")
                            continue;

                        if (call.FindPropertyRelative("m_Target")?.objectReferenceValue is Animator animator)
                            registerAnimator = animator;

                        string state = call.FindPropertyRelative("m_Arguments.m_StringArgument")?.stringValue;
                        if (!string.IsNullOrWhiteSpace(state))
                            registerState = state;
                    }
                }

                var authAnimator = so.FindProperty("authFlowAnimator");
                var splashAnimator = registerAnimator != null ? registerAnimator : FindSplashAnimator();
                if (authAnimator != null && authAnimator.objectReferenceValue == null && splashAnimator != null)
                {
                    authAnimator.objectReferenceValue = splashAnimator;
                    viewChanged = true;
                }

                var registerSuccessState = so.FindProperty("registerSuccessAnimatorState");
                if (registerSuccessState != null && !string.IsNullOrWhiteSpace(registerState) &&
                    registerSuccessState.stringValue != registerState)
                {
                    registerSuccessState.stringValue = registerState;
                    viewChanged = true;
                }

                if (registerCalls != null && registerCalls.arraySize > 0)
                {
                    registerCalls.ClearArray();
                    viewChanged = true;
                }

                if (!viewChanged)
                    continue;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(loginView);
                changed++;
            }

            return changed;
        }

        private static Animator FindSplashAnimator()
        {
            var splash = GameObject.Find("Splash Screen");
            if (splash != null && splash.TryGetComponent<Animator>(out var animator))
                return animator;

            return Object.FindFirstObjectByType<LoginView>(FindObjectsInactive.Include)?.GetComponent<Animator>();
        }

        private static int EnsureFriendPanelRouting()
        {
            var router = Object.FindFirstObjectByType<HomeUIActionRouter>(FindObjectsInactive.Include);
            var friendPanel = Object.FindFirstObjectByType<FriendPanelView>(FindObjectsInactive.Include);
            if (router == null || friendPanel == null)
                return 0;

            int changed = 0;
            changed += ClearFriendPanelOpenUnityEvent(friendPanel);

            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                int removedOpen = RemoveButtonCalls(button, "Michsky.UI.Shift.FriendsPanelManager", "WindowIn", true);
                if (removedOpen > 0)
                {
                    AddRouterButtonCall(button, router, nameof(HomeUIActionRouter.OpenFriendsPanel));
                    changed += removedOpen;
                }

            }

            return changed;
        }

        private static int ClearFriendPanelOpenUnityEvent(FriendPanelView friendPanel)
        {
            var so = new SerializedObject(friendPanel);
            var calls = so.FindProperty("onOpenRequested.m_PersistentCalls.m_Calls");
            if (calls == null || calls.arraySize == 0)
                return 0;

            calls.ClearArray();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(friendPanel);
            return 1;
        }

        private static int RemoveButtonCalls(Button button, string targetTypeFragment, string methodName, bool requireEmptyStringArgument)
        {
            var so = new SerializedObject(button);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null || calls.arraySize == 0)
                return 0;

            int removed = 0;
            for (int i = calls.arraySize - 1; i >= 0; i--)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (!PersistentCallMatches(call, targetTypeFragment, methodName, requireEmptyStringArgument))
                    continue;

                calls.DeleteArrayElementAtIndex(i);
                removed++;
            }

            if (removed == 0)
                return 0;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(button);
            return removed;
        }

        private static bool PersistentCallMatches(SerializedProperty call, string targetTypeFragment, string methodName, bool requireEmptyStringArgument)
        {
            string method = call.FindPropertyRelative("m_MethodName")?.stringValue;
            if (method != methodName)
                return false;

            string stringArgument = call.FindPropertyRelative("m_Arguments.m_StringArgument")?.stringValue;
            if (requireEmptyStringArgument && !string.IsNullOrEmpty(stringArgument))
                return false;

            var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
            string runtimeType = target != null ? target.GetType().FullName : string.Empty;
            string serializedType = call.FindPropertyRelative("m_TargetAssemblyTypeName")?.stringValue ?? string.Empty;
            return runtimeType.Contains(targetTypeFragment) || serializedType.Contains(targetTypeFragment);
        }

        private static void AddRouterButtonCall(Button button, HomeUIActionRouter router, string methodName)
        {
            if (HasButtonCall(button, router, methodName))
                return;

            if (methodName == nameof(HomeUIActionRouter.OpenFriendsPanel))
                UnityEventTools.AddPersistentListener(button.onClick, router.OpenFriendsPanel);
            else if (methodName == nameof(HomeUIActionRouter.CloseFriendsPanel))
                UnityEventTools.AddPersistentListener(button.onClick, router.CloseFriendsPanel);

            EditorUtility.SetDirty(button);
        }

        private static bool HasButtonCall(Button button, Object target, string methodName)
        {
            var so = new SerializedObject(button);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null)
                return false;

            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target")?.objectReferenceValue == target &&
                    call.FindPropertyRelative("m_MethodName")?.stringValue == methodName)
                {
                    return true;
                }
            }

            return false;
        }

        private static int RemoveMissingScripts()
        {
            var scene = SceneManager.GetActiveScene();
            int removed = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = transform.gameObject;
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (count <= 0)
                        continue;

                    Undo.RegisterCompleteObjectUndo(go, "Remove missing script component");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    EditorUtility.SetDirty(go);
                    removed += count;
                }
            }

            return removed;
        }

        private static int EnsurePlayerProfilePanel()
        {
            var panel = Object.FindFirstObjectByType<PlayerProfilePanel>(FindObjectsInactive.Include);
            int changed = 0;

            if (panel == null)
            {
                var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
                if (canvas == null)
                    return 0;

                var host = new GameObject(ProfileHostName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(host, "Create PlayerProfilePanel host");
                host.layer = 5;
                host.transform.SetParent(canvas.transform, false);
                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                panel = Undo.AddComponent<PlayerProfilePanel>(host);
                changed++;
            }

            if (!IsProfilePanelComplete(panel))
            {
                Undo.RecordObject(panel, "Wire PlayerProfilePanel");
                panel.EnsureRuntimeWiring();
                EditorUtility.SetDirty(panel);
                MarkProfileObjectsDirty(panel);
                changed++;
            }

            return changed;
        }

        private static int EnsureBootIntroPanel()
        {
            var persistentCanvas = EnsurePersistentUICanvas();
            var manager = Object.FindFirstObjectByType<LoadingManager>(FindObjectsInactive.Include);
            if (persistentCanvas == null)
                return 0;

            int changed = 0;
            var persistentRoot = persistentCanvas.transform.root;
            var view = persistentCanvas.GetComponentInChildren<BootIntroView>(true);
            if (view == null)
            {
                var canvas = persistentCanvas.Canvas != null
                    ? persistentCanvas.Canvas
                    : persistentCanvas.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                    return 0;

                view = FindSceneScopedBootIntroView(persistentRoot);
                if (view != null)
                {
                    Undo.SetTransformParent(view.transform, canvas.transform, "Move BootIntroView to PersistentUICanvas");
                    StretchFull(view.GetComponent<RectTransform>());
                    view.EnsureRuntimeWiring();
                    var animator = view.GetComponent<Animator>() ?? Undo.AddComponent<Animator>(view.gameObject);
                    animator.runtimeAnimatorController = EnsureBootIntroAnimatorController();
                    var viewSo = new SerializedObject(view);
                    SetObjectReferenceIfMissing(viewSo, "root", view.gameObject);
                    SetObjectReferenceIfMissing(viewSo, "animator", animator);
                    viewSo.ApplyModifiedProperties();
                    view.gameObject.SetActive(false);
                    EditorUtility.SetDirty(view);
                    changed++;
                }
                else
                {
                    var host = CreateEditorUIObject(BootIntroHostName, canvas.transform);
                    StretchFull(host.GetComponent<RectTransform>());

                    var canvasGroup = Undo.AddComponent<CanvasGroup>(host);
                    canvasGroup.alpha = 0f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;

                    var background = Undo.AddComponent<Image>(host);
                    background.color = new Color(0.015f, 0.018f, 0.024f, 1f);
                    background.raycastTarget = false;

                    var animator = Undo.AddComponent<Animator>(host);
                    animator.runtimeAnimatorController = EnsureBootIntroAnimatorController();

                    var title = CreateEditorText("Title", host.transform, "NIGHT HUNT", 46f, FontStyles.Bold, new Vector2(0f, 22f));
                    var subtitle = CreateEditorText("Subtitle", host.transform, "INITIALIZING", 16f, FontStyles.UpperCase, new Vector2(0f, -34f));

                    view = Undo.AddComponent<BootIntroView>(host);
                    var viewSo = new SerializedObject(view);
                    SetObjectReferenceIfMissing(viewSo, "root", host);
                    SetObjectReferenceIfMissing(viewSo, "canvasGroup", canvasGroup);
                    SetObjectReferenceIfMissing(viewSo, "animator", animator);
                    SetObjectReferenceIfMissing(viewSo, "backgroundImage", background);
                    SetObjectReferenceIfMissing(viewSo, "titleText", title);
                    SetObjectReferenceIfMissing(viewSo, "subtitleText", subtitle);
                    viewSo.ApplyModifiedProperties();

                    host.SetActive(false);
                    EditorUtility.SetDirty(host);
                    EditorUtility.SetDirty(view);
                    changed++;
                }
            }
            else
            {
                var viewSo = new SerializedObject(view);
                bool missingRoot = viewSo.FindProperty("root")?.objectReferenceValue == null;
                bool missingCanvas = viewSo.FindProperty("canvasGroup")?.objectReferenceValue == null;
                bool missingAnimator = viewSo.FindProperty("animator")?.objectReferenceValue == null;
                bool missingTitle = viewSo.FindProperty("titleText")?.objectReferenceValue == null;
                if (missingRoot || missingCanvas || missingAnimator || missingTitle ||
                    view.GetComponent<Animator>()?.runtimeAnimatorController == null)
                {
                    Undo.RecordObject(view, "Wire BootIntroView");
                    view.EnsureRuntimeWiring();
                    var animator = view.GetComponent<Animator>() ?? Undo.AddComponent<Animator>(view.gameObject);
                    animator.runtimeAnimatorController = EnsureBootIntroAnimatorController();

                    viewSo.Update();
                    SetObjectReferenceIfMissing(viewSo, "animator", animator);
                    viewSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(view);
                    changed++;
                }
            }

            changed += WirePersistentBootIntroReference(persistentCanvas, view);

            if (manager != null)
            {
                if (manager.transform.root != persistentRoot)
                {
                    Debug.LogWarning(
                        $"[HomeUIEditorAutoWiring] LoadingManager '{manager.name}' is not under PersistentUICanvas. " +
                        "BootIntroView was still wired to persistent canvas.");
                }

                var managerSo = new SerializedObject(manager);
                changed += SetObjectReferenceIfMissing(managerSo, "bootIntroView", view);
                managerSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(manager);
            }

            return changed;
        }

        private static BootIntroView FindSceneScopedBootIntroView(Transform persistentRoot)
        {
            foreach (var candidate in Object.FindObjectsByType<BootIntroView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null)
                    continue;

                if (persistentRoot == null || candidate.transform.root != persistentRoot)
                    return candidate;
            }

            return null;
        }

        private static PersistentUICanvas EnsurePersistentUICanvas()
        {
            var persistentCanvas = Object.FindFirstObjectByType<PersistentUICanvas>(FindObjectsInactive.Include);
            var host = persistentCanvas != null
                ? persistentCanvas.gameObject
                : GameObject.Find("CanvasDontDestroy") ?? GameObject.Find("PersistentUICanvas");
            bool createdHost = false;
            if (host == null)
            {
                host = new GameObject("CanvasDontDestroy", typeof(RectTransform));
                createdHost = true;
            }

            if (createdHost)
                Undo.RegisterCreatedObjectUndo(host, "Create PersistentUICanvas");
            var canvas = host.GetComponent<Canvas>() ?? Undo.AddComponent<Canvas>(host);
            var scaler = host.GetComponent<CanvasScaler>() ?? Undo.AddComponent<CanvasScaler>(host);
            var raycaster = host.GetComponent<GraphicRaycaster>() ?? Undo.AddComponent<GraphicRaycaster>(host);
            persistentCanvas = persistentCanvas != null
                ? persistentCanvas
                : host.GetComponent<PersistentUICanvas>() ?? Undo.AddComponent<PersistentUICanvas>(host);

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var so = new SerializedObject(persistentCanvas);
            SetObjectReferenceIfMissing(so, "canvas", canvas);
            SetObjectReferenceIfMissing(so, "canvasScaler", scaler);
            SetObjectReferenceIfMissing(so, "graphicRaycaster", raycaster);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(host);
            return persistentCanvas;
        }

        private static int WirePersistentBootIntroReference(PersistentUICanvas persistentCanvas, BootIntroView view)
        {
            if (persistentCanvas == null || view == null)
                return 0;

            var so = new SerializedObject(persistentCanvas);
            int changed = SetObjectReferenceIfMissing(so, "bootIntroView", view);
            if (changed > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(persistentCanvas);
            }

            return changed;
        }

        private static RuntimeAnimatorController EnsureBootIntroAnimatorController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BootIntroControllerPath);
            if (controller != null)
                return controller;

            EnsureAssetFolder(BootIntroAssetFolder);

            var introIn = AssetDatabase.LoadAssetAtPath<AnimationClip>(BootIntroInClipPath);
            if (introIn == null)
            {
                introIn = CreateAlphaClip("BootIntro_In", 0f, 1f, 0.25f);
                AssetDatabase.CreateAsset(introIn, BootIntroInClipPath);
            }

            var introOut = AssetDatabase.LoadAssetAtPath<AnimationClip>(BootIntroOutClipPath);
            if (introOut == null)
            {
                introOut = CreateAlphaClip("BootIntro_Out", 1f, 0f, 0.35f);
                AssetDatabase.CreateAsset(introOut, BootIntroOutClipPath);
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(BootIntroControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var introInState = stateMachine.AddState("Intro In");
            introInState.motion = introIn;
            stateMachine.defaultState = introInState;

            var introOutState = stateMachine.AddState("Intro Out");
            introOutState.motion = introOut;

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip CreateAlphaClip(string name, float from, float to, float duration)
        {
            var clip = new AnimationClip
            {
                name = name,
                frameRate = 60f,
                wrapMode = WrapMode.ClampForever
            };

            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(CanvasGroup), "m_Alpha"),
                AnimationCurve.EaseInOut(0f, from, Mathf.Max(0.01f, duration), to));

            return clip;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static bool IsProfilePanelComplete(PlayerProfilePanel panel)
        {
            if (panel == null)
                return false;

            var so = new SerializedObject(panel);
            string[] required =
            {
                "root",
                "backdrop",
                "txt_Username",
                "txt_ELO",
                "txt_Tier",
                "txt_WinLoss",
                "txt_WinRate",
                "btn_Close"
            };

            foreach (string propertyName in required)
            {
                var property = so.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                    return false;
            }

            return true;
        }

        private static void MarkProfileObjectsDirty(PlayerProfilePanel panel)
        {
            var so = new SerializedObject(panel);
            string[] objectFields =
            {
                "root",
                "backdrop",
                "txt_Username",
                "txt_ELO",
                "txt_Tier",
                "txt_WinLoss",
                "txt_WinRate",
                "btn_Close",
                "loadingIndicator"
            };

            foreach (string field in objectFields)
            {
                var obj = so.FindProperty(field)?.objectReferenceValue;
                if (obj != null)
                    EditorUtility.SetDirty(obj);
            }
        }

        private static int EnsureButtonAudioTriggers()
        {
            int changed = 0;
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button == null || button.GetComponent<UIAudioTrigger>() != null)
                    continue;

                Undo.AddComponent<UIAudioTrigger>(button.gameObject);
                EditorUtility.SetDirty(button.gameObject);
                changed++;
            }

            return changed;
        }

        private static GameObject CreateEditorUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateEditorText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Vector2 anchoredPosition)
        {
            var go = CreateEditorUIObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 64f);
            rect.anchoredPosition = anchoredPosition;

            var label = Undo.AddComponent<TextMeshProUGUI>(go);
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void StretchFull(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
