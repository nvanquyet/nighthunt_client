using System;
using System.Threading.Tasks;
using NightHunt.Audio;
using NightHunt.Config;
using NightHunt.Gameplay.Input.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NightHunt.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class SettingsView : MonoBehaviour, INavigableView
    {
        public event Action CloseRequested;

        [Header("Panels")]
        [SerializeField] private GameObject gameplaySettingsRoot;
        [SerializeField] private GameObject audioSettingsRoot;
        [SerializeField] private GameObject controlsSettingsRoot;
        [SerializeField] private GameObject graphicsSettingsRoot;
        [SerializeField] private GameplaySettingsPanel gameplaySettingsPanel;
        [SerializeField] private AudioSettingsPanel audioSettingsPanel;
        [SerializeField] private ControlsSettingsPanel controlsSettingsPanel;
        [SerializeField] private GraphicsSettingsPanel graphicsSettingsPanel;

        [Header("Manual Close")]
        [SerializeField] private Button closeButton;

        private GameObject runtimeSettingsRoot;
        private TextMeshProUGUI demoObjectCountLabel;
        private int demoObjectCount = 2;
        private NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler uiInputHandler;
        private bool controlsBindingsGenerated;

        public bool CanLeave(NavigationContext context) => true;

        public Task OnShowAsync(NavigationContext context)
        {
            ResolveReferences();
            EnsureRuntimeSettingsContent();
            HookCancelInput();

            gameplaySettingsPanel?.RefreshFromPrefs();
            audioSettingsPanel?.RefreshFromPrefs();
            controlsSettingsPanel?.RefreshFromPrefs();
            graphicsSettingsPanel?.RefreshFromPrefs();
            EnsureControlsBindingsContent();
            ShowGameplay();

            // Push input context so Escape works
            NightHunt.Gameplay.Input.Core.InputLayerManager.Instance?.PushContext(NightHunt.Gameplay.Input.InputState.Paused);

            return Task.CompletedTask;
        }

        public Task OnHideAsync(NavigationContext context)
        {
            UnhookCancelInput();
            // Pop input context
            NightHunt.Gameplay.Input.Core.InputLayerManager.Instance?.PopContext();
            return Task.CompletedTask;
        }

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
        }

        private void OnDestroy()
        {
            UnhookCancelInput();

            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
        }

        private void HandleCancel()
        {
            // If this panel is the currently active one in UINavigator, go back.
            if (UINavigator.Instance?.CurrentPanel == PanelType.Settings)
            {
                UINavigator.Instance.GoBack();
                return;
            }

            CloseRequested?.Invoke();
        }

        private void HandleCloseButtonClicked()
        {
            HandleCancel();
        }

        private void HookCancelInput()
        {
            var found = FindFirstObjectByType<NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler>(FindObjectsInactive.Include);
            if (found == null)
                return;

            if (uiInputHandler == found)
                return;

            UnhookCancelInput();
            uiInputHandler = found;
            uiInputHandler.OnCancelPressed += HandleCancel;
        }

        private void UnhookCancelInput()
        {
            if (uiInputHandler == null)
                return;

            uiInputHandler.OnCancelPressed -= HandleCancel;
            uiInputHandler = null;
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (gameplaySettingsPanel == null)
                gameplaySettingsPanel = GetComponentInChildren<GameplaySettingsPanel>(true);
            if (audioSettingsPanel == null)
                audioSettingsPanel = GetComponentInChildren<AudioSettingsPanel>(true);
            if (controlsSettingsPanel == null)
                controlsSettingsPanel = GetComponentInChildren<ControlsSettingsPanel>(true);
            if (graphicsSettingsPanel == null)
                graphicsSettingsPanel = GetComponentInChildren<GraphicsSettingsPanel>(true);

            if (gameplaySettingsRoot == null && gameplaySettingsPanel != null)
                gameplaySettingsRoot = gameplaySettingsPanel.gameObject;
            if (audioSettingsRoot == null && audioSettingsPanel != null)
                audioSettingsRoot = audioSettingsPanel.gameObject;
            if (controlsSettingsRoot == null && controlsSettingsPanel != null)
                controlsSettingsRoot = controlsSettingsPanel.gameObject;
            if (graphicsSettingsRoot == null && graphicsSettingsPanel != null)
                graphicsSettingsRoot = graphicsSettingsPanel.gameObject;

            // Fallback for roots if panels are missing but objects exist
            if (gameplaySettingsRoot == null) gameplaySettingsRoot = FindChildObject("Gameplay");
            if (audioSettingsRoot == null) audioSettingsRoot = FindChildObject("Audio");
            if (controlsSettingsRoot == null) controlsSettingsRoot = FindChildObject("Controls");
            if (graphicsSettingsRoot == null) graphicsSettingsRoot = FindChildObject("Visuals") ?? FindChildObject("Graphics");
        }

        private void EnsureRuntimeSettingsContent()
        {
            if (runtimeSettingsRoot != null)
                return;

            if (HasConcreteSettingsPanelContent())
                return;

            var root = CreateUIObject("Runtime Settings Content", transform);
            runtimeSettingsRoot = root;
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.16f);
            rt.anchorMax = new Vector2(0.58f, 0.84f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            gameplaySettingsRoot = CreatePanel(root.transform, "Gameplay Runtime Panel");
            audioSettingsRoot = CreatePanel(root.transform, "Audio Runtime Panel");
            controlsSettingsRoot = CreatePanel(root.transform, "Controls Runtime Panel");
            graphicsSettingsRoot = CreatePanel(root.transform, "Visuals Runtime Panel");

            BuildGameplayContent(gameplaySettingsRoot.transform);
            BuildAudioContent(audioSettingsRoot.transform);
            BuildControlsContent(controlsSettingsRoot.transform);
            controlsBindingsGenerated = true;
            BuildVisualsContent(graphicsSettingsRoot.transform);
        }

        private bool HasConcreteSettingsPanelContent()
        {
            return HasPanelControls(gameplaySettingsPanel != null ? gameplaySettingsPanel.gameObject : gameplaySettingsRoot)
                || HasPanelControls(audioSettingsPanel != null ? audioSettingsPanel.gameObject : audioSettingsRoot)
                || HasPanelControls(controlsSettingsPanel != null ? controlsSettingsPanel.gameObject : controlsSettingsRoot)
                || HasPanelControls(graphicsSettingsPanel != null ? graphicsSettingsPanel.gameObject : graphicsSettingsRoot);
        }

        private static bool HasPanelControls(GameObject root)
        {
            if (root == null) return false;
            return root.GetComponentInChildren<Slider>(true) != null
                || root.GetComponentInChildren<Toggle>(true) != null
                || root.GetComponentInChildren<TMP_Dropdown>(true) != null;
        }

        private GameObject CreatePanel(Transform parent, string name)
        {
            var panel = CreateUIObject(name, parent);
            Stretch(panel.GetComponent<RectTransform>());
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.06f, 0.18f, 0.24f, 0.54f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 14f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return panel;
        }

        private void BuildGameplayContent(Transform parent)
        {
            CreateTitle(parent, "Gameplay");
            CreateToggle(parent, "Tutorial Tips", GameSettings.Instance?.EnableTutorials ?? true, on =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.EnableTutorials = on;
                GameSettings.Instance.SaveSettings();
            });
            CreateToggle(parent, "Quick Swap", GameSettings.Instance?.QuickSwap ?? true, on =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.QuickSwap = on;
                GameSettings.Instance.SaveSettings();
            });
            CreateSlider(parent, "UI Scale", 0.8f, 1.4f, GameSettings.Instance?.UIScale ?? 1f, value =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.UIScale = value;
                GameSettings.Instance.SaveSettings();
                GlobalUIController.Instance?.ApplyUIScale(value);
            });

            CreateTitle(parent, "Scene Demo Objects");
            demoObjectCountLabel = CreateLabel(parent, $"Objects: {demoObjectCount}", 22f, TextAlignmentOptions.Left);
            var row = CreateRow(parent, 54f);
            CreateButton(row.transform, "Add", () => { demoObjectCount++; UpdateDemoObjectCount(); });
            CreateButton(row.transform, "Edit", () => { demoObjectCount = Mathf.Max(1, demoObjectCount); UpdateDemoObjectCount("Edited"); });
            CreateButton(row.transform, "Delete", () => { demoObjectCount = Mathf.Max(0, demoObjectCount - 1); UpdateDemoObjectCount(); });
        }

        private void BuildAudioContent(Transform parent)
        {
            CreateTitle(parent, "Audio");
            CreateSlider(parent, "Master Volume", 0f, 1f, AudioListener.volume, value => AudioListener.volume = value);
            CreateToggle(parent, "Mute In Background", false, value => AudioListener.pause = value);
            CreateButton(CreateRow(parent, 54f).transform, "Reset Audio", () => AudioListener.volume = 1f);
        }

        private void BuildMobileControlsContent(Transform parent)
        {
            CreateTitle(parent, "Mobile");
            CreateSlider(parent, "Mobile Camera Sensitivity", 1f, 5f, GameSettings.Instance?.MobileCameraDegreesPerPixel ?? 3f, value =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.MobileCameraDegreesPerPixel = value;
                GameSettings.Instance.SaveSettings();
            });
        }

        private void EnsureControlsBindingsContent()
        {
            if (controlsBindingsGenerated)
                return;

            var contentRoot = ResolveControlsBindingsRoot();
            if (contentRoot == null)
                return;

            if (contentRoot.GetComponentInChildren<RebindActionUI>(true) != null)
            {
                controlsBindingsGenerated = true;
                return;
            }

            var asset = InputLayerManager.Instance?.Config?.InputActionAsset;
            if (asset == null)
            {
                CreateLabel(contentRoot,
                    "<color=#FF8C60>Key bindings unavailable\n(InputLayerManager not found in scene)</color>",
                    16f, TextAlignmentOptions.Left);
                controlsBindingsGenerated = true;
                return;
            }

            BuildKeyBindingsSection(contentRoot, asset);
            controlsBindingsGenerated = true;
        }

        private Transform ResolveControlsBindingsRoot()
        {
            if (controlsSettingsPanel != null)
            {
                var content = controlsSettingsPanel.transform.Find("Content/List/List Content");
                if (content != null)
                    return content;
            }

            if (controlsSettingsRoot != null)
            {
                var content = controlsSettingsRoot.transform.Find("Content/List/List Content");
                if (content != null)
                    return content;

                return controlsSettingsRoot.transform;
            }

            return null;
        }

        private void BuildKeyBindingsSection(Transform parent, InputActionAsset asset)
        {
            CreateTitle(parent, "Key Bindings");
            var resetAllRow = CreateRow(parent, 50f);
            CreateButton(resetAllRow.transform, "Reset All Bindings", () =>
            {
                InputBindingSaveSystem.ResetAllBindings(asset);
                if (controlsSettingsPanel != null)
                    controlsSettingsPanel.RefreshFromPrefs();
                Debug.Log("[SettingsView] All key bindings reset to defaults.");
            });

            var mapsToShow = new[] { "Player", "Combat", "Inventory", "Camera", "Team" };
            foreach (var mapName in mapsToShow)
            {
                var map = asset.FindActionMap(mapName, throwIfNotFound: false);
                if (map == null) continue;

                CreateKeyBindingSection(parent, asset, map);
            }
        }

        private void BuildControlsContent(Transform parent)
        {
            CreateTitle(parent, "Controls");
            BuildMobileControlsContent(parent);
            CreateSlider(parent, "Mouse Sensitivity", 0.1f, 5f, GameSettings.Instance?.MouseSensitivity ?? 1.5f, value =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.MouseSensitivity = value;
                GameSettings.Instance.SaveSettings();
            });
            CreateToggle(parent, "Invert Y", GameSettings.Instance?.InvertY ?? false, on =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.InvertY = on;
                GameSettings.Instance.SaveSettings();
            });

            // ── Key Bindings ────────────────────────────────────────────────
            var asset = InputLayerManager.Instance?.Config?.InputActionAsset;
            if (asset == null)
            {
                CreateLabel(parent,
                    "<color=#FF8C60>Key bindings unavailable\n(InputLayerManager not found in scene)</color>",
                    16f, TextAlignmentOptions.Left);
                return;
            }

            BuildKeyBindingsSection(parent, asset);
        }

        /// <summary>
        /// Builds a collapsible section header + one row per action in the map.
        /// Each row: [Action Name Label] [Current Key Button] [Reset X Button]
        /// </summary>
        private void CreateKeyBindingSection(Transform parent, InputActionAsset asset, InputActionMap map)
        {
            // Section header
            var header = CreateLabel(parent, map.name, 22f, TextAlignmentOptions.Left);
            header.color = new Color(0.60f, 0.92f, 1f, 1f);
            header.GetComponent<LayoutElement>().minHeight = 36f;

            // Divider line
            var divider = CreateUIObject("Divider", parent);
            divider.AddComponent<Image>().color = new Color(0.35f, 0.82f, 1f, 0.25f);
            divider.AddComponent<LayoutElement>().minHeight = 1f;

            foreach (var action in map.actions)
            {
                // Skip axis/value actions (Move, MouseDelta, etc.) — only rebind buttons
                if (action.type == InputActionType.Value || action.type == InputActionType.PassThrough)
                    continue;

                // Find first Keyboard&Mouse binding index
                int bindingIdx = -1;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite || b.isPartOfComposite) continue;
                    if (b.groups.Contains("Keyboard") || b.groups.Contains("Keyboard&Mouse") || string.IsNullOrEmpty(b.groups))
                    {
                        bindingIdx = i;
                        break;
                    }
                }
                if (bindingIdx < 0) continue;

                CreateRebindRow(parent, asset, action, bindingIdx);
            }
        }

        /// <summary>
        /// Single key-binding row: [Action Name] [Key Button → click to rebind] [X reset]
        /// Uses a local RebindingOperation — no RebindActionUI MonoBehaviour required,
        /// but compatible with InputBindingSaveSystem for persistence.
        /// </summary>
        private void CreateRebindRow(Transform parent, InputActionAsset asset,
                                     InputAction action, int bindingIndex)
        {
            var row = CreateRow(parent, 52f);

            // Action name label
            var nameLabel = CreateLabel(row.transform, FormatActionName(action.name), 18f, TextAlignmentOptions.Left);
            nameLabel.GetComponent<LayoutElement>().preferredWidth = 200f;

            // Key button — shows current binding, click to start rebind
            var keyBtnGo = CreateUIObject("KeyBtn_" + action.name, row.transform);
            var keyBtnLayout = keyBtnGo.AddComponent<LayoutElement>();
            keyBtnLayout.minWidth = 130f;
            keyBtnLayout.preferredWidth = 130f;

            var keyBtnImg = keyBtnGo.AddComponent<Image>();
            keyBtnImg.color = new Color(0.08f, 0.22f, 0.30f, 1f);

            var keyBtn = keyBtnGo.AddComponent<Button>();
            keyBtn.targetGraphic = keyBtnImg;

            var keyLabel = CreateLabel(keyBtnGo.transform, GetBindingDisplayName(action, bindingIndex), 17f, TextAlignmentOptions.Center);
            keyLabel.color = new Color(0.90f, 0.95f, 1f, 1f);
            Stretch(keyLabel.GetComponent<RectTransform>());

            // Waiting overlay (shows "Press a key..." during rebind)
            var waitingGo = CreateUIObject("Waiting", keyBtnGo.transform);
            Stretch(waitingGo.GetComponent<RectTransform>());
            var waitingImg = waitingGo.AddComponent<Image>();
            waitingImg.color = new Color(0.15f, 0.45f, 0.60f, 0.95f);
            var waitingLabel = CreateLabel(waitingGo.transform, "Press a key…", 15f, TextAlignmentOptions.Center);
            waitingLabel.color = Color.white;
            Stretch(waitingLabel.GetComponent<RectTransform>());
            waitingGo.SetActive(false);

            // Reset (X) button
            var resetBtnGo = CreateUIObject("ResetBtn_" + action.name, row.transform);
            resetBtnGo.AddComponent<LayoutElement>().minWidth = 40f;
            var resetImg = resetBtnGo.AddComponent<Image>();
            resetImg.color = new Color(0.55f, 0.12f, 0.12f, 0.85f);
            var resetBtn = resetBtnGo.AddComponent<Button>();
            resetBtn.targetGraphic = resetImg;
            var resetLabel = CreateLabel(resetBtnGo.transform, "✕", 18f, TextAlignmentOptions.Center);
            Stretch(resetLabel.GetComponent<RectTransform>());

            // State for the closure
            InputActionRebindingExtensions.RebindingOperation[] opHolder = { null };

            // Key button click → start interactive rebind
            keyBtn.onClick.AddListener(() =>
            {
                if (opHolder[0] != null) return; // already rebinding

                waitingGo.SetActive(true);
                keyBtn.interactable = false;
                action.Disable();

                opHolder[0] = action
                    .PerformInteractiveRebinding(bindingIndex)
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/delta")
                    .OnMatchWaitForAnother(0.1f)
                    .OnComplete(_ =>
                    {
                        opHolder[0]?.Dispose();
                        opHolder[0] = null;
                        action.Enable();
                        waitingGo.SetActive(false);
                        keyBtn.interactable = true;
                        keyLabel.text = GetBindingDisplayName(action, bindingIndex);
                        InputBindingSaveSystem.SaveBindings(asset);
                    })
                    .OnCancel(_ =>
                    {
                        opHolder[0]?.Dispose();
                        opHolder[0] = null;
                        action.Enable();
                        waitingGo.SetActive(false);
                        keyBtn.interactable = true;
                    })
                    .Start();
            });

            // Reset button click → remove override
            resetBtn.onClick.AddListener(() =>
            {
                action.RemoveBindingOverride(bindingIndex);
                keyLabel.text = GetBindingDisplayName(action, bindingIndex);
                InputBindingSaveSystem.SaveBindings(asset);
            });
        }

        private static string GetBindingDisplayName(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return "—";

            string path = action.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrEmpty(path)) return "—";

            int slash = path.LastIndexOf('/');
            string name = slash >= 0 ? path.Substring(slash + 1) : path;
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[<>]", string.Empty);
            if (name.Length > 0) name = char.ToUpper(name[0]) + name.Substring(1);

            return name switch
            {
                "LeftButton"   => "LMB",
                "RightButton"  => "RMB",
                "MiddleButton" => "MMB",
                "LeftShift"    => "L.Shift",
                "RightShift"   => "R.Shift",
                "LeftCtrl"     => "L.Ctrl",
                "LeftAlt"      => "L.Alt",
                _              => name
            };
        }

        private static string FormatActionName(string actionName)
        {
            // "WeaponSlot1" → "Weapon Slot 1" ; "AimDownSights" → "Aim Down Sights"
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < actionName.Length; i++)
            {
                char c = actionName[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(actionName[i - 1]))
                    sb.Append(' ');
                else if (i > 0 && char.IsDigit(c) && !char.IsDigit(actionName[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void BuildVisualsContent(Transform parent)
        {
            CreateTitle(parent, "Visuals");
            CreateSlider(parent, "Field of View", 60f, 110f, GameSettings.Instance?.FOV ?? 75f, value =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.FOV = value;
                GameSettings.Instance.SaveSettings();
            });
            CreateToggle(parent, "V-Sync", GameSettings.Instance?.VSync ?? true, on =>
            {
                if (GameSettings.Instance == null) return;
                GameSettings.Instance.VSync = on;
                GameSettings.Instance.ApplySettings();
                GameSettings.Instance.SaveSettings();
            });
        }

        private void UpdateDemoObjectCount(string prefix = null)
        {
            if (demoObjectCountLabel == null) return;
            demoObjectCountLabel.text = string.IsNullOrEmpty(prefix)
                ? $"Objects: {demoObjectCount}"
                : $"{prefix}: {demoObjectCount}";
        }

        private void CreateTitle(Transform parent, string text)
        {
            var label = CreateLabel(parent, text, 28f, TextAlignmentOptions.Left);
            label.color = new Color(0.45f, 0.85f, 1f, 1f);
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var go = CreateUIObject("Label", parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.Normal;
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = Mathf.Max(32f, size + 10f);
            return label;
        }

        private void CreateSlider(Transform parent, string label, float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = CreateRow(parent, 64f);
            CreateLabel(row.transform, label, 20f, TextAlignmentOptions.Left).GetComponent<LayoutElement>().preferredWidth = 210f;

            var sliderGo = CreateUIObject("Slider", row.transform);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            sliderGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
            BuildSliderVisual(sliderGo.transform, slider);

            var valueLabel = CreateLabel(row.transform, value.ToString("F1"), 18f, TextAlignmentOptions.Right);
            valueLabel.GetComponent<LayoutElement>().preferredWidth = 70f;
            slider.onValueChanged.AddListener(v =>
            {
                valueLabel.text = v.ToString("F1");
                onChanged?.Invoke(v);
            });
        }

        private void CreateToggle(Transform parent, string label, bool value, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var row = CreateRow(parent, 54f);
            CreateLabel(row.transform, label, 20f, TextAlignmentOptions.Left).GetComponent<LayoutElement>().flexibleWidth = 1f;
            var toggleGo = CreateUIObject("Toggle", row.transform);
            toggleGo.AddComponent<LayoutElement>().preferredWidth = 70f;
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.isOn = value;
            var bg = toggleGo.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.28f, 0.36f, 0.95f);
            toggle.targetGraphic = bg;
            var check = CreateUIObject("Check", toggleGo.transform);
            Stretch(check.GetComponent<RectTransform>());
            var checkImage = check.AddComponent<Image>();
            checkImage.color = new Color(0.35f, 0.82f, 1f, 1f);
            toggle.graphic = checkImage;
            toggle.onValueChanged.AddListener(onChanged);
        }

        private GameObject CreateRow(Transform parent, float height)
        {
            var row = CreateUIObject("Row", parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            row.AddComponent<LayoutElement>().minHeight = height;
            return row;
        }

        private Button CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = CreateUIObject(text + " Button", parent);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.24f, 0.31f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            go.AddComponent<NightHunt.UI.NH_Button>();
            var layout = go.AddComponent<LayoutElement>();
            layout.minWidth = 120f;
            layout.flexibleWidth = 1f;
            var label = CreateLabel(go.transform, text, 18f, TextAlignmentOptions.Center);
            Stretch(label.GetComponent<RectTransform>());
            return button;
        }

        private void BuildSliderVisual(Transform parent, Slider slider)
        {
            var background = CreateUIObject("Background", parent);
            Stretch(background.GetComponent<RectTransform>());
            background.AddComponent<Image>().color = new Color(0.08f, 0.2f, 0.26f, 1f);

            var fillArea = CreateUIObject("Fill Area", parent);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = CreateUIObject("Fill", fillArea.transform);
            Stretch(fill.GetComponent<RectTransform>());
            fill.AddComponent<Image>().color = new Color(0.35f, 0.82f, 1f, 1f);

            var handleArea = CreateUIObject("Handle Slide Area", parent);
            Stretch(handleArea.GetComponent<RectTransform>());
            var handle = CreateUIObject("Handle", handleArea.transform);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(20f, 32f);
            handle.AddComponent<Image>().color = Color.white;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void ShowGameplay()
        {
            ResolveReferences();
            ShowSettingsPanel(gameplaySettingsRoot, "Gameplay");
        }

        public void ShowAudio()
        {
            ResolveReferences();
            ShowSettingsPanel(audioSettingsRoot, "Audio");
        }

        public void ShowControls()
        {
            ResolveReferences();
            ShowSettingsPanel(controlsSettingsRoot, "Controls");
        }

        public void ShowVisuals()
        {
            ResolveReferences();
            ShowSettingsPanel(graphicsSettingsRoot, "Visuals");
        }

        private void ShowSettingsPanel(GameObject activeRoot, string panelName)
        {
            ResolveReferences();

            if (activeRoot == null)
            {
                Debug.LogWarning($"[SettingsView] Cannot show '{panelName}' settings panel because its root is not assigned.");
                return;
            }

            SetPanelActive(gameplaySettingsRoot, activeRoot);
            SetPanelActive(audioSettingsRoot, activeRoot);
            SetPanelActive(controlsSettingsRoot, activeRoot);
            SetPanelActive(graphicsSettingsRoot, activeRoot);
        }

        private static void SetPanelActive(GameObject root, GameObject activeRoot)
        {
            if (root != null && root.activeSelf != (root == activeRoot))
                root.SetActive(root == activeRoot);
        }

        private GameObject FindChildObject(string childName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
            }

            return null;
        }
    }
}
