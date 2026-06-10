#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Michsky.UI.Shift;
using NightHunt.Audio;
using NightHunt.Core;
using NightHunt.Services.Auth;
using NightHunt.Services.Backend;
using NightHunt.Services.Game;
using NightHunt.Services.Party;
using NightHunt.Services.Room;
using NightHunt.UI.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NightHunt.UI.Editor
{
    public static class HomeSceneAuditTool
    {
        private const string HomeScenePath = "Assets/_Night_Hunt/Scenes/01_Home.unity";
        private const string ReportPath = "Temp/NightHunt_01_Home_UI_Audit.md";
        private const string BackupPath = "Temp/01_Home.before-home-flow-refactor.unity";

        private enum ButtonRouteAction
        {
            None,
            ShowHome,
            ShowPartyCustomMode,
            ShowSettings,
            ShowMultiplayer,
            ShowCampaignUnavailable,
            ShowSettingsGameplay,
            ShowSettingsAudio,
            ShowSettingsControls,
            ShowSettingsVisuals
        }

        [MenuItem("Tools/NightHunt/Audit 01_Home UI Flow")]
        public static void AuditHomeScene()
        {
            OpenHomeSceneIfNeeded();
            string report = BuildReport("Manual audit", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            WriteReport(report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("01_Home UI Audit", report, "OK");
        }

        public static void AuditHomeSceneBatch()
        {
            OpenHomeSceneIfNeeded();
            string report = BuildReport("Batch audit", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            WriteReport(report);
            Debug.Log(report);
        }

        [MenuItem("Tools/NightHunt/Apply 01_Home UI Flow Refactor")]
        public static void ApplyHomeFlowRefactorInteractive()
        {
            if (!EditorUtility.DisplayDialog(
                    "Apply 01_Home UI Flow Refactor",
                    "This will back up 01_Home to Temp, remove legacy/broken UnityEvent calls, rewrite direct Shift panel button calls to NightHunt routers, wire code-first routes, centralize UI audio through AudioManager, attach settings lifecycle controllers, and save the scene.",
                    "Apply",
                    "Cancel"))
            {
                return;
            }

            ApplyHomeFlowRefactor(interactive: true);
        }

        public static void ApplyHomeFlowRefactorBatch()
        {
            ApplyHomeFlowRefactor(interactive: false);
        }

        [MenuItem("Tools/NightHunt/Cleanup 01_Home Broken UnityEvents")]
        public static void CleanupBrokenUnityEvents()
        {
            if (!EditorUtility.DisplayDialog(
                    "Cleanup 01_Home Broken UnityEvents",
                    "This opens 01_Home and removes UnityEvent persistent calls whose target is missing. Continue?",
                    "Cleanup",
                    "Cancel"))
            {
                return;
            }

            OpenHomeSceneIfNeeded();
            int removed = RemoveBrokenUnityEventCallsInLoadedScene();
            SaveHomeSceneIfChanged(removed > 0);
            string message = removed == 0
                ? "No broken UnityEvent persistent calls were found."
                : $"Removed {removed} broken UnityEvent persistent calls and saved 01_Home.";
            Debug.Log($"[HomeSceneAuditTool] {message}");
            EditorUtility.DisplayDialog("01_Home Cleanup", message, "OK");
        }

        private static void ApplyHomeFlowRefactor(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(ProjectFilePath("Temp"));
            string homeSceneFile = ProjectFilePath(HomeScenePath);
            if (File.Exists(homeSceneFile))
                File.Copy(homeSceneFile, ProjectFilePath(BackupPath), overwrite: true);

            OpenHomeSceneIfNeeded();

            int brokenRemoved = RemoveBrokenUnityEventCallsInLoadedScene();
            int routesWired = SetupNavigatorRoutes();
            int settingsWired = SetupSettingsLifecycle();
            int directShiftEventsNormalized = NormalizeDirectShiftPanelButtonEvents();
            int shiftPanelManagersRemoved = RemoveShiftPanelManagers();
            int loadingWired = SetupLoadingProgressAdapters();
            int persistentWired = SetupPersistentFlowSceneComponents();
            int demoRemoved = RemoveUnreferencedInactiveDemoObjects();
            int navTargetsWired = SetupCodeFirstNavigationTargets();
            int soundsWired = SetupAudioOwnershipPolicy();

            bool changed = brokenRemoved + directShiftEventsNormalized + shiftPanelManagersRemoved + routesWired + settingsWired + loadingWired + persistentWired + demoRemoved + navTargetsWired + soundsWired > 0;
            SaveHomeSceneIfChanged(changed);

            string report = BuildReport(
                "Apply 01_Home UI Flow Refactor",
                brokenRemoved,
                directShiftEventsNormalized,
                shiftPanelManagersRemoved,
                routesWired,
                settingsWired,
                loadingWired,
                persistentWired,
                demoRemoved,
                navTargetsWired,
                soundsWired);

            WriteReport(report);
            Debug.Log(report);

            if (interactive)
                EditorUtility.DisplayDialog("01_Home Refactor", report, "OK");
        }

        private static int SetupNavigatorRoutes()
        {
            int changed = 0;
            var navigator = UnityEngine.Object.FindFirstObjectByType<UINavigator>(FindObjectsInactive.Include);
            if (navigator == null)
                return 0;

            var mainPanel = FindMainPanelManager();
            var loginView = FindView<LoginView>();
            var homeView = FindView<HomeView>();
            var partyCustomModeView = FindView<PartyCustomModeView>();
            var settingsView = FindView<SettingsView>();
            var splashRoot = loginView != null ? loginView.gameObject : FindByName("Splash Screen");
            var splashAnimator = splashRoot != null ? splashRoot.GetComponent<Animator>() : null;
            var mainPanelsAnimator = FindByName("Main Panels")?.GetComponent<Animator>()
                ?? mainPanel?.GetComponent<Animator>();

            var so = new SerializedObject(navigator);

            changed += SetObject(so.FindProperty("splashScreenAnimator"), splashAnimator) ? 1 : 0;
            changed += SetObject(so.FindProperty("mainPanelsAnimator"), mainPanelsAnimator) ? 1 : 0;
            changed += SetString(so.FindProperty("loginAnimatorState"), "Login") ? 1 : 0;
            changed += SetString(so.FindProperty("splashHiddenState"), "Invisible") ? 1 : 0;
            changed += SetString(so.FindProperty("mainPanelsStartState"), "Start") ? 1 : 0;
            changed += SetString(so.FindProperty("mainPanelsHiddenState"), "Invisible") ? 1 : 0;

            var routes = so.FindProperty("routes");
            if (routes != null)
            {
                changed += SetRoute(routes, PanelType.Login, splashRoot, loginView);
                changed += SetRoute(routes, PanelType.Home, FindRouteRoot(routes, PanelType.Home, mainPanel, "Home"), homeView);
                changed += SetRoute(routes, PanelType.PartyCustomMode, FindRouteRoot(routes, PanelType.PartyCustomMode, mainPanel, "CustomGame"), partyCustomModeView);
                changed += SetRoute(routes, PanelType.Settings, FindRouteRoot(routes, PanelType.Settings, mainPanel, "Settings"), settingsView);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(navigator);

            var router = navigator.GetComponent<HomeUIActionRouter>();
            if (router == null)
            {
                router = Undo.AddComponent<HomeUIActionRouter>(navigator.gameObject);
                changed++;
            }

            var routerSo = new SerializedObject(router);
            changed += SetObject(routerSo.FindProperty("navigator"), navigator) ? 1 : 0;
            routerSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(router);

            return changed;
        }

        private static int SetupCodeFirstNavigationTargets()
        {
            int changed = 0;
            foreach (var lobby in UnityEngine.Object.FindObjectsByType<PartyCustomModeView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(lobby);
                var entries = so.FindProperty("navigationButtons");
                if (entries == null)
                    continue;

                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    changed += SetEnum(entry.FindPropertyRelative("targetPanel"), PanelType.Home) ? 1 : 0;
                }

                changed += SetupPartyCustomModeRequiredReferences(lobby);

                if (changed > 0)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(lobby);
                }
            }

            return changed;
        }

        private static int SetupPartyCustomModeRequiredReferences(PartyCustomModeView lobby)
        {
            if (lobby == null)
                return 0;

            int changed = 0;
            var so = new SerializedObject(lobby);
            var joinCreatePanel = so.FindProperty("joinCreatePanel")?.objectReferenceValue as GameObject;
            var roomCodeText = so.FindProperty("roomCodeText")?.objectReferenceValue as TextMeshProUGUI;
            var inRoomPanel = so.FindProperty("inRoomPanel")?.objectReferenceValue as GameObject;
            var joinCreateParent = joinCreatePanel != null ? joinCreatePanel.transform : lobby.transform;
            var settingsParent = joinCreateParent;

            var copyButton = so.FindProperty("btnCopyCode")?.objectReferenceValue as Button
                ?? FindByNameOnly<Button>(lobby.gameObject, "copy");
            if (copyButton == null && roomCodeText != null)
            {
                copyButton = CreateLobbyCopyButton(roomCodeText.transform.parent ?? lobby.transform);
                changed++;
            }

            var statusText = so.FindProperty("statusText")?.objectReferenceValue as TextMeshProUGUI
                ?? FindByNameOnly<TextMeshProUGUI>(lobby.gameObject, "status");
            if (statusText == null)
            {
                statusText = CreateLobbyStatusText(inRoomPanel != null ? inRoomPanel.transform : lobby.transform);
                changed++;
            }

            var refreshButton = so.FindProperty("btnRefresh")?.objectReferenceValue as Button
                ?? FindByNameOnly<Button>(lobby.gameObject, "refresh");
            if (refreshButton == null)
            {
                refreshButton = CreateLobbyRefreshButton(joinCreateParent);
                changed++;
            }

            var lobbyListStatusText = so.FindProperty("lobbyListStatusText")?.objectReferenceValue as TextMeshProUGUI
                ?? FindByNameOnly<TextMeshProUGUI>(lobby.gameObject, "public lobby status")
                ?? FindByNameOnly<TextMeshProUGUI>(lobby.gameObject, "lobby list status");
            if (lobbyListStatusText == null)
            {
                lobbyListStatusText = CreatePublicLobbyStatusText(joinCreateParent);
                changed++;
            }

            var lobbyListContainer = so.FindProperty("lobbyListContainer")?.objectReferenceValue as Transform
                ?? FindByNameOnly<RectTransform>(lobby.gameObject, "public lobby list")
                ?? FindByNameOnly<RectTransform>(lobby.gameObject, "lobby list");
            if (lobbyListContainer == null)
            {
                lobbyListContainer = CreatePublicLobbyListContainer(joinCreateParent);
                changed++;
            }

            var publicToggle = so.FindProperty("publicToggle")?.objectReferenceValue as Toggle
                ?? FindByNameOnly<Toggle>(lobby.gameObject, "public");
            if (publicToggle == null)
            {
                publicToggle = CreateLobbyToggle(settingsParent, "Public Lobby Toggle", "Public", true, new Vector2(-210f, 74f));
                changed++;
            }

            var lockedToggle = so.FindProperty("lockedToggle")?.objectReferenceValue as Toggle
                ?? FindByNameOnly<Toggle>(lobby.gameObject, "lock");
            if (lockedToggle == null)
            {
                lockedToggle = CreateLobbyToggle(settingsParent, "Locked Lobby Toggle", "Locked", false, new Vector2(-70f, 74f));
                changed++;
            }

            var publicSwitch = so.FindProperty("publicSwitch")?.objectReferenceValue as SwitchManager
                ?? FindSwitchUnderNamedRoot(lobby.gameObject, "Public Room");
            var lockedSwitch = so.FindProperty("lockedSwitch")?.objectReferenceValue as SwitchManager
                ?? FindSwitchUnderNamedRoot(lobby.gameObject, "Locked Room");

            var passwordInput = so.FindProperty("passwordInput")?.objectReferenceValue as TMP_InputField
                ?? FindByNameOnly<TMP_InputField>(lobby.gameObject, "password");
            if (passwordInput == null)
            {
                passwordInput = CreateLobbyPasswordInput(settingsParent);
                changed++;
            }

            changed += SetObject(so.FindProperty("btnCopyCode"), copyButton) ? 1 : 0;
            changed += SetObject(so.FindProperty("statusText"), statusText) ? 1 : 0;
            changed += SetObject(so.FindProperty("btnRefresh"), refreshButton) ? 1 : 0;
            changed += SetObject(so.FindProperty("lobbyListStatusText"), lobbyListStatusText) ? 1 : 0;
            changed += SetObject(so.FindProperty("lobbyListContainer"), lobbyListContainer) ? 1 : 0;
            changed += SetObject(so.FindProperty("publicToggle"), publicToggle) ? 1 : 0;
            changed += SetObject(so.FindProperty("lockedToggle"), lockedToggle) ? 1 : 0;
            changed += SetObject(so.FindProperty("publicSwitch"), publicSwitch) ? 1 : 0;
            changed += SetObject(so.FindProperty("lockedSwitch"), lockedSwitch) ? 1 : 0;
            changed += SetObject(so.FindProperty("passwordInput"), passwordInput) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (changed > 0)
                EditorUtility.SetDirty(lobby);

            return changed;
        }

        private static Button CreateLobbyCopyButton(Transform parent)
        {
            var go = CreateUIObject("Copy Room Code", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 32f);
            rect.anchoredPosition = new Vector2(-8f, 0f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.13f, 0.18f, 0.22f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", go.transform, "Copy", 14f, FontStyles.Normal);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.raycastTarget = false;

            return button;
        }

        private static TextMeshProUGUI CreateLobbyStatusText(Transform parent)
        {
            var text = CreateText("Lobby Status", parent, string.Empty, 16f, FontStyles.Normal);
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 32f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            text.color = new Color(0.82f, 0.9f, 1f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateLobbyRefreshButton(Transform parent)
        {
            var go = CreateUIObject("Refresh Public Lobbies", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(150f, 38f);
            rect.anchoredPosition = new Vector2(0f, -148f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.13f, 0.18f, 0.22f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", go.transform, "Refresh", 14f, FontStyles.Normal);
            StretchFull(label.GetComponent<RectTransform>());
            label.raycastTarget = false;
            return button;
        }

        private static TextMeshProUGUI CreatePublicLobbyStatusText(Transform parent)
        {
            var text = CreateText("Public Lobby Status", parent, string.Empty, 13f, FontStyles.Normal);
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 28f);
            rect.anchoredPosition = new Vector2(0f, -190f);
            text.color = new Color(0.78f, 0.86f, 0.94f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Transform CreatePublicLobbyListContainer(Transform parent)
        {
            var go = CreateUIObject("Public Lobby List", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(720f, 190f);
            rect.anchoredPosition = new Vector2(0f, -212f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(4, 4, 4, 4);
            return go.transform;
        }

        private static Toggle CreateLobbyToggle(Transform parent, string name, string labelText, bool isOn, Vector2 position)
        {
            var go = CreateUIObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(130f, 28f);
            rect.anchoredPosition = position;

            var toggle = go.AddComponent<Toggle>();
            var boxGo = CreateUIObject("Box", go.transform);
            var boxRect = boxGo.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.sizeDelta = new Vector2(22f, 22f);
            boxRect.anchoredPosition = Vector2.zero;
            var box = boxGo.AddComponent<Image>();
            box.color = new Color(0.13f, 0.18f, 0.22f, 0.95f);

            var checkGo = CreateUIObject("Checkmark", boxGo.transform);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(14f, 14f);
            var check = checkGo.AddComponent<Image>();
            check.color = new Color(0.24f, 0.72f, 0.42f, 1f);

            var label = CreateText("Label", go.transform, labelText, 13f, FontStyles.Normal);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(30f, 0f);
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;

            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = isOn;
            return toggle;
        }

        private static TMP_InputField CreateLobbyPasswordInput(Transform parent)
        {
            var go = CreateUIObject("Lobby Password Input", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(230f, 34f);
            rect.anchoredPosition = new Vector2(80f, 74f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.14f, 0.95f);
            var input = go.AddComponent<TMP_InputField>();

            var text = CreateText("Text", go.transform, string.Empty, 14f, FontStyles.Normal);
            StretchFull(text.GetComponent<RectTransform>());
            text.GetComponent<RectTransform>().offsetMin = new Vector2(10f, 0f);
            text.GetComponent<RectTransform>().offsetMax = new Vector2(-10f, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;

            var placeholder = CreateText("Placeholder", go.transform, "Password", 14f, FontStyles.Normal);
            StretchFull(placeholder.GetComponent<RectTransform>());
            placeholder.GetComponent<RectTransform>().offsetMin = new Vector2(10f, 0f);
            placeholder.GetComponent<RectTransform>().offsetMax = new Vector2(-10f, 0f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(0.7f, 0.76f, 0.82f, 0.7f);
            placeholder.raycastTarget = false;

            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = TMP_InputField.ContentType.Password;
            input.inputType = TMP_InputField.InputType.Password;
            input.characterLimit = 50;
            go.SetActive(false);
            return input;
        }

        private static int SetupSettingsLifecycle()
        {
            int changed = 0;
            var mainPanel = FindMainPanelManager();
            var settingsRoot = FindRootForShiftPanel(mainPanel, "Settings") ?? FindByName("Settings");
            if (settingsRoot == null)
                return 0;

            var settingsView = settingsRoot.GetComponent<SettingsView>();
            if (settingsView == null)
            {
                settingsView = Undo.AddComponent<SettingsView>(settingsRoot);
                changed++;
            }

            var audioRoot = FindDescendant(settingsRoot.transform, "Audio");
            var gameplayRoot = FindDescendant(settingsRoot.transform, "Gameplay");
            var controlsRoot = FindDescendant(settingsRoot.transform, "Controls");
            var graphicsRoot = FindDescendant(settingsRoot.transform, "Visuals")
                ?? FindDescendant(settingsRoot.transform, "Graphics");

            var audio = EnsureComponent<AudioSettingsPanel>(audioRoot, ref changed);
            var controls = EnsureComponent<ControlsSettingsPanel>(controlsRoot, ref changed);
            var graphics = EnsureComponent<GraphicsSettingsPanel>(graphicsRoot, ref changed);

            changed += WireSettingsView(settingsView, gameplayRoot?.gameObject, audioRoot?.gameObject, controlsRoot?.gameObject, graphicsRoot?.gameObject, audio, controls, graphics);
            changed += AutoWireAudioSettings(audio);
            changed += AutoWireControlsSettings(controls);
            changed += AutoWireGraphicsSettings(graphics);

            return changed;
        }

        private static int SetupLoadingProgressAdapters()
        {
            int changed = 0;
            foreach (var manager in UnityEngine.Object.FindObjectsByType<LoadingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed += SetupLoadingProgressAdapter(manager);

            foreach (var overlay in UnityEngine.Object.FindObjectsByType<MatchLoadingOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed += SetupMatchLoadingProgressAdapter(overlay);

            return changed;
        }

        private static int SetupPersistentFlowSceneComponents()
        {
            int changed = 0;
            var persistentCanvas = UnityEngine.Object.FindFirstObjectByType<PersistentUICanvas>(FindObjectsInactive.Include);
            if (persistentCanvas == null)
                return 0;

            if (persistentCanvas.GetComponentInChildren<SessionTerminationListener>(true) == null)
            {
                Undo.AddComponent<SessionTerminationListener>(persistentCanvas.gameObject);
                changed++;
            }

            var matchFound = UnityEngine.Object.FindFirstObjectByType<MatchFoundOverlay>(FindObjectsInactive.Include);
            if (matchFound == null)
            {
                matchFound = CreateMatchFoundOverlay(persistentCanvas.transform);
                changed++;
            }

            var canvasSo = new SerializedObject(persistentCanvas);
            changed += SetObject(canvasSo.FindProperty("matchFoundOverlay"), matchFound) ? 1 : 0;
            changed += SetObject(
                canvasSo.FindProperty("sessionTerminationListener"),
                persistentCanvas.GetComponentInChildren<SessionTerminationListener>(true)) ? 1 : 0;
            var mfc = UnityEngine.Object.FindFirstObjectByType<MatchFlowCoordinator>(FindObjectsInactive.Include);
            changed += SetObject(canvasSo.FindProperty("matchFlowCoordinator"), mfc) ? 1 : 0;
            canvasSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(persistentCanvas);

            return changed;
        }

        private static MatchFoundOverlay CreateMatchFoundOverlay(Transform parent)
        {
            var root = CreateUIObject("MatchFoundOverlay", parent);
            StretchFull(root.GetComponent<RectTransform>());
            root.SetActive(true);

            var panel = CreateUIObject("Panel", root.transform);
            StretchFull(panel.GetComponent<RectTransform>());
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);
            var canvasGroup = panel.AddComponent<CanvasGroup>();

            var box = CreateUIObject("Content", panel.transform);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(560f, 420f);
            boxRect.anchoredPosition = Vector2.zero;
            var boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);
            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 28, 28);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;

            var gameMode = CreateText("GameModeText", box.transform, "Match Found", 30f, FontStyles.Bold);
            var mapName = CreateText("MapNameText", box.transform, "Starting server...", 18f, FontStyles.Normal);
            var listRoot = CreateUIObject("PlayerList", box.transform);
            var listLayout = listRoot.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            listRoot.AddComponent<LayoutElement>().preferredHeight = 210f;
            var status = CreateText("StatusText", box.transform, "Waiting for players...", 18f, FontStyles.Normal);

            var templates = CreateUIObject("Templates", root.transform);
            templates.SetActive(false);
            var rowPrefab = CreateMatchFoundRowTemplate(templates.transform);

            var overlay = root.AddComponent<MatchFoundOverlay>();
            var so = new SerializedObject(overlay);
            SetObject(so.FindProperty("panel"), panel);
            SetObject(so.FindProperty("canvasGroup"), canvasGroup);
            SetObject(so.FindProperty("gameModeText"), gameMode);
            SetObject(so.FindProperty("mapNameText"), mapName);
            SetObject(so.FindProperty("playerListParent"), listRoot.transform);
            SetObject(so.FindProperty("playerRowPrefab"), rowPrefab);
            SetObject(so.FindProperty("statusText"), status);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(overlay);
            panel.SetActive(false);

            return overlay;
        }

        private static GameObject CreateMatchFoundRowTemplate(Transform parent)
        {
            var row = CreateUIObject("MatchFoundPlayerRow_Template", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 42f;
            var image = row.AddComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

            var horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(14, 14, 6, 6);
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = false;
            horizontal.childForceExpandWidth = false;

            var nameText = CreateText("NameText", row.transform, "Player", 16f, FontStyles.Normal);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.gameObject.AddComponent<LayoutElement>().preferredWidth = 430f;

            var statusIcon = CreateUIObject("StatusIcon", row.transform);
            var statusRect = statusIcon.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(18f, 18f);
            var statusImage = statusIcon.AddComponent<Image>();
            statusImage.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            var rowView = row.AddComponent<MatchFoundPlayerRow>();
            var so = new SerializedObject(rowView);
            SetObject(so.FindProperty("nameText"), nameText);
            SetObject(so.FindProperty("statusIcon"), statusImage);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rowView);
            return row;
        }

        private static int SetupAudioOwnershipPolicy()
        {
            int changed = 0;

            foreach (var sound in UnityEngine.Object.FindObjectsByType<UIElementSound>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(sound);
                changed++;
            }

            foreach (var bgm in UnityEngine.Object.FindObjectsByType<UIManagerBGMusic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(bgm);
                changed++;
            }

            foreach (var quality in UnityEngine.Object.FindObjectsByType<QualityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(quality);
                changed++;
            }

            changed += RemoveLegacyAudioPrefabEventOverrides();

            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsShiftOwnedAudioSource(source))
                    continue;

                bool sourceChanged = false;
                Undo.RecordObject(source, "Disable Shift audio source");
                if (source.playOnAwake)
                {
                    source.playOnAwake = false;
                    sourceChanged = true;
                }
                if (source.loop)
                {
                    source.loop = false;
                    sourceChanged = true;
                }
                if (sourceChanged)
                {
                    EditorUtility.SetDirty(source);
                    changed++;
                }
            }

            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.GetComponent<UIAudioTrigger>() == null)
                {
                    Undo.AddComponent<UIAudioTrigger>(button.gameObject);
                    changed++;
                }

                var modernButton = button.GetComponent<Michsky.MUIP.ButtonManager>();
                if (modernButton != null)
                {
                    var so = new SerializedObject(modernButton);
                    var prop = so.FindProperty("enableButtonSounds");
                    if (prop != null && prop.boolValue)
                    {
                        prop.boolValue = false;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(modernButton);
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static int RemoveLegacyAudioPrefabEventOverrides()
        {
            int removed = 0;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(GetSelfAndChildren)
                .Where(PrefabUtility.IsAnyPrefabInstanceRoot)
                .Distinct()
                .ToList();

            foreach (var root in roots)
            {
                var modifications = PrefabUtility.GetPropertyModifications(root);
                if (modifications == null || modifications.Length == 0)
                    continue;

                var callPrefixes = modifications
                    .Where(IsLegacyAudioEventModification)
                    .Select(mod => GetPersistentCallPrefix(mod.propertyPath))
                    .Where(prefix => !string.IsNullOrEmpty(prefix))
                    .ToHashSet();

                if (callPrefixes.Count == 0)
                    continue;

                var kept = modifications
                    .Where(mod => !callPrefixes.Any(prefix => mod.propertyPath != null && mod.propertyPath.StartsWith(prefix, StringComparison.Ordinal)))
                    .ToArray();

                if (kept.Length == modifications.Length)
                    continue;

                PrefabUtility.SetPropertyModifications(root, kept);
                EditorUtility.SetDirty(root);
                removed += modifications.Length - kept.Length;
            }

            return removed;
        }

        private static bool IsLegacyAudioEventModification(PropertyModification modification)
        {
            if (modification == null || string.IsNullOrEmpty(modification.propertyPath))
                return false;

            if (modification.propertyPath.IndexOf("m_PersistentCalls.m_Calls.Array.data", StringComparison.Ordinal) < 0)
                return false;

            if (modification.propertyPath.EndsWith(".m_TargetAssemblyTypeName", StringComparison.Ordinal))
                return IsLegacyAudioTypeName(modification.value);

            if (modification.propertyPath.EndsWith(".m_MethodName", StringComparison.Ordinal))
                return modification.value == "VolumeSetMaster"
                    || modification.value == "VolumeSetMusic"
                    || modification.value == "VolumeSetSFX";

            return false;
        }

        private static bool IsLegacyAudioTypeName(string typeName)
        {
            return !string.IsNullOrEmpty(typeName) &&
                   (typeName.IndexOf("Michsky.UI.Shift.QualityManager", StringComparison.Ordinal) >= 0
                    || typeName.IndexOf("Michsky.UI.Shift.UIElementSound", StringComparison.Ordinal) >= 0
                    || typeName.IndexOf("Michsky.UI.Shift.UIManagerBGMusic", StringComparison.Ordinal) >= 0);
        }

        private static string GetPersistentCallPrefix(string propertyPath)
        {
            const string marker = ".m_PersistentCalls.m_Calls.Array.data[";
            int markerIndex = propertyPath?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            if (markerIndex < 0)
                return null;

            int closeIndex = propertyPath.IndexOf(']', markerIndex);
            return closeIndex < 0 ? null : propertyPath.Substring(0, closeIndex + 1);
        }

        private static bool IsShiftOwnedAudioSource(AudioSource source)
        {
            if (source == null)
                return false;

            string name = source.gameObject.name;
            return name.Equals("UI Audio", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("Background Music", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("BG Music", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("BGM", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int RemoveBrokenUnityEventCallsInLoadedScene()
        {
            int removed = 0;
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                var serializedObject = new SerializedObject(behaviour);
                var property = serializedObject.GetIterator();
                bool changed = false;

                while (property.Next(true))
                {
                    if (!property.isArray ||
                        !property.propertyPath.EndsWith("m_PersistentCalls.m_Calls", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    for (int i = property.arraySize - 1; i >= 0; i--)
                    {
                        var call = property.GetArrayElementAtIndex(i);
                        var target = call.FindPropertyRelative("m_Target");

                        if (target == null || target.objectReferenceValue != null)
                        {
                            continue;
                        }

                        property.DeleteArrayElementAtIndex(i);
                        removed++;
                        changed = true;
                    }
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(behaviour);
                }
            }

            return removed;
        }

        private static int NormalizeDirectShiftPanelButtonEvents()
        {
            int changed = 0;
            var router = UnityEngine.Object.FindFirstObjectByType<HomeUIActionRouter>(FindObjectsInactive.Include);
            var settingsView = UnityEngine.Object.FindFirstObjectByType<SettingsView>(FindObjectsInactive.Include);
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                var serializedObject = new SerializedObject(button);
                var calls = serializedObject.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null)
                    continue;

                var actions = new List<ButtonRouteAction>();
                bool buttonChanged = false;

                for (int i = calls.arraySize - 1; i >= 0; i--)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
                    var method = call.FindPropertyRelative("m_MethodName")?.stringValue;

                    var manager = target as MainPanelManager;
                    if (manager == null)
                    {
                        continue;
                    }

                    if (method == "OpenPanel" || method == "OpenFirstTab")
                    {
                        string panelName = call.FindPropertyRelative("m_Arguments.m_StringArgument")?.stringValue;
                        var action = ResolveLegacyPanelAction(manager, method, panelName);
                        if (action != ButtonRouteAction.None)
                            actions.Insert(0, action);
                    }
                    else if (!string.IsNullOrWhiteSpace(method))
                    {
                        Debug.LogWarning($"[HomeSceneAuditTool] Removed direct Shift MainPanelManager call '{method}' from button '{button.name}'.");
                    }

                    Undo.RecordObject(button, "Normalize direct Shift panel button event");
                    calls.DeleteArrayElementAtIndex(i);
                    buttonChanged = true;
                    changed++;
                }

                if (buttonChanged)
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(button);
                }

                foreach (var action in DistinctPreserveOrder(actions))
                    changed += AddCodeFirstButtonListener(button, router, settingsView, action) ? 1 : 0;
            }

            return changed;
        }

        private static int CountDirectShiftPanelButtonCalls()
        {
            int count = 0;
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                var serializedObject = new SerializedObject(button);
                var calls = serializedObject.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null)
                    continue;

                for (int i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
                    var method = call.FindPropertyRelative("m_MethodName")?.stringValue;

                    if (target is MainPanelManager)
                        count++;
                }
            }

            return count;
        }

        private static IEnumerable<ButtonRouteAction> DistinctPreserveOrder(IEnumerable<ButtonRouteAction> actions)
        {
            var seen = new HashSet<ButtonRouteAction>();
            foreach (var action in actions)
            {
                if (action == ButtonRouteAction.None || !seen.Add(action))
                    continue;

                yield return action;
            }
        }

        private static ButtonRouteAction ResolveLegacyPanelAction(MainPanelManager manager, string method, string panelName)
        {
            if (method == "OpenFirstTab")
                return IsSettingsPanelManager(manager) ? ButtonRouteAction.ShowSettingsGameplay : ButtonRouteAction.ShowHome;

            if (string.IsNullOrWhiteSpace(panelName))
                return ButtonRouteAction.None;

            switch (panelName.Trim())
            {
                case "Home":
                    return ButtonRouteAction.ShowHome;
                case "Campaign":
                    return ButtonRouteAction.ShowCampaignUnavailable;
                case "Multiplayer":
                    return ButtonRouteAction.ShowMultiplayer;
                case "Settings":
                    return ButtonRouteAction.ShowSettings;
                case "CustomGame":
                case "PartyCustomMode":
                    return ButtonRouteAction.ShowPartyCustomMode;
                case "Gameplay":
                    return ButtonRouteAction.ShowSettingsGameplay;
                case "Audio":
                    return ButtonRouteAction.ShowSettingsAudio;
                case "Controls":
                    return ButtonRouteAction.ShowSettingsControls;
                case "Visuals":
                case "Graphics":
                    return ButtonRouteAction.ShowSettingsVisuals;
                default:
                    Debug.LogWarning($"[HomeSceneAuditTool] Direct Shift panel call '{panelName}' on '{manager.name}' has no NightHunt route mapping.");
                    return ButtonRouteAction.None;
            }
        }

        private static bool AddCodeFirstButtonListener(
            Button button,
            HomeUIActionRouter router,
            SettingsView settingsView,
            ButtonRouteAction action)
        {
            if (button == null || action == ButtonRouteAction.None)
                return false;

            switch (action)
            {
                case ButtonRouteAction.ShowHome:
                    return AddPersistentListenerIfMissing(button, router, nameof(HomeUIActionRouter.ShowHome), () => UnityEventTools.AddPersistentListener(button.onClick, router.ShowHome));
                case ButtonRouteAction.ShowPartyCustomMode:
                    return AddPersistentListenerIfMissing(button, router, nameof(HomeUIActionRouter.ShowPartyCustomMode), () => UnityEventTools.AddPersistentListener(button.onClick, router.ShowPartyCustomMode));
                case ButtonRouteAction.ShowSettings:
                    return AddPersistentListenerIfMissing(button, router, nameof(HomeUIActionRouter.ShowSettings), () => UnityEventTools.AddPersistentListener(button.onClick, router.ShowSettings));
                case ButtonRouteAction.ShowMultiplayer:
                    return AddPersistentListenerIfMissing(button, router, nameof(HomeUIActionRouter.ShowMultiplayer), () => UnityEventTools.AddPersistentListener(button.onClick, router.ShowMultiplayer));
                case ButtonRouteAction.ShowCampaignUnavailable:
                    return AddPersistentListenerIfMissing(button, router, nameof(HomeUIActionRouter.ShowCampaignUnavailable), () => UnityEventTools.AddPersistentListener(button.onClick, router.ShowCampaignUnavailable));
                case ButtonRouteAction.ShowSettingsGameplay:
                    return AddPersistentListenerIfMissing(button, settingsView, nameof(SettingsView.ShowGameplay), () => UnityEventTools.AddPersistentListener(button.onClick, settingsView.ShowGameplay));
                case ButtonRouteAction.ShowSettingsAudio:
                    return AddPersistentListenerIfMissing(button, settingsView, nameof(SettingsView.ShowAudio), () => UnityEventTools.AddPersistentListener(button.onClick, settingsView.ShowAudio));
                case ButtonRouteAction.ShowSettingsControls:
                    return AddPersistentListenerIfMissing(button, settingsView, nameof(SettingsView.ShowControls), () => UnityEventTools.AddPersistentListener(button.onClick, settingsView.ShowControls));
                case ButtonRouteAction.ShowSettingsVisuals:
                    return AddPersistentListenerIfMissing(button, settingsView, nameof(SettingsView.ShowVisuals), () => UnityEventTools.AddPersistentListener(button.onClick, settingsView.ShowVisuals));
                default:
                    return false;
            }
        }

        private static bool AddPersistentListenerIfMissing(
            Button button,
            UnityEngine.Object target,
            string methodName,
            Action addListener)
        {
            if (button == null || target == null || string.IsNullOrWhiteSpace(methodName) || addListener == null)
                return false;

            if (HasPersistentListener(button, target, methodName))
                return false;

            Undo.RecordObject(button, $"Wire {methodName}");
            addListener();
            EditorUtility.SetDirty(button);
            return true;
        }

        private static bool HasPersistentListener(Button button, UnityEngine.Object target, string methodName)
        {
            var serializedObject = new SerializedObject(button);
            var calls = serializedObject.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
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

        private static bool IsSettingsPanelManager(MainPanelManager manager)
        {
            return manager?.panels != null && manager.panels.Any(panel =>
                panel.panelName == "Gameplay" ||
                 panel.panelName == "Audio" ||
                 panel.panelName == "Controls" ||
                 panel.panelName == "Visuals" ||
                 panel.panelName == "Graphics");
        }

        private static int RemoveShiftPanelManagers()
        {
            int removed = 0;
            var managers = UnityEngine.Object.FindObjectsByType<MainPanelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var manager in managers)
            {
                if (manager == null)
                    continue;

                Undo.DestroyObjectImmediate(manager);
                removed++;
            }

            return removed;
        }

        private static int RemoveUnreferencedInactiveDemoObjects()
        {
            int removed = 0;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var candidates = roots
                .SelectMany(GetSelfAndChildren)
                .Where(go => !go.activeSelf && IsDemoCandidateName(go.name))
                .Where(go => go.GetComponentsInChildren<MonoBehaviour>(true)
                    .All(mb => mb == null || !(mb.GetType().Namespace?.StartsWith("NightHunt", StringComparison.Ordinal) ?? false)))
                .Where(go => !IsReferencedOutsideHierarchy(go))
                .ToList();

            foreach (var go in candidates)
            {
                Undo.DestroyObjectImmediate(go);
                removed++;
            }

            return removed;
        }

        private static string BuildReport(
            string action,
            int brokenRemoved,
            int directShiftEventsNormalized,
            int shiftPanelManagersRemoved,
            int routesWired,
            int settingsWired,
            int loadingWired,
            int persistentWired,
            int demoRemoved,
            int navTargetsWired,
            int soundsWired)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 01_Home UI Flow Audit");
            sb.AppendLine();
            sb.AppendLine($"Action: {action}");
            sb.AppendLine($"Scene: {HomeScenePath}");
            sb.AppendLine($"Backup: {BackupPath}");
            sb.AppendLine();
            sb.AppendLine("## Applied Changes");
            sb.AppendLine($"- Broken UnityEvent calls removed: {brokenRemoved}");
            sb.AppendLine($"- Direct Shift panel button calls normalized: {directShiftEventsNormalized}");
            sb.AppendLine($"- Shift MainPanelManager components removed: {shiftPanelManagersRemoved}");
            sb.AppendLine($"- Navigator routes wired: {routesWired}");
            sb.AppendLine($"- Settings lifecycle/controllers wired: {settingsWired}");
            sb.AppendLine($"- Loading progress adapters wired: {loadingWired}");
            sb.AppendLine($"- Persistent flow components wired: {persistentWired}");
            sb.AppendLine($"- Inactive demo objects removed: {demoRemoved}");
            sb.AppendLine($"- Code-first navigation target updates: {navTargetsWired}");
            sb.AppendLine($"- Audio ownership policy updates: {soundsWired}");
            sb.AppendLine();

            string homeSceneFile = ProjectFilePath(HomeScenePath);
            string yaml = File.Exists(homeSceneFile) ? File.ReadAllText(homeSceneFile) : string.Empty;
            sb.AppendLine("## Scene YAML");
            sb.AppendLine($"- Broken UnityEvent target markers: {Count(yaml, "m_Target: {fileID: 0}")}");
            sb.AppendLine($"- Missing MonoBehaviour script markers: {Count(yaml, "m_Script: {fileID: 0}")}");
            sb.AppendLine($"- GameObjects: {Count(yaml, "--- !u!1 &")}");
            sb.AppendLine($"- MonoBehaviours: {Count(yaml, "--- !u!114 &")}");
            sb.AppendLine($"- Animators: {Count(yaml, "--- !u!95 &")}");
            sb.AppendLine();

            sb.AppendLine("## Core Components In Loaded Scene");
            AppendCount<UINavigator>(sb, "UINavigator");
            AppendCount<HomeUIActionRouter>(sb, "HomeUIActionRouter");
            AppendCount<LoginView>(sb, "LoginView");
            AppendCount<HomeView>(sb, "HomeView");
            AppendCount<PartyCustomModeView>(sb, "PartyCustomModeView");
            AppendCount<SettingsView>(sb, "SettingsView");
            AppendCount<LoadingManager>(sb, "LoadingManager");
            AppendCount<BootIntroView>(sb, "BootIntroView");
            AppendCount<ToastService>(sb, "ToastService");
            AppendCount<GameModalWindow>(sb, "GameModalWindow");
            AppendCount<MatchFlowCoordinator>(sb, "MatchFlowCoordinator");
            AppendCount<MatchFoundOverlay>(sb, "MatchFoundOverlay");
            AppendCount<SessionTerminationListener>(sb, "SessionTerminationListener");
            sb.AppendLine();

            sb.AppendLine("## Service Instances In Loaded Scene");
            AppendCount<GameManager>(sb, "GameManager");
            AppendCount<AuthService>(sb, "AuthService");
            AppendCount<BackendHttpClient>(sb, "BackendHttpClient");
            AppendCount<GameWebSocketService>(sb, "GameWebSocketService");
            AppendCount<RoomService>(sb, "RoomService");
            AppendCount<PartyService>(sb, "PartyService");
            AppendCount<GameEventBus>(sb, "GameEventBus");
            sb.AppendLine();

            sb.AppendLine("## Package Components");
            AppendCount<MainPanelManager>(sb, "Shift MainPanelManager");
            AppendCount<UIElementSound>(sb, "Shift UIElementSound");
            AppendCount<UIManagerBGMusic>(sb, "Shift UIManagerBGMusic");
            AppendCount<QualityManager>(sb, "Shift QualityManager");
            AppendCount<UIAudioTrigger>(sb, "NightHunt UIAudioTrigger");
            AppendCount<SliderManager>(sb, "Shift SliderManager");
            AppendCount<SwitchManager>(sb, "Shift SwitchManager");
            AppendCount<Michsky.MUIP.NotificationManager>(sb, "MUIP NotificationManager");
            AppendCount<Michsky.MUIP.CustomDropdown>(sb, "MUIP CustomDropdown");
            sb.AppendLine();

            sb.AppendLine("## Persistent Button Calls");
            sb.AppendLine($"- Button -> Shift MainPanelManager calls: {CountDirectShiftPanelButtonCalls()}");
            sb.AppendLine($"- Button -> FriendsPanelManager.WindowIn calls: {CountFriendPanelWindowInButtonCalls()}");
            sb.AppendLine();

            sb.AppendLine("## NightHunt Flow Guards");
            sb.AppendLine($"- LoginView.onLoginSuccess persistent calls: {CountPersistentEventCalls<LoginView>("onLoginSuccess")}");
            sb.AppendLine($"- LoginView.onRegisterSuccess persistent calls: {CountPersistentEventCalls<LoginView>("onRegisterSuccess")}");
            sb.AppendLine($"- FriendPanelView.onOpenRequested persistent calls: {CountPersistentEventCalls<FriendPanelView>("onOpenRequested")}");
            sb.AppendLine($"- Incomplete PlayerProfilePanel instances: {CountIncompleteProfilePanels()}");
            sb.AppendLine($"- Incomplete BootIntroView instances: {CountIncompleteBootIntroViews()}");
            sb.AppendLine($"- Scene-scoped BootIntroView instances: {CountSceneScopedBootIntroViews()}");
            sb.AppendLine($"- Buttons missing UIAudioTrigger: {CountButtonsMissingAudioTrigger()}");
            sb.AppendLine();

            sb.AppendLine("## Expected Final State");
            sb.AppendLine("- Broken UnityEvent target markers should be 0.");
            sb.AppendLine("- Missing script markers should be 0.");
            sb.AppendLine("- UINavigator has Login/Home/PartyCustomMode/Settings routes assigned.");
            sb.AppendLine("- BootIntroView lives under PersistentUICanvas/CanvasDontDestroy, and LoadingManager.bootIntroView points there.");
            sb.AppendLine("- UINavigator owns route roots and Splash/MainPanels animator states; it does not reference Shift MainPanelManager.");
            sb.AppendLine("- Buttons call HomeUIActionRouter/SettingsView or feature controllers, not Shift MainPanelManager.");
            sb.AppendLine("- Legacy OnGo* UnityEvents are not used.");
            sb.AppendLine("- AudioManager owns music/UI SFX; Shift UIElementSound, UIManagerBGMusic, and QualityManager should be 0 in 01_Home.");
            sb.AppendLine("- MatchFlowCoordinator owns match_ready/ds_ready scene loading.");
            return sb.ToString();
        }

        private static int CountPersistentEventCalls<T>(string eventFieldName) where T : MonoBehaviour
        {
            int count = 0;
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null)
                    continue;

                var so = new SerializedObject(behaviour);
                var calls = so.FindProperty($"{eventFieldName}.m_PersistentCalls.m_Calls");
                if (calls != null)
                    count += calls.arraySize;
            }

            return count;
        }

        private static int CountIncompleteProfilePanels()
        {
            int count = 0;
            foreach (var panel in UnityEngine.Object.FindObjectsByType<PlayerProfilePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (panel == null || !IsPlayerProfilePanelComplete(panel))
                    count++;
            }

            return count;
        }

        private static int CountIncompleteBootIntroViews()
        {
            int count = 0;
            var persistentCanvas = UnityEngine.Object.FindFirstObjectByType<PersistentUICanvas>(FindObjectsInactive.Include);
            Transform persistentRoot = persistentCanvas != null ? persistentCanvas.transform.root : null;
            foreach (var view in UnityEngine.Object.FindObjectsByType<BootIntroView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view == null)
                {
                    count++;
                    continue;
                }

                var so = new SerializedObject(view);
                string[] required =
                {
                    "root",
                    "canvasGroup",
                    "animator",
                    "backgroundImage",
                    "titleText",
                    "subtitleText"
                };

                bool incomplete = false;
                foreach (string propertyName in required)
                {
                    var property = so.FindProperty(propertyName);
                    if (property != null && property.objectReferenceValue != null)
                        continue;

                    incomplete = true;
                    break;
                }

                if (view.GetComponent<Animator>()?.runtimeAnimatorController == null)
                    incomplete = true;

                if (persistentRoot == null || view.transform.root != persistentRoot)
                    incomplete = true;

                if (incomplete)
                    count++;
            }

            return count;
        }

        private static int CountSceneScopedBootIntroViews()
        {
            int count = 0;
            var persistentCanvas = UnityEngine.Object.FindFirstObjectByType<PersistentUICanvas>(FindObjectsInactive.Include);
            Transform persistentRoot = persistentCanvas != null ? persistentCanvas.transform.root : null;
            foreach (var view in UnityEngine.Object.FindObjectsByType<BootIntroView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view == null)
                    continue;

                if (persistentRoot == null || view.transform.root != persistentRoot)
                    count++;
            }

            return count;
        }

        private static bool IsPlayerProfilePanelComplete(PlayerProfilePanel panel)
        {
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

            foreach (string field in required)
            {
                var property = so.FindProperty(field);
                if (property == null || property.objectReferenceValue == null)
                    return false;
            }

            return true;
        }

        private static int CountButtonsMissingAudioTrigger()
        {
            int count = 0;
            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button != null && button.GetComponent<UIAudioTrigger>() == null)
                    count++;
            }

            return count;
        }

        private static int CountFriendPanelWindowInButtonCalls()
        {
            int count = 0;
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var button in buttons)
            {
                if (button == null)
                    continue;

                var serializedObject = new SerializedObject(button);
                var calls = serializedObject.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null)
                    continue;

                for (int i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    string method = call.FindPropertyRelative("m_MethodName")?.stringValue;
                    if (method != "WindowIn")
                        continue;

                    var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
                    string targetType = target != null ? target.GetType().FullName : string.Empty;
                    string serializedType = call.FindPropertyRelative("m_TargetAssemblyTypeName")?.stringValue ?? string.Empty;
                    if (targetType.Contains("Michsky.UI.Shift.FriendsPanelManager") ||
                        serializedType.Contains("Michsky.UI.Shift.FriendsPanelManager"))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int WireSettingsView(
            SettingsView settingsView,
            GameObject gameplayRoot,
            GameObject audioRoot,
            GameObject controlsRoot,
            GameObject graphicsRoot,
            AudioSettingsPanel audio,
            ControlsSettingsPanel controls,
            GraphicsSettingsPanel graphics)
        {
            if (settingsView == null)
                return 0;

            int changed = 0;
            var so = new SerializedObject(settingsView);
            changed += SetObject(so.FindProperty("gameplaySettingsRoot"), gameplayRoot) ? 1 : 0;
            changed += SetObject(so.FindProperty("audioSettingsRoot"), audioRoot) ? 1 : 0;
            changed += SetObject(so.FindProperty("controlsSettingsRoot"), controlsRoot) ? 1 : 0;
            changed += SetObject(so.FindProperty("graphicsSettingsRoot"), graphicsRoot) ? 1 : 0;
            changed += SetObject(so.FindProperty("audioSettingsPanel"), audio) ? 1 : 0;
            changed += SetObject(so.FindProperty("controlsSettingsPanel"), controls) ? 1 : 0;
            changed += SetObject(so.FindProperty("graphicsSettingsPanel"), graphics) ? 1 : 0;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settingsView);
            return changed;
        }

        private static int AutoWireAudioSettings(AudioSettingsPanel audio)
        {
            if (audio == null)
                return 0;

            var sliders = audio.GetComponentsInChildren<Slider>(true);
            if (sliders.Length == 0)
                return 0;

            var so = new SerializedObject(audio);
            var entries = so.FindProperty("sliders");
            if (entries == null)
                return 0;

            string[] keys = { "MasterVol", "MusicVol", "SFXVol", "UIVol" };
            int count = Mathf.Min(keys.Length, sliders.Length);
            if (entries.arraySize == count)
                return 0;

            entries.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("paramKey").stringValue = keys[i];
                entry.FindPropertyRelative("slider").objectReferenceValue = sliders[i];
                entry.FindPropertyRelative("defaultValue").floatValue = 1f;
                var label = sliders[i].GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                entry.FindPropertyRelative("percentLabel").objectReferenceValue = label;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(audio);
            return count;
        }

        private static int AutoWireControlsSettings(ControlsSettingsPanel controls)
        {
            if (controls == null)
                return 0;

            int changed = 0;
            var so = new SerializedObject(controls);
            changed += SetObject(so.FindProperty("mouseSensitivitySlider"), FindByNameOrFirst<Slider>(controls.gameObject, "sens")) ? 1 : 0;
            changed += SetObject(so.FindProperty("invertYToggle"), FindByNameOrFirst<Toggle>(controls.gameObject, "invert")) ? 1 : 0;
            changed += SetObject(so.FindProperty("invertYSwitch"), FindByNameOrFirst<SwitchManager>(controls.gameObject, "invert")) ? 1 : 0;
            so.ApplyModifiedProperties();
            if (changed > 0) EditorUtility.SetDirty(controls);
            return changed;
        }

        private static int AutoWireGraphicsSettings(GraphicsSettingsPanel graphics)
        {
            if (graphics == null)
                return 0;

            int changed = 0;
            var so = new SerializedObject(graphics);
            var dropdowns = graphics.GetComponentsInChildren<TMP_Dropdown>(true);
            if (dropdowns.Length > 0)
                changed += SetObject(so.FindProperty("qualityDropdown"), dropdowns[0]) ? 1 : 0;
            if (dropdowns.Length > 1)
                changed += SetObject(so.FindProperty("resolutionDropdown"), dropdowns[1]) ? 1 : 0;

            changed += SetObject(so.FindProperty("vsyncToggle"), FindByNameOrFirst<Toggle>(graphics.gameObject, "vsync")) ? 1 : 0;
            changed += SetObject(so.FindProperty("fullscreenToggle"), FindByNameOrFirst<Toggle>(graphics.gameObject, "full")) ? 1 : 0;
            changed += SetObject(so.FindProperty("vsyncSwitch"), FindByNameOrFirst<SwitchManager>(graphics.gameObject, "vsync")) ? 1 : 0;
            changed += SetObject(so.FindProperty("fullscreenSwitch"), FindByNameOrFirst<SwitchManager>(graphics.gameObject, "full")) ? 1 : 0;
            so.ApplyModifiedProperties();
            if (changed > 0) EditorUtility.SetDirty(graphics);
            return changed;
        }

        private static int SetupLoadingProgressAdapter(LoadingManager manager)
        {
            var so = new SerializedObject(manager);
            var progressBar = so.FindProperty("progressBar")?.objectReferenceValue as Slider;
            int changed = 0;
            var loadingPanel = so.FindProperty("loadingPanel")?.objectReferenceValue as GameObject;
            var loadingText = so.FindProperty("loadingText")?.objectReferenceValue as TextMeshProUGUI;

            if (loadingPanel != null)
            {
                if (progressBar == null)
                {
                    progressBar = loadingPanel.GetComponentInChildren<Slider>(true) ?? CreateLoadingSlider(loadingPanel.transform);
                    changed++;
                }

                if (loadingText == null)
                {
                    loadingText = loadingPanel.GetComponentInChildren<TextMeshProUGUI>(true) ?? CreateLoadingMessage(loadingPanel.transform);
                    changed++;
                }

                var retryButton = so.FindProperty("retryButton")?.objectReferenceValue as Button;
                if (retryButton == null)
                {
                    retryButton = FindByNameOnly<Button>(loadingPanel, "retry") ?? CreateLoadingRetryButton(loadingPanel.transform);
                    changed++;
                    changed += SetObject(so.FindProperty("retryButton"), retryButton) ? 1 : 0;
                }
            }

            if (progressBar == null)
                return changed;

            var view = progressBar.GetComponent<LoadingProgressView>();
            if (view == null)
            {
                view = Undo.AddComponent<LoadingProgressView>(progressBar.gameObject);
                changed++;
            }

            changed += WireLoadingProgressView(view, progressBar, loadingText);
            changed += SetObject(so.FindProperty("progressBar"), progressBar) ? 1 : 0;
            changed += SetObject(so.FindProperty("loadingText"), loadingText) ? 1 : 0;
            changed += SetObject(so.FindProperty("progressViewComponent"), view) ? 1 : 0;
            so.ApplyModifiedProperties();
            if (changed > 0) EditorUtility.SetDirty(manager);
            return changed;
        }

        private static Slider CreateLoadingSlider(Transform parent)
        {
            var root = CreateUIObject("Loading Progress Bar", parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(480f, 12f);
            rect.anchoredPosition = new Vector2(0f, 96f);

            var background = root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.12f, 0.85f);

            var fillArea = CreateUIObject("Fill Area", root.transform);
            StretchFull(fillArea.GetComponent<RectTransform>());

            var fill = CreateUIObject("Fill", fillArea.transform);
            StretchFull(fill.GetComponent<RectTransform>());
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.25f, 0.75f, 1f, 1f);

            var slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = background;
            slider.fillRect = fill.GetComponent<RectTransform>();
            return slider;
        }

        private static TextMeshProUGUI CreateLoadingMessage(Transform parent)
        {
            var text = CreateText("Loading Message", parent, "Starting up...", 18f, FontStyles.Normal);
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 44f);
            rect.anchoredPosition = new Vector2(0f, 132f);
            return text;
        }

        private static Button CreateLoadingRetryButton(Transform parent)
        {
            var go = CreateUIObject("Retry", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 44f);
            rect.anchoredPosition = new Vector2(0f, 48f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.2f, 0.24f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", go.transform, "Retry", 16f, FontStyles.Normal);
            StretchFull(label.GetComponent<RectTransform>());
            label.raycastTarget = false;
            go.SetActive(false);
            return button;
        }

        private static int SetupMatchLoadingProgressAdapter(MatchLoadingOverlay overlay)
        {
            var so = new SerializedObject(overlay);
            var progressBar = so.FindProperty("overallProgressBar")?.objectReferenceValue as Slider;
            var statusText = so.FindProperty("statusText")?.objectReferenceValue as TMP_Text;
            if (progressBar == null)
                return 0;

            var view = progressBar.GetComponent<LoadingProgressView>();
            int changed = 0;
            if (view == null)
            {
                view = Undo.AddComponent<LoadingProgressView>(progressBar.gameObject);
                changed++;
            }

            changed += WireLoadingProgressView(view, progressBar, statusText);
            changed += SetObject(so.FindProperty("overallProgressViewComponent"), view) ? 1 : 0;
            so.ApplyModifiedProperties();
            if (changed > 0) EditorUtility.SetDirty(overlay);
            return changed;
        }

        private static int WireLoadingProgressView(LoadingProgressView view, Slider slider, TMP_Text messageText)
        {
            int changed = 0;
            var so = new SerializedObject(view);
            changed += SetObject(so.FindProperty("unitySlider"), slider) ? 1 : 0;
            changed += SetObject(so.FindProperty("shiftSlider"), slider != null ? slider.GetComponent<SliderManager>() : null) ? 1 : 0;
            changed += SetObject(so.FindProperty("messageLabel"), messageText) ? 1 : 0;
            so.ApplyModifiedProperties();
            if (changed > 0) EditorUtility.SetDirty(view);
            return changed;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, FontStyles style)
        {
            var go = CreateUIObject(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private static void StretchFull(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static int SetRoute(SerializedProperty routes, PanelType panel, GameObject root, MonoBehaviour view)
        {
            int index = FindRouteIndex(routes, panel);
            if (index < 0)
            {
                index = routes.arraySize;
                routes.InsertArrayElementAtIndex(index);
            }

            var route = routes.GetArrayElementAtIndex(index);
            int changed = 0;
            changed += SetEnum(route.FindPropertyRelative("panel"), panel) ? 1 : 0;
            changed += SetObject(route.FindPropertyRelative("rootObject"), root) ? 1 : 0;
            changed += SetObject(route.FindPropertyRelative("view"), view) ? 1 : 0;
            return changed;
        }

        private static int FindRouteIndex(SerializedProperty routes, PanelType panel)
        {
            for (int i = 0; i < routes.arraySize; i++)
            {
                var routePanel = routes.GetArrayElementAtIndex(i).FindPropertyRelative("panel");
                if (routePanel != null && (PanelType)routePanel.enumValueIndex == panel)
                    return i;
            }
            return -1;
        }

        private static T EnsureComponent<T>(Transform root, ref int changed) where T : Component
        {
            if (root == null)
                return null;

            var component = root.GetComponent<T>();
            if (component != null)
                return component;

            changed++;
            return Undo.AddComponent<T>(root.gameObject);
        }

        private static bool SetObject(SerializedProperty property, UnityEngine.Object value)
        {
            if (property == null || value == null || property.objectReferenceValue == value)
                return false;

            property.objectReferenceValue = value;
            return true;
        }

        private static bool SetString(SerializedProperty property, string value)
        {
            if (property == null || property.stringValue == value)
                return false;

            property.stringValue = value ?? string.Empty;
            return true;
        }

        private static bool SetEnum(SerializedProperty property, PanelType value)
        {
            if (property == null || property.enumValueIndex == (int)value)
                return false;

            property.enumValueIndex = (int)value;
            return true;
        }

        private static T FindView<T>() where T : MonoBehaviour
        {
            return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        private static MainPanelManager FindMainPanelManager()
        {
            return UnityEngine.Object.FindObjectsByType<MainPanelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m.panels != null && m.panels.Any(p => p.panelName == "Home"));
        }

        private static GameObject FindRootForShiftPanel(MainPanelManager manager, string panelName)
        {
            return manager?.panels?.FirstOrDefault(p => p.panelName == panelName)?.panelObject;
        }

        private static GameObject FindRouteRoot(
            SerializedProperty routes,
            PanelType panel,
            MainPanelManager manager,
            string fallbackName)
        {
            return FindRootForShiftPanel(manager, fallbackName)
                ?? GetExistingRouteRoot(routes, panel)
                ?? FindByName(fallbackName);
        }

        private static GameObject GetExistingRouteRoot(SerializedProperty routes, PanelType panel)
        {
            int index = FindRouteIndex(routes, panel);
            if (index < 0)
                return null;

            return routes.GetArrayElementAtIndex(index)
                .FindPropertyRelative("rootObject")
                ?.objectReferenceValue as GameObject;
        }

        private static GameObject FindByName(string name)
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(GetSelfAndChildren)
                .FirstOrDefault(go => go.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return transform;
            }

            return null;
        }

        private static T FindByNameOrFirst<T>(GameObject root, string nameToken) where T : Component
        {
            if (root == null)
                return null;

            var components = root.GetComponentsInChildren<T>(true);
            return components.FirstOrDefault(c => c.name.IndexOf(nameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? components.FirstOrDefault();
        }

        private static T FindByNameOnly<T>(GameObject root, string nameToken) where T : Component
        {
            if (root == null)
                return null;

            return root.GetComponentsInChildren<T>(true)
                .FirstOrDefault(c => c.name.IndexOf(nameToken, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static SwitchManager FindSwitchUnderNamedRoot(GameObject root, string rootName)
        {
            if (root == null)
                return null;

            foreach (var switchManager in root.GetComponentsInChildren<SwitchManager>(true))
            {
                if (switchManager == null)
                    continue;

                Transform current = switchManager.transform;
                while (current != null && current != root.transform)
                {
                    if (string.Equals(current.name, rootName, StringComparison.OrdinalIgnoreCase))
                        return switchManager;
                    current = current.parent;
                }
            }

            return null;
        }

        private static IEnumerable<GameObject> GetSelfAndChildren(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
            {
                foreach (var nested in GetSelfAndChildren(child.gameObject))
                    yield return nested;
            }
        }

        private static bool IsDemoCandidateName(string name)
        {
            return name.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("sample", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("example", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("template", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsReferencedOutsideHierarchy(GameObject target)
        {
            var hierarchy = new HashSet<UnityEngine.Object>(target.GetComponentsInChildren<Component>(true).Where(c => c != null));
            hierarchy.Add(target);

            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || hierarchy.Contains(behaviour))
                    continue;

                var so = new SerializedObject(behaviour);
                var iter = so.GetIterator();
                while (iter.NextVisible(true))
                {
                    if (iter.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var referenced = iter.objectReferenceValue;
                    if (referenced != null && hierarchy.Contains(referenced))
                        return true;
                }
            }

            return false;
        }

        private static void AppendCount<T>(StringBuilder sb, string label) where T : UnityEngine.Object
        {
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"- {label}: {found.Length}");
        }

        private static int Count(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static void OpenHomeSceneIfNeeded()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != HomeScenePath)
                EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        private static void SaveHomeSceneIfChanged(bool changed)
        {
            if (!changed)
                return;

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WriteReport(string report)
        {
            string reportFile = ProjectFilePath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportFile) ?? ProjectFilePath("Temp"));
            File.WriteAllText(reportFile, report, Encoding.UTF8);
        }

        private static string ProjectFilePath(string relativeProjectPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string normalized = relativeProjectPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, normalized);
        }
    }
}
#endif
