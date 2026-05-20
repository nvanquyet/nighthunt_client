#if UNITY_EDITOR
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.UI;

namespace NightHunt.UI.Editor
{
    /// <summary>
    /// One-click tool that creates and wires the PlayerProfilePanel hierarchy
    /// inside the currently open Home scene.
    ///
    /// Menu: Tools → NightHunt → Setup PlayerProfilePanel in Home Scene
    ///
    /// What it does:
    ///   1. Finds the Canvas in the open scene.
    ///   2. Creates the full PlayerProfilePanel UI subtree as the LAST sibling
    ///      of Canvas children (renders on top of everything else).
    ///   3. Wires all [SerializeField] references via Reflection so the
    ///      component is immediately usable without any manual Inspector work.
    ///   4. Marks the scene dirty so Unity prompts to save.
    ///
    /// Safe to run multiple times — skips creation if a PlayerProfilePanel
    /// component already exists in the scene.
    /// </summary>
    public static class HomeSceneSetupTool
    {
        // ── Color palette matching the existing NightHunt UI style ─────────
        private static readonly Color BackdropColor       = new Color(0f,    0f,    0f,    0.60f);
        private static readonly Color PanelBgColor        = new Color(0.08f, 0.09f, 0.12f, 0.97f);
        private static readonly Color HeaderBgColor       = new Color(0.05f, 0.06f, 0.09f, 1.00f);
        private static readonly Color SeparatorColor      = new Color(0.20f, 0.55f, 0.85f, 0.60f);
        private static readonly Color AccentBlue          = new Color(0.18f, 0.52f, 0.90f, 1.00f);
        private static readonly Color TextPrimary         = new Color(0.92f, 0.93f, 0.95f, 1.00f);
        private static readonly Color TextSecondary       = new Color(0.55f, 0.60f, 0.68f, 1.00f);
        private static readonly Color CloseBtnColor       = new Color(0.75f, 0.18f, 0.18f, 1.00f);
        private static readonly Color LoadingColor        = new Color(0.18f, 0.52f, 0.90f, 0.85f);

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/NightHunt/Setup PlayerProfilePanel in Home Scene")]
        public static void SetupPlayerProfilePanel()
        {
            // ── 1. Guard: must be editing the Home scene ──────────────────
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.name.Contains("Home") && !activeScene.name.Contains("01"))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Setup PlayerProfilePanel",
                    $"Active scene is '{activeScene.name}', not the Home scene.\n\nProceed anyway?",
                    "Yes, proceed", "Cancel");
                if (!proceed) return;
            }

            // ── 2. Guard: skip if already exists ─────────────────────────
            var existing = Object.FindFirstObjectByType<PlayerProfilePanel>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "Already Exists",
                    $"PlayerProfilePanel already exists on '{existing.gameObject.name}'.\n\nSelect it in the Hierarchy?",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // ── 3. Find Canvas ────────────────────────────────────────────
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Error", "No Canvas found in the open scene.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Setup PlayerProfilePanel");
            int undoGroup = Undo.GetCurrentGroup();

            // ── 4. Build hierarchy ────────────────────────────────────────
            var (panelRoot, backdrop, panel, txtUsername, txtElo, txtTier, txtWinLoss, txtWinRate, btnClose, loadingIndicator)
                = BuildHierarchy(canvas.transform);

            // ── 5. Add & wire PlayerProfilePanel component ────────────────
            var comp = panelRoot.AddComponent<PlayerProfilePanel>();
            Undo.RegisterCreatedObjectUndo(panelRoot, "Create PlayerProfilePanel");

            WireField(comp, "root",             panel);
            WireField(comp, "backdrop",         backdrop.GetComponent<Button>());
            WireField(comp, "txt_Username",     txtUsername);
            WireField(comp, "txt_ELO",          txtElo);
            WireField(comp, "txt_Tier",         txtTier);
            WireField(comp, "txt_WinLoss",      txtWinLoss);
            WireField(comp, "txt_WinRate",      txtWinRate);
            WireField(comp, "btn_Close",        btnClose.GetComponent<Button>());
            WireField(comp, "loadingIndicator", loadingIndicator);

            // ── 6. Wire backdrop button onClick (close) ───────────────────
            var backdropBtn = backdrop.GetComponent<Button>();
            var serializedComp = new SerializedObject(backdropBtn);
            // onClick is wired at runtime in Awake — no Editor wiring needed.

            // ── 7. Select result, mark dirty ──────────────────────────────
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = panelRoot;
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[HomeSceneSetupTool] ✅ PlayerProfilePanel created and wired. Save the scene (Ctrl+S).");
            EditorUtility.DisplayDialog(
                "✅ Done",
                "PlayerProfilePanel has been created in the scene.\n\n" +
                "• All Inspector fields are pre-wired.\n" +
                "• The panel starts INACTIVE (hidden at runtime).\n" +
                "• Save the scene with Ctrl+S.\n\n" +
                "The panel is now selected in the Hierarchy.",
                "OK");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Validation menu — checks for required wiring without creating anything
        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/NightHunt/Validate Home Scene Wiring")]
        public static void ValidateHomeSceneWiring()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== NightHunt Home Scene Wiring Report ===\n");

            CheckComponent<PlayerProfilePanel>(report, "PlayerProfilePanel");

            // Check FriendPanelView, PartyPanelView, SharedPartyContextMenu, PartyCustomModeView
            CheckComponent(report, "FriendPanelView",          typeof(FriendPanelView));
            CheckComponent(report, "PartyPanelView",           typeof(PartyPanelView));
            CheckComponent(report, "SharedPartyContextMenu",   typeof(SharedPartyContextMenu));
            CheckComponent(report, "PartyCustomModeView",          typeof(PartyCustomModeView));

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Wiring Validation", report.ToString(), "OK");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Hierarchy builder
        // ═════════════════════════════════════════════════════════════════════
        private static (
            GameObject root,
            GameObject backdrop,
            GameObject panel,
            TMP_Text txtUsername,
            TMP_Text txtElo,
            TMP_Text txtTier,
            TMP_Text txtWinLoss,
            TMP_Text txtWinRate,
            GameObject btnClose,
            GameObject loadingIndicator
        ) BuildHierarchy(Transform canvasTransform)
        {
            // ── Root (PlayerProfilePanel) — full-screen transparent overlay ──
            var root = CreateGO("PlayerProfilePanel", canvasTransform);
            root.SetActive(false); // starts hidden
            StretchFull(root);

            // ── Backdrop — semi-transparent dim, click to close ───────────
            var backdrop = CreateGO("Backdrop", root.transform);
            StretchFull(backdrop);
            var bdImg = backdrop.AddComponent<Image>();
            bdImg.color = BackdropColor;
            bdImg.raycastTarget = true;
            backdrop.AddComponent<Button>(); // onClick wired at runtime

            // ── Panel card — centered modal ───────────────────────────────
            var panel = CreateGO("Panel", root.transform);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(480f, 540f);
            panelRT.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PanelBgColor;
            panelImg.raycastTarget = true;

            // ── Header bar ───────────────────────────────────────────────
            var header = CreateGO("Header", panel.transform);
            var headerRT = SetRect(header, new Vector2(0,1), new Vector2(1,1), new Vector2(0,1));
            headerRT.sizeDelta        = new Vector2(0, 60f);
            headerRT.anchoredPosition = new Vector2(0, 0);
            var headerImg = header.AddComponent<Image>();
            headerImg.color = HeaderBgColor;

            // Title label in header
            var lblTitle = CreateGO("Lbl_Title", header.transform);
            StretchFull(lblTitle);
            var tmpTitle = lblTitle.AddComponent<TextMeshProUGUI>();
            tmpTitle.text      = "Player Profile";
            tmpTitle.fontSize  = 22f;
            tmpTitle.fontStyle = FontStyles.Bold;
            tmpTitle.color     = TextPrimary;
            tmpTitle.alignment = TextAlignmentOptions.MidlineLeft;
            lblTitle.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 0);
            lblTitle.GetComponent<RectTransform>().offsetMax = new Vector2(-56f, 0);

            // Close button inside header (top-right)
            var btnClose = CreateGO("Btn_Close", header.transform);
            var btnCloseRT = btnClose.GetComponent<RectTransform>();
            btnCloseRT.anchorMin        = new Vector2(1,0.5f);
            btnCloseRT.anchorMax        = new Vector2(1,0.5f);
            btnCloseRT.pivot            = new Vector2(1,0.5f);
            btnCloseRT.sizeDelta        = new Vector2(44f, 44f);
            btnCloseRT.anchoredPosition = new Vector2(-8f, 0);
            var btnCloseImg = btnClose.AddComponent<Image>();
            btnCloseImg.color = CloseBtnColor;
            btnClose.AddComponent<Button>();
            var closeLbl = CreateGO("Lbl", btnClose.transform);
            StretchFull(closeLbl);
            var closeTmp = closeLbl.AddComponent<TextMeshProUGUI>();
            closeTmp.text      = "✕";
            closeTmp.fontSize  = 20f;
            closeTmp.color     = Color.white;
            closeTmp.alignment = TextAlignmentOptions.Center;

            // ── Blue separator line under header ──────────────────────────
            var sep = CreateGO("Separator", panel.transform);
            var sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin        = new Vector2(0,1);
            sepRT.anchorMax        = new Vector2(1,1);
            sepRT.pivot            = new Vector2(0.5f,1);
            sepRT.sizeDelta        = new Vector2(0, 2f);
            sepRT.anchoredPosition = new Vector2(0, -60f);
            sep.AddComponent<Image>().color = SeparatorColor;

            // ── Content area ──────────────────────────────────────────────
            var content = CreateGO("Content", panel.transform);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin        = new Vector2(0,0);
            contentRT.anchorMax        = new Vector2(1,1);
            contentRT.offsetMin        = new Vector2(24f, 24f);
            contentRT.offsetMax        = new Vector2(-24f, -64f);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing            = 18f;
            vlg.padding            = new RectOffset(0, 0, 10, 0);

            // ── Username row ──────────────────────────────────────────────
            var txtUsername = CreateLabelRow(content.transform, "Row_Username", "👤  Username", 28f, FontStyles.Bold, TextPrimary, 44f);

            // ── ELO row ───────────────────────────────────────────────────
            var txtElo = CreateLabelRow(content.transform, "Row_ELO", "⚡  ELO: —", 20f, FontStyles.Normal, AccentBlue, 32f);

            // ── Tier row ──────────────────────────────────────────────────
            var txtTier = CreateLabelRow(content.transform, "Row_Tier", "🏅  Tier: —", 20f, FontStyles.Normal, TextSecondary, 32f);

            // ── Divider ───────────────────────────────────────────────────
            var divider = CreateGO("Divider", content.transform);
            divider.AddComponent<Image>().color = SeparatorColor;
            divider.AddComponent<LayoutElement>().preferredHeight = 1f;

            // ── Win / Loss row ────────────────────────────────────────────
            var txtWinLoss = CreateLabelRow(content.transform, "Row_WinLoss", "🏆  W/L: — / —", 20f, FontStyles.Normal, TextSecondary, 32f);

            // ── Win Rate row ──────────────────────────────────────────────
            var txtWinRate = CreateLabelRow(content.transform, "Row_WinRate", "📊  Win Rate: —%", 20f, FontStyles.Normal, TextSecondary, 32f);

            // ── Loading indicator (spinner placeholder) ───────────────────
            var loadingGo = CreateGO("LoadingIndicator", panel.transform);
            loadingGo.SetActive(false);
            var loadingRT = loadingGo.GetComponent<RectTransform>();
            loadingRT.anchorMin        = new Vector2(0.5f,0.5f);
            loadingRT.anchorMax        = new Vector2(0.5f,0.5f);
            loadingRT.pivot            = new Vector2(0.5f,0.5f);
            loadingRT.sizeDelta        = new Vector2(64f, 64f);
            loadingRT.anchoredPosition = Vector2.zero;
            var loadImg = loadingGo.AddComponent<Image>();
            loadImg.color = LoadingColor;
            var loadLbl = CreateGO("Lbl", loadingGo.transform);
            StretchFull(loadLbl);
            var loadTmp = loadLbl.AddComponent<TextMeshProUGUI>();
            loadTmp.text      = "…";
            loadTmp.fontSize  = 32f;
            loadTmp.color     = Color.white;
            loadTmp.alignment = TextAlignmentOptions.Center;

            return (root, backdrop, panel, txtUsername, txtElo, txtTier, txtWinLoss, txtWinRate, btnClose, loadingGo);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// Creates a simple label row inside a VerticalLayoutGroup container.
        private static TMP_Text CreateLabelRow(Transform parent, string goName, string text,
            float fontSize, FontStyles style, Color color, float preferredHeight)
        {
            var go = CreateGO(goName, parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }

        private static GameObject CreateGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = 5; // UI layer
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;
        }

        private static RectTransform SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            return rt;
        }

        /// Uses reflection to assign a private [SerializeField] field on any MonoBehaviour.
        private static void WireField(MonoBehaviour target, string fieldName, Object value)
        {
            if (value == null)
            {
                Debug.LogWarning($"[HomeSceneSetupTool] WireField: value for '{fieldName}' is null — skipping.");
                return;
            }

            var type  = target.GetType();
            FieldInfo fi = null;

            // Walk up the type hierarchy (in case field is on a base class)
            while (fi == null && type != null)
            {
                fi   = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                type = type.BaseType;
            }

            if (fi == null)
            {
                Debug.LogWarning($"[HomeSceneSetupTool] Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }

            Undo.RecordObject(target, $"Wire {fieldName}");
            fi.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }

        private static void CheckComponent<T>(System.Text.StringBuilder sb, string label) where T : Component
        {
            CheckComponent(sb, label, typeof(T));
        }

        private static void CheckComponent(System.Text.StringBuilder sb, string label, System.Type type)
        {
            var comp = (Component)Object.FindFirstObjectByType(type);
            if (comp == null)
            {
                sb.AppendLine($"❌  {label} — NOT FOUND in scene");
            }
            else
            {
                sb.AppendLine($"✅  {label} — found on '{comp.gameObject.name}'");

                // Check for null SerializeField refs
                var so = new SerializedObject(comp);
                var iter = so.GetIterator();
                iter.NextVisible(true);
                var nullFields = new System.Collections.Generic.List<string>();
                while (iter.NextVisible(false))
                {
                    if (iter.propertyType == SerializedPropertyType.ObjectReference
                        && iter.objectReferenceValue == null
                        && iter.name != "m_Script")
                    {
                        nullFields.Add(iter.name);
                    }
                }
                if (nullFields.Count > 0)
                    sb.AppendLine($"   ⚠️  Null Inspector fields: {string.Join(", ", nullFields)}");
            }
        }
    }
}
#endif
