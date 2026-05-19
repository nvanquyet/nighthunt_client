// NightHuntInputSetupTool.cs
// Unity Editor tool — Menu: NightHunt / Input / Setup Input Architecture (Phase 4)
//
// Handles Phase 4 tasks automatically:
//   4A. CameraLockIndicator wiring is now code-driven in GameHUDController (no manual wiring needed).
//   4B. Wire ControlsSettingsPanel._resetAllBindingsButton → ResetAllBindings()
//   4C. Wire PlayerInput Gamepad control scheme on InputLayerManager GameObject
//   4D. Scan and report obsolete components (GameHUD, UIRootController, old input bridges)
//   4E. Cleanup: remove [Obsolete] / legacy input components from scene GameObjects

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NightHunt.UI.Settings;
using NightHunt.Gameplay.Input.Core;
using NightHunt.UI;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// One-click setup for the new GameActionBus input architecture.
    /// Run from: NightHunt → Input → Setup Input Architecture
    /// </summary>
    public static class NightHuntInputSetupTool
    {
        private const string MenuRoot = "NightHunt/Input/";

        // ── Entry points ────────────────────────────────────────────────────────

        [MenuItem(MenuRoot + "⚡ Run Full Input Phase 4 Setup", priority = 0)]
        public static void RunFullSetup()
        {
            int fixes = 0;
            fixes += WireControlsPanelResetButton();
            fixes += AssignGamepadSchemeToPlayerInput();
            fixes += ScanAndRemoveObsoleteComponents();
            fixes += ValidateCameraLockIndicator();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[NightHuntInputSetupTool] ✅ Phase 4 setup complete — {fixes} fix(es) applied.");
            EditorUtility.DisplayDialog(
                "NightHunt Input Setup Complete",
                $"Phase 4 setup applied {fixes} fix(es).\n\nSee Console for full report.",
                "OK");
        }

        [MenuItem(MenuRoot + "4B · Wire ResetAllBindings Buttons", priority = 10)]
        public static void RunWireResetButtons()
        {
            int n = WireControlsPanelResetButton();
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[NightHuntInputSetupTool] 4B complete — {n} button(s) wired.");
        }

        [MenuItem(MenuRoot + "4C · Assign Gamepad Control Scheme (PlayerInput)", priority = 11)]
        public static void RunAssignGamepadScheme()
        {
            int n = AssignGamepadSchemeToPlayerInput();
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[NightHuntInputSetupTool] 4C complete — {n} PlayerInput component(s) updated.");
        }

        [MenuItem(MenuRoot + "4D · Scan + Remove Obsolete Components", priority = 12)]
        public static void RunCleanupObsoleteComponents()
        {
            int n = ScanAndRemoveObsoleteComponents();
            if (n > 0) EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[NightHuntInputSetupTool] 4D complete — {n} obsolete component(s) removed.");
        }

        [MenuItem(MenuRoot + "4A · Validate CameraLockIndicator", priority = 13)]
        public static void RunValidateCameraLock()
        {
            int n = ValidateCameraLockIndicator();
            Debug.Log($"[NightHuntInputSetupTool] 4A complete — {n} indicator(s) validated.");
        }

        [MenuItem(MenuRoot + "Audit → Print Input Component Report", priority = 50)]
        public static void PrintInputComponentReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== NightHunt Input Component Report ===\n");

            // ControlsSettingsPanel
            var panels = FindAllObjects<ControlsSettingsPanel>();
            sb.AppendLine($"[ControlsSettingsPanel] Found: {panels.Count}");
            foreach (var p in panels)
            {
                var resetBtn = GetPrivateField<Button>(p, "_resetAllBindingsButton");
                sb.AppendLine($"  └ {GetPath(p)} | _resetAllBindingsButton={(resetBtn != null ? "WIRED" : "NULL")}");
            }

            // InputLayerManager + PlayerInput
            var ilms = FindAllObjects<InputLayerManager>();
            sb.AppendLine($"\n[InputLayerManager] Found: {ilms.Count}");
            foreach (var ilm in ilms)
            {
                var pi = ilm.GetComponent<PlayerInput>();
                sb.AppendLine($"  └ {GetPath(ilm)} | PlayerInput={(pi != null ? "YES" : "NONE")}");
                if (pi != null)
                    sb.AppendLine($"      defaultScheme='{pi.defaultControlScheme}' currentScheme='{pi.currentControlScheme}'");
            }

            // CameraLockIndicator
            var indicators = FindAllObjects<CameraLockIndicator>();
            sb.AppendLine($"\n[CameraLockIndicator] Found: {indicators.Count}");
            foreach (var ind in indicators)
                sb.AppendLine($"  └ {GetPath(ind)}");

            // Obsolete GameHUD
            var obsolete = FindObsoleteComponents();
            sb.AppendLine($"\n[Obsolete Components] Found: {obsolete.Count}");
            foreach (var c in obsolete)
                sb.AppendLine($"  └ {GetPath(c)} [{c.GetType().Name}]");

            Debug.Log(sb.ToString());
        }

        // ── 4A: Validate CameraLockIndicator ───────────────────────────────────

        /// <summary>
        /// Verifies CameraLockIndicator exists somewhere under a HUD canvas.
        /// Does NOT auto-create (requires designer to place the GO + assign icons).
        /// Prints a warning if missing.
        /// </summary>
        private static int ValidateCameraLockIndicator()
        {
            var found = FindAllObjects<CameraLockIndicator>();
            if (found.Count == 0)
            {
                Debug.LogWarning(
                    "[4A] CameraLockIndicator not found in any open scene. " +
                    "Please add a CameraLockIndicator component to a child GameObject " +
                    "inside the HUD Canvas and assign its icon/label fields in the Inspector. " +
                    "GameHUDController will auto-bind it at runtime via GetComponentInChildren.");
                return 0;
            }

            foreach (var ind in found)
                Debug.Log($"[4A] ✅ CameraLockIndicator found at '{GetPath(ind)}'. " +
                          "GameHUDController.BindMobileInput() will auto-bind at runtime.");
            return found.Count;
        }

        // ── 4B: Wire ResetAllBindings button ───────────────────────────────────

        private static int WireControlsPanelResetButton()
        {
            int count = 0;
            var panels = FindAllObjects<ControlsSettingsPanel>();
            if (panels.Count == 0)
            {
                Debug.LogWarning("[4B] No ControlsSettingsPanel found in open scenes.");
                return 0;
            }

            foreach (var panel in panels)
            {
                var btn = GetPrivateField<Button>(panel, "_resetAllBindingsButton");
                if (btn == null)
                {
                    // Try to auto-find by name convention inside the same GameObject
                    btn = FindButtonByNameInHierarchy(panel.gameObject, "ResetAllBindings", "ResetBindings", "ResetAll");
                    if (btn != null)
                    {
                        SetPrivateField(panel, "_resetAllBindingsButton", btn);
                        EditorUtility.SetDirty(panel);
                        Debug.Log($"[4B] Auto-assigned _resetAllBindingsButton on '{GetPath(panel)}' → '{btn.name}'.");
                    }
                    else
                    {
                        Debug.LogWarning($"[4B] Cannot find ResetAllBindings button on '{GetPath(panel)}'. " +
                                         "Create a Button named 'Btn_ResetAllBindings' as a child and re-run.");
                        continue;
                    }
                }

                // Wire onClick → ResetAllBindings() if not already wired
                var onClick = btn.onClick;
                bool alreadyWired = IsMethodAlreadyWired(onClick, panel, "ResetAllBindings");
                if (!alreadyWired)
                {
                    Undo.RecordObject(btn, "Wire ResetAllBindings");
                    var method = panel.GetType().GetMethod("ResetAllBindings",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        UnityEventTools.AddPersistentListener(onClick,
                            (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), panel, method));
                        EditorUtility.SetDirty(btn);
                        Debug.Log($"[4B] ✅ Wired '{btn.name}'.onClick → ControlsSettingsPanel.ResetAllBindings() on '{GetPath(panel)}'.");
                        count++;
                    }
                    else
                    {
                        Debug.LogError($"[4B] ResetAllBindings() method not found on ControlsSettingsPanel. Did the method get renamed?");
                    }
                }
                else
                {
                    Debug.Log($"[4B] Already wired: '{btn.name}'.onClick → ResetAllBindings() on '{GetPath(panel)}'.");
                }
            }
            return count;
        }

        // ── 4C: Assign Gamepad control scheme to PlayerInput ───────────────────

        private static int AssignGamepadSchemeToPlayerInput()
        {
            int count = 0;
            var ilms = FindAllObjects<InputLayerManager>();

            if (ilms.Count == 0)
            {
                // Also search all scene GameObjects named InputManager
                var allPIs = FindAllObjects<PlayerInput>();
                foreach (var pi in allPIs)
                {
                    count += ApplyGamepadScheme(pi);
                }
                if (count == 0)
                    Debug.LogWarning("[4C] No InputLayerManager or PlayerInput found in open scenes.");
                return count;
            }

            foreach (var ilm in ilms)
            {
                var pi = ilm.GetComponent<PlayerInput>();
                if (pi == null)
                {
                    // PlayerInput is optional — InputLayerManager drives maps manually
                    Debug.Log($"[4C] InputLayerManager at '{GetPath(ilm)}' has no PlayerInput — skipping (manual map control is fine).");
                    continue;
                }
                count += ApplyGamepadScheme(pi);
            }
            return count;
        }

        private static int ApplyGamepadScheme(PlayerInput pi)
        {
            Undo.RecordObject(pi, "Set Gamepad Default Scheme");

            // Verify the action asset actually has a Gamepad scheme
            if (pi.actions == null)
            {
                Debug.LogWarning($"[4C] PlayerInput at '{GetPath(pi)}' has no InputActionAsset assigned.");
                return 0;
            }

            bool hasGamepadScheme = false;
            foreach (var scheme in pi.actions.controlSchemes)
            {
                if (scheme.name == "Gamepad") { hasGamepadScheme = true; break; }
            }

            if (!hasGamepadScheme)
            {
                Debug.LogWarning($"[4C] InputActionAsset '{pi.actions.name}' does not contain a 'Gamepad' control scheme. " +
                                 "Re-import the .inputactions file first (Unity → Assets → Reimport).");
                return 0;
            }

            // Set default scheme — Unity will auto-switch based on connected device
            pi.defaultControlScheme = "Gamepad";
            // Notify actions to update current scheme
            pi.SwitchCurrentControlScheme("Gamepad", Gamepad.all.ToArray());

            EditorUtility.SetDirty(pi);
            Debug.Log($"[4C] ✅ PlayerInput at '{GetPath(pi)}' — defaultControlScheme set to 'Gamepad'. " +
                      "Unity Input System will auto-switch between Keyboard&Mouse / Gamepad based on device.");
            return 1;
        }

        // ── 4D: Remove obsolete components ─────────────────────────────────────

        private static readonly string[] ObsoleteTypeNames = new[]
        {
            // Legacy classes replaced by GameHUDController
            "NightHunt.UI.GameHUD",
            "NightHunt.GameplaySystems.UI.Inventory.UIRootController",
            // Legacy simulation bridges replaced by GameActionBus
            "NightHunt.Gameplay.Input.SimulationInputBridge",
            "NightHunt.Gameplay.Input.LegacyInputBridge",
            "NightHunt.Gameplay.Input.Handlers.LegacyQuickSlotBridge",
            // Old MonoBehaviours that were superseded
            "NightHunt.UI.ItemSelectionSimBridge",
        };

        private static int ScanAndRemoveObsoleteComponents()
        {
            int removed = 0;
            var allBehaviours = FindObsoleteComponents();

            if (allBehaviours.Count == 0)
            {
                Debug.Log("[4D] ✅ No obsolete components found. Scene is clean.");
                return 0;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Obsolete Components?",
                $"Found {allBehaviours.Count} obsolete component(s):\n\n" +
                BuildComponentList(allBehaviours) +
                "\n\nRemove them now? (Undo is available)",
                "Remove All", "Skip");

            if (!confirmed) return 0;

            foreach (var comp in allBehaviours)
            {
                if (comp == null) continue;
                Debug.Log($"[4D] Removing obsolete [{comp.GetType().Name}] from '{GetPath(comp)}'.");
                Undo.DestroyObjectImmediate(comp);
                removed++;
            }

            return removed;
        }

        private static List<Component> FindObsoleteComponents()
        {
            var result = new List<Component>();

            // Use type name matching since obsolete types may not compile
            foreach (var typeName in ObsoleteTypeNames)
            {
                var type = FindTypeByFullName(typeName);
                if (type == null) continue;

                var found = UnityEngine.Object.FindObjectsByType(type,
                    FindObjectsSortMode.None) as Component[];
                if (found != null)
                    result.AddRange(found);
            }

            // Also scan for MonoBehaviours with [Obsolete] attribute
            var allMbs = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMbs)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.GetCustomAttribute<ObsoleteAttribute>() != null)
                {
                    // Avoid double-adding
                    if (!result.Contains(mb))
                        result.Add(mb);
                }
            }

            return result;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static List<T> FindAllObjects<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_2_OR_NEWER
            var arr = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            var arr = UnityEngine.Object.FindObjectsOfType<T>();
#endif
            return new List<T>(arr);
        }

        private static Button FindButtonByNameInHierarchy(GameObject root, params string[] names)
        {
            var allButtons = root.GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                foreach (var n in names)
                {
                    if (btn.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                        return btn;
                }
            }
            return null;
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return field?.GetValue(target) as T;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            field?.SetValue(target, value);
        }

        private static bool IsMethodAlreadyWired(UnityEvent evt, object target, string methodName)
        {
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
            {
                if (evt.GetPersistentTarget(i) == (UnityEngine.Object)target &&
                    evt.GetPersistentMethodName(i) == methodName)
                    return true;
            }
            return false;
        }

        private static string GetPath(Component c)
        {
            if (c == null) return "(null)";
            var go = c.gameObject;
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return $"[{go.scene.name}] {path}";
        }

        private static Type FindTypeByFullName(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static string BuildComponentList(List<Component> comps)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in comps)
                sb.AppendLine($"  • [{c.GetType().Name}] on {c.gameObject.name}");
            return sb.ToString();
        }

        private static Gamepad[] ToArray(this IReadOnlyList<Gamepad> list)
        {
            var arr = new Gamepad[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i];
            return arr;
        }
    }
}
#endif
