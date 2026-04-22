#if UNITY_EDITOR
// ══════════════════════════════════════════════════════════════════════════════
//  NightHuntUISetupWizard.cs
//  Menu: Tools ▶ NightHunt ▶ UI Setup Wizard
//
//  Comprehensive one-click wizard that:
//    • Creates InventoryConfig + UISlotLayoutConfig ScriptableObjects
//    • Generates all UI prefabs (slots, cards, buttons, ghost, dialog)
//      using Synty InterfaceMilitaryCombatHUD sprites where available
//    • Builds the full HUD Canvas hierarchy in the active scene
//    • Validates all component references and reports nulls
// ══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.UI.Inventory;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.UI.Mobile;

namespace NightHunt.UI.Editor
{
    public sealed class NightHuntUISetupWizard : EditorWindow
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Window
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/NightHunt/UI Setup Wizard", priority = 1)]
        public static void Open() =>
            GetWindow<NightHuntUISetupWizard>("NightHunt UI Wizard", true);

        private void OnEnable() => minSize = new Vector2(480, 620);

        // ─────────────────────────────────────────────────────────────────────
        //  Constants — output folders
        // ─────────────────────────────────────────────────────────────────────

        private const string ConfigPath  = "Assets/_Night_Hunt/Data/Configs";
        private const string PrefabPath  = "Assets/_Night_Hunt/Prefabs/UI";
        private const string SyntyHUD    = "Assets/Synty/InterfaceMilitaryCombatHUD/Sprites";

        // ─────────────────────────────────────────────────────────────────────
        //  Color palette — Synty Military dark theme
        // ─────────────────────────────────────────────────────────────────────

        // Scene / prefab colors
        private static readonly Color ColDarkBg        = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        private static readonly Color ColPanelBg       = new Color(0.09f, 0.11f, 0.15f, 0.97f);
        private static readonly Color ColHeaderBg      = new Color(0.04f, 0.05f, 0.08f, 1.00f);
        private static readonly Color ColSeparator     = new Color(0.20f, 0.55f, 0.85f, 0.50f);
        private static readonly Color ColAccentBlue    = new Color(0.18f, 0.52f, 0.90f, 1.00f);
        private static readonly Color ColAccentGreen   = new Color(0.18f, 0.75f, 0.28f, 1.00f);
        private static readonly Color ColAccentOrange  = new Color(0.90f, 0.55f, 0.10f, 1.00f);
        private static readonly Color ColAccentRed     = new Color(0.80f, 0.20f, 0.18f, 1.00f);
        private static readonly Color ColTextPrimary   = new Color(0.92f, 0.93f, 0.95f, 1.00f);
        private static readonly Color ColTextSecondary = new Color(0.55f, 0.60f, 0.68f, 1.00f);
        private static readonly Color ColSlotEmpty     = new Color(0.13f, 0.15f, 0.20f, 1.00f);
        private static readonly Color ColSlotHover     = new Color(0.22f, 0.26f, 0.34f, 1.00f);
        private static readonly Color ColSlotSelected  = new Color(0.18f, 0.52f, 0.90f, 0.35f);
        private static readonly Color ColSelectedBorder = new Color(1.00f, 0.80f, 0.10f, 1.00f);
        private static readonly Color ColLockedOverlay = new Color(0.05f, 0.05f, 0.05f, 0.60f);
        private static readonly Color ColGhostAlpha    = new Color(1.00f, 1.00f, 1.00f, 0.75f);

        // Rarity colors
        private static readonly Color RarCommon    = new Color(0.70f, 0.70f, 0.70f, 1f);
        private static readonly Color RarUncommon  = new Color(0.18f, 0.75f, 0.28f, 1f);
        private static readonly Color RarRare      = new Color(0.12f, 0.45f, 0.90f, 1f);
        private static readonly Color RarEpic      = new Color(0.55f, 0.18f, 0.82f, 1f);
        private static readonly Color RarLegendary = new Color(1.00f, 0.65f, 0.00f, 1f);

        // ─────────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────────

        private int          _tab;
        private Vector2      _scroll;
        private Vector2      _logScroll;
        private readonly List<string> _log = new List<string>();

        private static readonly string[] TabLabels =
            { "Overview", "① Configs", "② Prefabs", "③ Scene", "④ Validate" };

        // ─────────────────────────────────────────────────────────────────────
        //  OnGUI
        // ─────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // Header
            var hdrRect = new Rect(0, 0, position.width, 40);
            EditorGUI.DrawRect(hdrRect, new Color(0.04f, 0.06f, 0.12f));
            GUI.Label(new Rect(12, 8, position.width - 12, 26),
                "<b>NightHunt UI Setup Wizard</b> — Synty Military Theme",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, richText = true,
                    normal = { textColor = new Color(0.75f, 0.85f, 1f) } });

            GUILayout.Space(44);

            _tab = GUILayout.Toolbar(_tab, TabLabels, GUILayout.Height(26));

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.Space(4);

            switch (_tab)
            {
                case 0: DrawOverview();  break;
                case 1: DrawConfigs();   break;
                case 2: DrawPrefabs();   break;
                case 3: DrawScene();     break;
                case 4: DrawValidate();  break;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            DrawLogPanel();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB 0 — Overview
        // ═════════════════════════════════════════════════════════════════════

        private void DrawOverview()
        {
            Section("What this wizard does");
            HelpBox(
                "Run the tabs in order ①→②→③.\n\n" +
                "① Configs   — Creates InventoryConfig.asset + UISlotLayoutConfig.asset\n" +
                "               with default values and Synty sprite references pre-wired.\n\n" +
                "② Prefabs   — Generates all reusable UI prefabs:\n" +
                "               SlotPrefab · AttachmentSlot · WeaponCard (×3) ·\n" +
                "               EquipmentCard · WeaponSlotButton · DragDropGhost · DropDialog\n\n" +
                "③ Scene     — Builds the full HUD canvas hierarchy in the active scene:\n" +
                "               CombatHUD · InventoryPanel · WeaponEquipmentPanel ·\n" +
                "               PlayerHUDPanel · DragDropController · PlatformManager\n\n" +
                "④ Validate  — Scans all UI components for null Inspector references.",
                MessageType.Info);

            Section("Synty package detected");
            bool syntyExists = AssetDatabase.IsValidFolder(SyntyHUD);
            if (syntyExists)
                HelpBox("✅  Synty InterfaceMilitaryCombatHUD sprites found — will be auto-assigned.", MessageType.Info);
            else
                HelpBox("⚠️  Synty sprites not found at:\n" + SyntyHUD +
                        "\nPrefabs will be created with placeholder (white) visuals.", MessageType.Warning);

            Section("Output paths");
            EditorGUILayout.LabelField("Configs :", ConfigPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Prefabs :", PrefabPath,  EditorStyles.miniLabel);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB 1 — Configs
        // ═════════════════════════════════════════════════════════════════════

        private void DrawConfigs()
        {
            Section("ScriptableObject Assets");
            HelpBox(
                "Creates InventoryConfig.asset and UISlotLayoutConfig.asset in:\n" +
                ConfigPath + "\n\n" +
                "• InventoryConfig   — 20-slot grid, 4 equipment slots (Head/Chest/Legs/Back), 3 weapon slots.\n" +
                "• UISlotLayoutConfig — Synty sprites, slot prefab refs (assign after Prefabs tab), timings.",
                MessageType.Info);

            if (GUILayout.Button("Create / Update Config Assets", GUILayout.Height(32)))
                CreateConfigs();

            EditorGUILayout.Space(4);
            var invCfg = AssetDatabase.LoadAssetAtPath<InventoryConfig>(
                ConfigPath + "/InventoryConfig.asset");
            var uiCfg = AssetDatabase.LoadAssetAtPath<UISlotLayoutConfig>(
                ConfigPath + "/UISlotLayoutConfig.asset");

            if (invCfg != null) EditorGUILayout.ObjectField("InventoryConfig", invCfg, typeof(InventoryConfig), false);
            if (uiCfg  != null) EditorGUILayout.ObjectField("UISlotLayoutConfig", uiCfg, typeof(UISlotLayoutConfig), false);
        }

        private void CreateConfigs()
        {
            EnsureDir(ConfigPath);
            _log.Clear();

            // ── InventoryConfig ──────────────────────────────────────────────
            var invPath = ConfigPath + "/InventoryConfig.asset";
            var inv = AssetDatabase.LoadAssetAtPath<InventoryConfig>(invPath);
            if (inv == null)
            {
                inv = ScriptableObject.CreateInstance<InventoryConfig>();
                AssetDatabase.CreateAsset(inv, invPath);
                Log("✅ Created InventoryConfig.asset");
            }
            else { Log("♻️  InventoryConfig.asset already exists — skipped."); }

            // Use SerializedObject to set nested fields safely
            var soInv = new SerializedObject(inv);

            // Inventory grid
            SetProp(soInv, "Inventory.GridWidth",    5);
            SetProp(soInv, "Inventory.GridHeight",   4);
            SetProp(soInv, "Inventory.TotalSlots",   20);

            // Equipment slots (Head, Chest, Legs, Back)
            var eqProp = soInv.FindProperty("EquipmentConfig");
            if (eqProp != null)
            {
                eqProp.arraySize = 4;
                SetArrayEnum(eqProp, 0, "SlotType", 0); // Head
                SetArrayEnum(eqProp, 1, "SlotType", 1); // Chest
                SetArrayEnum(eqProp, 2, "SlotType", 3); // Legs
                SetArrayEnum(eqProp, 3, "SlotType", 2); // Back
            }

            // Weapon slots (Primary, Secondary, Melee)
            var wepProp = soInv.FindProperty("WeaponConfig");
            if (wepProp != null)
            {
                wepProp.arraySize = 3;
                SetArrayEnum(wepProp, 0, "Type", 0); // Primary
                SetArrayEnum(wepProp, 1, "Type", 1); // Secondary
                SetArrayEnum(wepProp, 2, "Type", 2); // Melee
            }

            soInv.ApplyModifiedProperties();
            EditorUtility.SetDirty(inv);

            // ── UISlotLayoutConfig ──────────────────────────────────────────
            var uiPath = ConfigPath + "/UISlotLayoutConfig.asset";
            var ui = AssetDatabase.LoadAssetAtPath<UISlotLayoutConfig>(uiPath);
            if (ui == null)
            {
                ui = ScriptableObject.CreateInstance<UISlotLayoutConfig>();
                AssetDatabase.CreateAsset(ui, uiPath);
                Log("✅ Created UISlotLayoutConfig.asset");
            }
            else { Log("♻️  UISlotLayoutConfig.asset already exists — updating values."); }

            var soUI = new SerializedObject(ui);

            // Wire InventoryConfig reference
            SetProp(soUI, "InventoryConfig", inv);

            // Dimensions
            SetProp(soUI, "DefaultSlotSize",       new Vector2(100f, 100f));
            SetProp(soUI, "DefaultExtraEmptySlots", 20);
            SetProp(soUI, "MinimumEmptySlots",      10);

            // Timing
            SetProp(soUI, "DoubleClickThreshold",   0.3f);
            SetProp(soUI, "GhostSnapBackDuration",  0.18f);

            // Tooltip defaults
            SetProp(soUI, "TooltipOffset",          new Vector2(16f, -16f));
            SetProp(soUI, "ShowTooltipDuringDrag",  false);
            SetProp(soUI, "ContextMenuGap",         8f);
            SetProp(soUI, "HideContextMenuOnDragStart", true);

            // Attachment highlight (Synty gold tint)
            SetProp(soUI, "AttachmentSlotHighlightColor", new Color(1f, 0.80f, 0.08f, 0.70f));
            SetProp(soUI, "AttachmentHighlightPulseSpeed", 2.5f);

            // Rarity backgrounds — set colors (sprites will show white without assignment)
            var rarProp = soUI.FindProperty("RarityBackgrounds");
            if (rarProp != null)
            {
                rarProp.arraySize = 5;
                // Rarity enums: Common=0, Uncommon=1, Rare=2, Epic=3, Legendary=4
                for (int i = 0; i < 5; i++) SetArrayEnum(rarProp, i, "Rarity", i);
            }

            soUI.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("💾 Config assets saved.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB 2 — Prefabs
        // ═════════════════════════════════════════════════════════════════════

        private void DrawPrefabs()
        {
            Section("UI Prefabs");
            HelpBox(
                "Generates all UI prefabs using Synty sprites as backgrounds/icons.\n" +
                "Run ① Configs first — UISlotLayoutConfig must exist before making prefabs.\n\n" +
                "Output folder: " + PrefabPath,
                MessageType.Info);

            if (GUILayout.Button("Create / Refresh All Prefabs", GUILayout.Height(32)))
                CreateAllPrefabs();

            EditorGUILayout.Space(6);
            DrawPrefabStatus("NH_DefaultSlot.prefab",         "Inventory/Equipment/Weapon grid slot");
            DrawPrefabStatus("NH_AttachmentSlot.prefab",      "Attachment slot (smaller)");
            DrawPrefabStatus("NH_WeaponCard_Primary.prefab",  "Primary weapon card (4 attach slots)");
            DrawPrefabStatus("NH_WeaponCard_Secondary.prefab","Secondary weapon card (3 attach slots)");
            DrawPrefabStatus("NH_WeaponCard_Melee.prefab",    "Melee weapon card (1 attach slot)");
            DrawPrefabStatus("NH_EquipmentCard.prefab",       "Equipment card (dynamic attach)");
            DrawPrefabStatus("NH_WeaponSlotButton.prefab",    "Combat HUD weapon slot button");
            DrawPrefabStatus("NH_DragDropGhost.prefab",       "Drag ghost with snap-back animation");
            DrawPrefabStatus("NH_DropQuantityDialog.prefab",  "Drop quantity modal dialog");
        }

        private void DrawPrefabStatus(string fileName, string description)
        {
            string path = PrefabPath + "/" + fileName;
            bool exists = File.Exists(
                Path.Combine(Application.dataPath, "..", path));
            string icon = exists ? "✅" : "○ ";
            EditorGUILayout.LabelField($" {icon}  {fileName}", description, EditorStyles.miniLabel);
        }

        private void CreateAllPrefabs()
        {
            EnsureDir(PrefabPath);
            _log.Clear();

            var uiCfg = AssetDatabase.LoadAssetAtPath<UISlotLayoutConfig>(
                ConfigPath + "/UISlotLayoutConfig.asset");
            if (uiCfg == null)
            {
                Log("❌ UISlotLayoutConfig not found — run ① Configs first.");
                return;
            }

            // Load Synty sprites (null-safe — fallback to white)
            var sprSocketBg = LoadSprite(SyntyHUD + "/MilitaryCombat/SPR_MilitaryCombat_Socket_Gem_01.png");
            var sprHighlight = LoadSprite(SyntyHUD + "/FX/SPR_FX_Glow_01.png");

            CreateSlotPrefab("NH_DefaultSlot",    new Vector2(100, 100), uiCfg, sprSocketBg, sprHighlight, true);
            CreateSlotPrefab("NH_AttachmentSlot", new Vector2(52, 52),   uiCfg, sprSocketBg, sprHighlight, false);
            CreateWeaponCardPrefab("NH_WeaponCard_Primary",   4, uiCfg);
            CreateWeaponCardPrefab("NH_WeaponCard_Secondary", 3, uiCfg);
            CreateWeaponCardPrefab("NH_WeaponCard_Melee",     1, uiCfg);
            CreateEquipmentCardPrefab(uiCfg);
            CreateWeaponSlotButtonPrefab();
            CreateDragDropGhostPrefab(sprSocketBg);
            CreateDropQuantityDialogPrefab();

            // Wire prefab refs into UISlotLayoutConfig
            WireSlotPrefabsToConfig(uiCfg);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("✅ All prefabs generated and config wired.");
        }

        // ── Slot prefab ──────────────────────────────────────────────────────

        private void CreateSlotPrefab(string name, Vector2 size,
            UISlotLayoutConfig uiCfg, Sprite bgSprite, Sprite highlightSprite, bool withStack)
        {
            string path = PrefabPath + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            { Log($"♻️  {name} already exists — skipping."); return; }

            var root = new GameObject(name);
            root.layer = 5;
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            // Background image
            var bg = MakeChild("Background", root.transform, size);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color  = ColSlotEmpty;
            bgImg.sprite = bgSprite;
            bgImg.type   = Image.Type.Sliced;

            // Item icon
            var icon = MakeChild("Icon", root.transform, size * 0.82f);
            var iconImg = icon.AddComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;

            // Highlight overlay (hover)
            var hl = MakeChild("Highlight", root.transform, size);
            var hlImg = hl.AddComponent<Image>();
            hlImg.color   = ColSlotHover;
            hlImg.sprite  = highlightSprite;
            hlImg.raycastTarget = false;
            hl.SetActive(false);

            // Selected frame
            var sel = MakeChild("Selected", root.transform, size);
            var selImg = sel.AddComponent<Image>();
            selImg.color  = ColSlotSelected;
            selImg.raycastTarget = false;
            sel.SetActive(false);

            // Stack badge
            GameObject stackObj  = null;
            TMP_Text   stackText = null;
            if (withStack)
            {
                stackObj = MakeChild("StackBadge", root.transform, new Vector2(size.x * 0.55f, 22f));
                var stackRT = stackObj.GetComponent<RectTransform>();
                stackRT.anchorMin = new Vector2(1, 0);
                stackRT.anchorMax = new Vector2(1, 0);
                stackRT.pivot     = new Vector2(1, 0);
                stackRT.anchoredPosition = new Vector2(-3f, 3f);
                var badgeBg = stackObj.AddComponent<Image>();
                badgeBg.color = new Color(0, 0, 0, 0.65f);

                var txtGo = MakeChild("Count", stackObj.transform, Vector2.zero);
                StretchFull(txtGo);
                stackText = txtGo.AddComponent<TextMeshProUGUI>();
                stackText.text      = "×1";
                stackText.fontSize  = 13f;
                stackText.fontStyle = FontStyles.Bold;
                stackText.color     = ColTextPrimary;
                stackText.alignment = TextAlignmentOptions.BottomRight;
                stackObj.SetActive(false);
            }

            // Locked overlay (spectator)
            var lockedGo = MakeChild("LockedOverlay", root.transform, size);
            var lockedImg = lockedGo.AddComponent<Image>();
            lockedImg.color = ColLockedOverlay;
            lockedImg.raycastTarget = true;
            lockedGo.SetActive(false);

            // Canvas group (for drag fade)
            var cg = root.AddComponent<CanvasGroup>();

            // ItemSlotView
            var slotView = root.AddComponent<ItemSlotView>();
            var soView   = new SerializedObject(slotView);
            SetProp(soView, "_icon",           iconImg);
            SetProp(soView, "_background",     bgImg);
            SetProp(soView, "_highlightFrame", hlImg);
            SetProp(soView, "_selectedFrame",  selImg);
            SetProp(soView, "_canvasGroup",    cg);
            SetProp(soView, "_lockedOverlay",  lockedGo);
            if (withStack)
            {
                SetProp(soView, "_stackText", stackText);
                SetProp(soView, "_stackObj",  stackObj);
            }
            SetProp(soView, "_uiConfig", uiCfg);
            soView.ApplyModifiedProperties();

            // ItemSlotInput
            root.AddComponent<ItemSlotInput>();

            SavePrefab(root, path);
            Object.DestroyImmediate(root);
            Log($"✅ Created {name}.prefab");
        }

        // ── WeaponCard prefab ────────────────────────────────────────────────

        private void CreateWeaponCardPrefab(string name, int attachCount, UISlotLayoutConfig uiCfg)
        {
            string path = PrefabPath + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            { Log($"♻️  {name} already exists — skipping."); return; }

            var attachPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_AttachmentSlot.prefab");
            var slotPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_DefaultSlot.prefab");

            var root = new GameObject(name);
            root.layer = 5;
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(220, 260);

            // Dark card background
            var bg = root.AddComponent<Image>();
            bg.color = ColPanelBg;

            // Slot name label
            var lblGo = MakeChild("SlotLabel", root.transform, new Vector2(200, 24));
            var lblRT = lblGo.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0, 1); lblRT.anchorMax = new Vector2(1, 1);
            lblRT.pivot = new Vector2(0.5f, 1);
            lblRT.anchoredPosition = new Vector2(0, -6);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text      = name.Replace("NH_WeaponCard_", "");
            lbl.fontSize  = 13f; lbl.fontStyle = FontStyles.Bold;
            lbl.color     = ColTextSecondary;
            lbl.alignment = TextAlignmentOptions.Center;

            // Main weapon slot
            var mainSlotGo  = slotPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab)
                : MakeChild("MainSlot", root.transform, new Vector2(100, 100));
            mainSlotGo.name = "MainSlot";
            mainSlotGo.transform.SetParent(root.transform, false);
            var mainSlotRT = mainSlotGo.GetComponent<RectTransform>();
            mainSlotRT.anchoredPosition = new Vector2(0, -60);

            // Attachment slots row
            var attachViews  = new ItemSlotView[attachCount];
            var attachTypes  = new int[attachCount];   // AttachmentSlotType enum values
            var attachRow = MakeChild("AttachSlots", root.transform, new Vector2(200, 52));
            var attachRowRT = attachRow.GetComponent<RectTransform>();
            attachRowRT.anchorMin = new Vector2(0.5f, 0);
            attachRowRT.anchorMax = new Vector2(0.5f, 0);
            attachRowRT.pivot = new Vector2(0.5f, 0);
            attachRowRT.anchoredPosition = new Vector2(0, 8);
            var hlg = attachRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4; hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < attachCount; i++)
            {
                var aGo = attachPrefab != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(attachPrefab)
                    : MakeChild($"Attach_{i}", attachRow.transform, new Vector2(52, 52));
                aGo.name = $"AttachSlot_{i}";
                aGo.transform.SetParent(attachRow.transform, false);
                var view = aGo.GetComponent<ItemSlotView>();
                if (view == null) view = aGo.AddComponent<ItemSlotView>();
                attachViews[i] = view;
                attachTypes[i] = i; // 0=Scope, 1=Barrel, 2=Grip, 3=Stock
            }

            // WeaponCardView
            var card   = root.AddComponent<WeaponCardView>();
            var soCard = new SerializedObject(card);
            SetProp(soCard, "_mainSlot",    mainSlotGo.GetComponent<ItemSlotView>());
            SetProp(soCard, "_slotNameText", lbl);
            SetProp(soCard, "_uiConfig",    uiCfg);

            var attachViewsProp = soCard.FindProperty("_attachmentSlotViews");
            var attachTypesProp = soCard.FindProperty("_attachmentSlotTypes");
            if (attachViewsProp != null && attachTypesProp != null)
            {
                attachViewsProp.arraySize = attachCount;
                attachTypesProp.arraySize = attachCount;
                for (int i = 0; i < attachCount; i++)
                {
                    attachViewsProp.GetArrayElementAtIndex(i).objectReferenceValue = attachViews[i];
                    attachTypesProp.GetArrayElementAtIndex(i).enumValueIndex       = attachTypes[i];
                }
            }
            soCard.ApplyModifiedProperties();

            SavePrefab(root, path);
            Object.DestroyImmediate(root);
            Log($"✅ Created {name}.prefab");
        }

        // ── EquipmentCard prefab ─────────────────────────────────────────────

        private void CreateEquipmentCardPrefab(UISlotLayoutConfig uiCfg)
        {
            string path = PrefabPath + "/NH_EquipmentCard.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            { Log("♻️  NH_EquipmentCard already exists — skipping."); return; }

            var slotPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_DefaultSlot.prefab");
            var attachPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_AttachmentSlot.prefab");

            var root   = new GameObject("NH_EquipmentCard");
            root.layer = 5;
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(180, 220);
            root.AddComponent<Image>().color = ColPanelBg;

            var mainGo = slotPrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab)
                : MakeChild("MainSlot", root.transform, new Vector2(100, 100));
            mainGo.name = "MainSlot";
            mainGo.transform.SetParent(root.transform, false);
            mainGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 30);

            // Dynamic attach container (VerticalLayoutGroup, spawned at runtime)
            var container = MakeChild("AttachContainer", root.transform, new Vector2(160, 60));
            var conRT = container.GetComponent<RectTransform>();
            conRT.anchorMin = new Vector2(0, 0); conRT.anchorMax = new Vector2(1, 0);
            conRT.pivot = new Vector2(0.5f, 0); conRT.anchoredPosition = new Vector2(0, 6);
            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;

            var card   = root.AddComponent<EquipmentCardView>();
            var soCard = new SerializedObject(card);
            SetProp(soCard, "_mainSlot",            mainGo.GetComponent<ItemSlotView>());
            SetProp(soCard, "_attachmentContainer", container.GetComponent<RectTransform>());
            SetProp(soCard, "_attachmentSlotPrefab", attachPrefab);
            SetProp(soCard, "_uiConfig",            uiCfg);
            soCard.ApplyModifiedProperties();

            SavePrefab(root, path);
            Object.DestroyImmediate(root);
            Log("✅ Created NH_EquipmentCard.prefab");
        }

        // ── WeaponSlotButton prefab ──────────────────────────────────────────

        private void CreateWeaponSlotButtonPrefab()
        {
            string path = PrefabPath + "/NH_WeaponSlotButton.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            { Log("♻️  NH_WeaponSlotButton already exists — skipping."); return; }

            var sprAmmo = LoadSprite(SyntyHUD + "/Icons_Inventory/ICON_MilitaryCombat_Inventory_Ammo_Bullets_01_Clean.png");

            var root = new GameObject("NH_WeaponSlotButton");
            root.layer = 5;
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(130, 70);

            var bgImg = root.AddComponent<Image>();
            bgImg.color = ColPanelBg;
            root.AddComponent<Button>();

            // Weapon icon area
            var iconGo = MakeChild("WeaponIcon", root.transform, new Vector2(60, 54));
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0.5f); iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.pivot = new Vector2(0, 0.5f); iconRT.anchoredPosition = new Vector2(6, 0);
            iconGo.AddComponent<Image>().color = new Color(1, 1, 1, 0.85f);

            // Weapon name
            var nameGo = MakeChild("WeaponName", root.transform, new Vector2(64, 18));
            var nameRT = nameGo.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(1, 1); nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(1, 1); nameRT.anchoredPosition = new Vector2(-4, -4);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "Weapon"; nameTmp.fontSize = 11f;
            nameTmp.color = ColTextPrimary; nameTmp.alignment = TextAlignmentOptions.TopRight;

            // Mag ammo
            var magGo = MakeChild("MagAmmo", root.transform, new Vector2(64, 22));
            var magRT = magGo.GetComponent<RectTransform>();
            magRT.anchorMin = new Vector2(1, 0.5f); magRT.anchorMax = new Vector2(1, 0.5f);
            magRT.pivot = new Vector2(1, 0.5f); magRT.anchoredPosition = new Vector2(-4, 0);
            var magTmp = magGo.AddComponent<TextMeshProUGUI>();
            magTmp.text = "30"; magTmp.fontSize = 20f; magTmp.fontStyle = FontStyles.Bold;
            magTmp.color = ColTextPrimary; magTmp.alignment = TextAlignmentOptions.MidlineRight;

            // Reserve ammo
            var resGo = MakeChild("ReserveAmmo", root.transform, new Vector2(64, 16));
            var resRT = resGo.GetComponent<RectTransform>();
            resRT.anchorMin = new Vector2(1, 0); resRT.anchorMax = new Vector2(1, 0);
            resRT.pivot = new Vector2(1, 0); resRT.anchoredPosition = new Vector2(-4, 6);
            var resTmp = resGo.AddComponent<TextMeshProUGUI>();
            resTmp.text = "/90"; resTmp.fontSize = 12f;
            resTmp.color = ColTextSecondary; resTmp.alignment = TextAlignmentOptions.BottomRight;

            // Ammo bar (Slider)
            var sliderGo = MakeChild("AmmoBar", root.transform, new Vector2(126, 4));
            var sliderRT = sliderGo.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0, 0); sliderRT.anchorMax = new Vector2(1, 0);
            sliderRT.pivot = new Vector2(0.5f, 0); sliderRT.anchoredPosition = new Vector2(0, 0);
            sliderRT.offsetMin = new Vector2(2, 2); sliderRT.offsetMax = new Vector2(-2, 6);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            slider.interactable = false;
            // Simple fill
            var fillArea = MakeChild("Fill Area", sliderGo.transform, Vector2.zero);
            StretchFull(fillArea); fillArea.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            var fill = MakeChild("Fill", fillArea.transform, Vector2.zero);
            StretchFull(fill);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = ColAccentGreen;
            slider.fillRect = fill.GetComponent<RectTransform>();

            // Selected border
            var selBorderGo = MakeChild("SelectedBorder", root.transform, Vector2.zero);
            StretchFull(selBorderGo);
            var selBorderImg = selBorderGo.AddComponent<Image>();
            selBorderImg.color = ColSelectedBorder;
            selBorderImg.raycastTarget = false;
            selBorderGo.SetActive(false);

            // WeaponSlotButton
            var wsb = root.AddComponent<WeaponSlotButton>();
            var soWSB = new SerializedObject(wsb);
            SetProp(soWSB, "_selectedBorder",  selBorderImg);
            SetProp(soWSB, "_weaponNameText",  nameTmp);
            SetProp(soWSB, "_magAmmoText",     magTmp);
            SetProp(soWSB, "_reserveAmmoText", resTmp);
            SetProp(soWSB, "_ammoSlider",      slider);
            soWSB.ApplyModifiedProperties();

            SavePrefab(root, path);
            Object.DestroyImmediate(root);
            Log("✅ Created NH_WeaponSlotButton.prefab");
        }

        // ── DragDropGhost prefab ─────────────────────────────────────────────

        private void CreateDragDropGhostPrefab(Sprite bgSprite)
        {
            string path = PrefabPath + "/NH_DragDropGhost.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            { Log("♻️  NH_DragDropGhost already exists — skipping."); return; }

            var root = new GameObject("NH_DragDropGhost");
            root.layer = 5;
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(100, 100);

            var cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 0.75f;
            cg.blocksRaycasts = false;

            var bgGo  = MakeChild("Background", root.transform, new Vector2(100, 100));
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color  = ColSlotEmpty;
            bgImg.sprite = bgSprite;
            bgImg.raycastTarget = false;

            var iconGo  = MakeChild("Icon", root.transform, new Vector2(82, 82));
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = ColGhostAlpha;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;

            var hlGo  = MakeChild("HighlightFrame", root.transform, new Vector2(100, 100));
            var hlImg = hlGo.AddComponent<Image>();
            hlImg.color = ColSlotHover;
            hlImg.raycastTarget = false;
            hlGo.SetActive(false);

            var ghost  = root.AddComponent<DragDropGhost>();
            var soGhost = new SerializedObject(ghost);
            SetProp(soGhost, "_icon",           iconImg);
            SetProp(soGhost, "_background",     bgImg);
            SetProp(soGhost, "_highlightFrame", hlImg);
            SetProp(soGhost, "_canvasGroup",    cg);
            soGhost.ApplyModifiedProperties();

            SavePrefab(root, path);
            Object.DestroyImmediate(root);
            Log("✅ Created NH_DragDropGhost.prefab");
        }

        // ── DropQuantityDialog prefab ────────────────────────────────────────

        private void CreateDropQuantityDialogPrefab()
        {
            string path = PrefabPath + "/NH_DropQuantityDialog.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            { Log("♻️  NH_DropQuantityDialog already exists — skipping."); return; }

            var root = new GameObject("NH_DropQuantityDialog");
            root.layer = 5;
            root.SetActive(false);
            StretchFull(root);

            // Backdrop dim
            var bdGo = MakeChild("Backdrop", root.transform, Vector2.zero);
            StretchFull(bdGo);
            bdGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);

            // Modal panel
            var panel = MakeChild("Panel", root.transform, new Vector2(340, 260));
            CenterAnchor(panel, Vector2.zero);
            panel.AddComponent<Image>().color = ColPanelBg;

            // Header
            var hdr = MakeChild("Header", panel.transform, new Vector2(0, 48));
            SetTopStretchAnchor(hdr);
            hdr.AddComponent<Image>().color = ColHeaderBg;
            var hdrLbl = MakeChild("Title", hdr.transform, Vector2.zero);
            StretchFull(hdrLbl);
            var hdrTmp = hdrLbl.AddComponent<TextMeshProUGUI>();
            hdrTmp.text = "Drop Items"; hdrTmp.fontSize = 17f; hdrTmp.fontStyle = FontStyles.Bold;
            hdrTmp.color = ColTextPrimary; hdrTmp.alignment = TextAlignmentOptions.Center;

            // Hint text
            var hint = MakeChild("HintText", panel.transform, new Vector2(300, 24));
            var hintRT = hint.GetComponent<RectTransform>();
            hintRT.anchoredPosition = new Vector2(0, 60);
            var hintTmp = hint.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "How many to drop?"; hintTmp.fontSize = 13f;
            hintTmp.color = ColTextSecondary; hintTmp.alignment = TextAlignmentOptions.Center;

            // Slider
            var sliderContainer = MakeChild("SliderContainer", panel.transform, new Vector2(290, 30));
            sliderContainer.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20);
            var slider = sliderContainer.AddComponent<Slider>();
            slider.minValue = 1; slider.maxValue = 99; slider.value = 1;
            var fillAreaGo = MakeChild("Fill Area", sliderContainer.transform, Vector2.zero);
            StretchFull(fillAreaGo);
            var fillGo = MakeChild("Fill", fillAreaGo.transform, Vector2.zero);
            StretchFull(fillGo);
            fillGo.AddComponent<Image>().color = ColAccentBlue;
            slider.fillRect = fillGo.GetComponent<RectTransform>();

            // Input field
            var inputGo = MakeChild("QuantityInput", panel.transform, new Vector2(80, 32));
            inputGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -15);
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.18f, 0.22f);
            var inputField = inputGo.AddComponent<TMP_InputField>();
            var inputTextGo = MakeChild("Text", inputGo.transform, Vector2.zero);
            StretchFull(inputTextGo);
            var inputTmp = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputTmp.text = "1"; inputTmp.fontSize = 16f;
            inputTmp.color = ColTextPrimary; inputTmp.alignment = TextAlignmentOptions.Center;
            inputField.textComponent = inputTmp;
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Button row
            var btnsGo = MakeChild("Buttons", panel.transform, new Vector2(310, 40));
            btnsGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -80);
            var hlgBtns = btnsGo.AddComponent<HorizontalLayoutGroup>();
            hlgBtns.spacing = 8; hlgBtns.childControlWidth = true; hlgBtns.childForceExpandWidth = true;

            var btnCancel   = CreateDialogButton(btnsGo.transform, "Cancel",   "Cancel",   ColAccentRed);
            var btnDropOne  = CreateDialogButton(btnsGo.transform, "Drop1",    "Drop 1",   ColPanelBg);
            var btnDropAll  = CreateDialogButton(btnsGo.transform, "DropAll",  "Drop All", ColAccentOrange);
            var btnDrop     = CreateDialogButton(btnsGo.transform, "Drop",     "Drop",     ColAccentBlue);

            var dlg   = root.AddComponent<DropQuantityDialog>();
            var soDlg = new SerializedObject(dlg);
            SetProp(soDlg, "_root",             panel);
            SetProp(soDlg, "_titleText",        hdrTmp);
            SetProp(soDlg, "_hintText",         hintTmp);
            SetProp(soDlg, "_quantitySlider",   slider);
            SetProp(soDlg, "_quantityInput",    inputField);
            SetProp(soDlg, "_sliderContainer",  sliderContainer);
            SetProp(soDlg, "_cancelButton",     btnCancel);
            SetProp(soDlg, "_dropOneButton",    btnDropOne);
            SetProp(soDlg, "_dropAllButton",    btnDropAll);
            SetProp(soDlg, "_dropButton",       btnDrop);
            soDlg.ApplyModifiedProperties();

            SavePrefab(root, path);
            Object.DestroyImmediate(root);
            Log("✅ Created NH_DropQuantityDialog.prefab");
        }

        private Button CreateDialogButton(Transform parent, string goName, string label, Color bgColor)
        {
            var go = MakeChild(goName, parent, new Vector2(70, 36));
            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            var txtGo = MakeChild("Label", go.transform, Vector2.zero);
            StretchFull(txtGo);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 13f; tmp.fontStyle = FontStyles.Bold;
            tmp.color = ColTextPrimary; tmp.alignment = TextAlignmentOptions.Center;
            return btn;
        }

        // ── Wire prefab refs into UISlotLayoutConfig ─────────────────────────

        private void WireSlotPrefabsToConfig(UISlotLayoutConfig uiCfg)
        {
            var defaultSlot    = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_DefaultSlot.prefab");
            var attachSlot     = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_AttachmentSlot.prefab");

            var soUI = new SerializedObject(uiCfg);
            SetProp(soUI, "DefaultSlotPrefab",     defaultSlot);
            SetProp(soUI, "AttachmentSlotPrefab",  attachSlot);
            // EquipmentSlotPrefab + WeaponSlotPrefab → null (falls back to DefaultSlotPrefab)
            soUI.ApplyModifiedProperties();
            EditorUtility.SetDirty(uiCfg);
            Log("🔗 UISlotLayoutConfig prefab refs wired.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB 3 — Scene
        // ═════════════════════════════════════════════════════════════════════

        private void DrawScene()
        {
            Section("Active Scene Setup");

            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            EditorGUILayout.LabelField("Active scene:", sceneName, EditorStyles.boldLabel);

            HelpBox(
                "Builds the complete HUD canvas in the active (Gameplay) scene.\n\n" +
                "Safe to run multiple times — skips any component already present.\n\n" +
                "Requires ① Configs + ② Prefabs to be complete first.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Build / Refresh HUD Canvas in Scene", GUILayout.Height(32)))
                BuildSceneHUD();

            EditorGUILayout.Space(8);
            DrawSceneStatus();
        }

        private void DrawSceneStatus()
        {
            void StatusRow<T>(string label) where T : Component
            {
                bool found = Object.FindFirstObjectByType<T>() != null;
                EditorGUILayout.LabelField($" {(found ? "✅" : "○ ")}  {label}",
                    found ? "found" : "missing", EditorStyles.miniLabel);
            }
            StatusRow<UIRootController>     ("UIRootController");
            StatusRow<PlatformManager>      ("PlatformManager");
            StatusRow<DragDropController>   ("DragDropController");
            StatusRow<CombatHUDPanel>       ("CombatHUDPanel");
            StatusRow<InventoryScreen>      ("InventoryScreen");
            StatusRow<WeaponEquipmentPanel> ("WeaponEquipmentPanel");
            StatusRow<PlayerHUDPanel>       ("PlayerHUDPanel");
        }

        private void BuildSceneHUD()
        {
            _log.Clear();

            // ── Load assets ──────────────────────────────────────────────────
            var invCfg    = AssetDatabase.LoadAssetAtPath<InventoryConfig>(ConfigPath + "/InventoryConfig.asset");
            var uiCfg     = AssetDatabase.LoadAssetAtPath<UISlotLayoutConfig>(ConfigPath + "/UISlotLayoutConfig.asset");
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_DefaultSlot.prefab");
            var wepBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_WeaponSlotButton.prefab");
            var ghostPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_DragDropGhost.prefab");
            var dropDlgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_DropQuantityDialog.prefab");
            var wepCardP  = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_WeaponCard_Primary.prefab");
            var wepCardS  = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_WeaponCard_Secondary.prefab");
            var wepCardM  = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_WeaponCard_Melee.prefab");
            var eqCard    = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + "/NH_EquipmentCard.prefab");

            if (uiCfg == null || invCfg == null)
            {
                Log("❌ Config assets missing — run ① Configs first.");
                return;
            }

            Undo.SetCurrentGroupName("NightHunt Build HUD");
            int undoGroup = Undo.GetCurrentGroup();

            // ── Canvas ───────────────────────────────────────────────────────
            var canvasGo = FindOrCreate<Canvas>("HUD_Canvas");
            var canvas   = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            EnsureComponent<CanvasScaler>(canvasGo, s => {
                s.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                s.referenceResolution = new Vector2(1920, 1080);
                s.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                s.matchWidthOrHeight = 0.5f;
            });
            EnsureComponent<GraphicRaycaster>(canvasGo, _ => { });
            Log("✅ HUD_Canvas ready.");

            // ── PlatformManager ──────────────────────────────────────────────
            EnsureComponent<PlatformManager>(canvasGo, _ => { });

            // ── DragDropController ───────────────────────────────────────────
            EnsureComponentAndWire<DragDropController>(canvasGo, ddc => {
                var so = new SerializedObject(ddc);
                if (ghostPrefab != null)     SetProp(so, "_ghostPrefab", ghostPrefab.GetComponent<DragDropGhost>());
                SetProp(so, "_dragCanvas",   canvas);
                // _dropQuantityDialog is wired after InventoryPanel is built (below)
                so.ApplyModifiedProperties();
            });
            Log("✅ DragDropController wired.");

            // ── UIRootController ─────────────────────────────────────────────
            var uiRootComp = EnsureComponent<UIRootController>(canvasGo, _ => { });

            // ── CombatHUD ────────────────────────────────────────────────────
            var combatHUDGo = FindOrCreateChild(canvasGo.transform, "CombatHUD");
            StretchFull(combatHUDGo);
            var combatHUD = EnsureComponent<CombatHUDPanel>(combatHUDGo, chp => {
                var so = new SerializedObject(chp);
                SetProp(so, "_inventoryConfig", invCfg);
                if (wepBtnPrefab != null) SetProp(so, "_weaponSlotPrefab", wepBtnPrefab.GetComponent<WeaponSlotButton>());
                so.ApplyModifiedProperties();
            });

            // Weapon slots row
            var wepRow = FindOrCreateChild(combatHUDGo.transform, "WeaponSlotsRow");
            var wepRowRT = wepRow.GetComponent<RectTransform>() ?? wepRow.AddComponent<RectTransform>();
            wepRowRT.anchorMin = new Vector2(0.5f, 0); wepRowRT.anchorMax = new Vector2(0.5f, 0);
            wepRowRT.pivot = new Vector2(0.5f, 0); wepRowRT.anchoredPosition = new Vector2(0, 10);
            wepRowRT.sizeDelta = new Vector2(420, 72);
            EnsureComponent<HorizontalLayoutGroup>(wepRow, hlg => {
                hlg.spacing = 6; hlg.childControlHeight = false; hlg.childControlWidth = false;
                hlg.childAlignment = TextAnchor.MiddleCenter;
            });
            var soHUD = new SerializedObject(combatHUD);
            SetProp(soHUD, "_weaponSlotsContainer", wepRow.transform);
            soHUD.ApplyModifiedProperties();
            Log("✅ CombatHUDPanel wired.");

            // ── InventoryPanel ───────────────────────────────────────────────
            var invPanelGo = FindOrCreateChild(canvasGo.transform, "InventoryPanel");
            StretchFull(invPanelGo);
            EnsureComponent<Image>(invPanelGo, img => img.color = new Color(0, 0, 0, 0.70f));
            invPanelGo.SetActive(false); // hidden by default

            // InventoryScreen
            var invScreen = EnsureComponent<InventoryScreen>(invPanelGo, isc => {
                var so = new SerializedObject(isc);
                SetProp(so, "_uiConfig", uiCfg);
                so.ApplyModifiedProperties();
            });

            // Inventory grid root (ScrollView content)
            var invGridGo = FindOrCreateChild(invPanelGo.transform, "InventoryGrid");
            var invGridRT = invGridGo.GetComponent<RectTransform>() ?? invGridGo.AddComponent<RectTransform>();
            invGridRT.anchorMin = new Vector2(0, 0.1f); invGridRT.anchorMax = new Vector2(0.55f, 0.95f);
            invGridRT.offsetMin = new Vector2(16, 0); invGridRT.offsetMax = new Vector2(-8, 0);
            EnsureComponent<GridLayoutGroup>(invGridGo, glg => {
                glg.cellSize     = new Vector2(100, 100);
                glg.spacing      = new Vector2(4, 4);
                glg.constraint   = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = 5;
            });

            // WeaponEquipmentPanel
            var wepEqGo = FindOrCreateChild(invPanelGo.transform, "WeaponEquipmentPanel");
            var wepEqRT = wepEqGo.GetComponent<RectTransform>() ?? wepEqGo.AddComponent<RectTransform>();
            wepEqRT.anchorMin = new Vector2(0.55f, 0.1f); wepEqRT.anchorMax = new Vector2(1f, 0.95f);
            wepEqRT.offsetMin = new Vector2(8, 0); wepEqRT.offsetMax = new Vector2(-16, 0);

            var wepCardsRoot = FindOrCreateChild(wepEqGo.transform, "WeaponCardsRoot");
            EnsureComponent<HorizontalLayoutGroup>(wepCardsRoot, hlg => {
                hlg.spacing = 8; hlg.childControlWidth = false;
            });
            var eqCardsRoot = FindOrCreateChild(wepEqGo.transform, "EquipmentCardsRoot");
            EnsureComponent<VerticalLayoutGroup>(eqCardsRoot, vlg => { vlg.spacing = 6; });

            var wepEqPanel = EnsureComponent<WeaponEquipmentPanel>(wepEqGo, wep => {
                var so = new SerializedObject(wep);
                SetProp(so, "_weaponCardContainer",    wepCardsRoot.GetComponent<RectTransform>());
                SetProp(so, "_equipmentCardContainer", eqCardsRoot.GetComponent<RectTransform>());
                SetProp(so, "_defaultWeaponCardPrefab", wepCardP);
                SetProp(so, "_equipmentCardPrefab",    eqCard);
                SetProp(so, "_uiConfig",               uiCfg);
                // WeaponCardMappings (per-item-type overrides) left empty — editor can fill
                so.ApplyModifiedProperties();
            });

            // DropQuantityDialog
            var dlgGo   = FindOrCreateChild(invPanelGo.transform, "DropQuantityDialog");
            DropQuantityDialog dlgComp;
            if (dropDlgPrefab != null)
            {
                if (dlgGo.GetComponent<DropQuantityDialog>() == null)
                {
                    var dlgInstance = (GameObject)PrefabUtility.InstantiatePrefab(dropDlgPrefab);
                    dlgInstance.name = "DropQuantityDialog";
                    dlgInstance.transform.SetParent(invPanelGo.transform, false);
                    StretchFull(dlgInstance);
                    dlgComp = dlgInstance.GetComponent<DropQuantityDialog>();
                    Object.DestroyImmediate(dlgGo); // replace placeholder
                    dlgGo = dlgInstance;
                }
                else dlgComp = dlgGo.GetComponent<DropQuantityDialog>();
            }
            else dlgComp = EnsureComponent<DropQuantityDialog>(dlgGo, _ => { });

            // ItemContextMenu (minimal placeholder — structure requires manual prefab)
            var ctxGo = FindOrCreateChild(invPanelGo.transform, "ItemContextMenu");
            ctxGo.SetActive(false);
            EnsureComponent<ItemContextMenu>(ctxGo, _ => { });

            // SpectatorBanner
            var bannerGo = FindOrCreateChild(invPanelGo.transform, "SpectatorBanner");
            var bannerRT = bannerGo.GetComponent<RectTransform>() ?? bannerGo.AddComponent<RectTransform>();
            bannerRT.anchorMin = new Vector2(0, 1); bannerRT.anchorMax = new Vector2(1, 1);
            bannerRT.pivot = new Vector2(0.5f, 1); bannerRT.sizeDelta = new Vector2(0, 36);
            bannerRT.anchoredPosition = Vector2.zero;
            EnsureComponent<Image>(bannerGo, img => img.color = new Color(0.85f, 0.35f, 0.10f, 0.85f));
            bannerGo.SetActive(false);
            var bannerLblGo = FindOrCreateChild(bannerGo.transform, "Label");
            StretchFull(bannerLblGo);
            var bannerTmp = bannerLblGo.GetComponent<TextMeshProUGUI>() ?? bannerLblGo.AddComponent<TextMeshProUGUI>();
            bannerTmp.text = "SPECTATING"; bannerTmp.fontSize = 14f; bannerTmp.fontStyle = FontStyles.Bold;
            bannerTmp.color = Color.white; bannerTmp.alignment = TextAlignmentOptions.Center;

            // Wire InventoryScreen
            var soIS = new SerializedObject(invScreen);
            SetProp(soIS, "_inventoryGridRoot",    invGridGo.GetComponent<RectTransform>());
            SetProp(soIS, "_weaponEquipmentPanel", wepEqPanel);
            SetProp(soIS, "_dropQuantityDialog",   dlgComp);
            SetProp(soIS, "_spectatorBanner",      bannerGo);
            SetProp(soIS, "_spectatorLabel",       bannerTmp);
            soIS.ApplyModifiedProperties();
            Log("✅ InventoryScreen wired.");

            // ── PlayerHUDPanel ───────────────────────────────────────────────
            var playerHUDGo = FindOrCreateChild(canvasGo.transform, "PlayerHUDPanel");
            var pHudRT = playerHUDGo.GetComponent<RectTransform>() ?? playerHUDGo.AddComponent<RectTransform>();
            pHudRT.anchorMin = new Vector2(0, 0); pHudRT.anchorMax = new Vector2(0, 0);
            pHudRT.pivot = new Vector2(0, 0); pHudRT.anchoredPosition = new Vector2(12, 12);
            pHudRT.sizeDelta = new Vector2(240, 180);
            EnsureComponent<PlayerHUDPanel>(playerHUDGo, _ => { });
            Log("✅ PlayerHUDPanel created (assign _statUIConfig manually).");

            // ── Wire UIRootController ────────────────────────────────────────
            var soRoot = new SerializedObject(uiRootComp);
            SetProp(soRoot, "_inventoryScreen",    invScreen);
            SetProp(soRoot, "_combatHudPanel",     combatHUD);
            SetProp(soRoot, "_playerHudPanel",     playerHUDGo.GetComponent<PlayerHUDPanel>());
            SetProp(soRoot, "_inventoryRootObject", invPanelGo);
            soRoot.ApplyModifiedProperties();
            Log("✅ UIRootController wired.");

            // Wire DragDropController dropQuantityDialog now that it exists
            var ddcComp = canvasGo.GetComponent<DragDropController>();
            if (ddcComp != null)
            {
                var soDDC = new SerializedObject(ddcComp);
                SetProp(soDDC, "_dropQuantityDialog", dlgComp);
                soDDC.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Log("✅ Scene setup complete! Save with Ctrl+S.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB 4 — Validate
        // ═════════════════════════════════════════════════════════════════════

        private void DrawValidate()
        {
            Section("Inspector Null Reference Scan");
            HelpBox(
                "Scans every NightHunt UI component in the active scene for null [SerializeField] fields.\n" +
                "Also checks Config SO assets for null prefab refs.",
                MessageType.Info);

            if (GUILayout.Button("Run Full Validation", GUILayout.Height(32)))
                RunValidation();
        }

        private void RunValidation()
        {
            _log.Clear();
            int issueCount = 0;

            // Scan config assets
            var invCfg = AssetDatabase.LoadAssetAtPath<InventoryConfig>(ConfigPath + "/InventoryConfig.asset");
            var uiCfg  = AssetDatabase.LoadAssetAtPath<UISlotLayoutConfig>(ConfigPath + "/UISlotLayoutConfig.asset");
            if (invCfg == null) { Log("❌ InventoryConfig.asset not found!"); issueCount++; }
            else                  Log("✅ InventoryConfig.asset found.");
            if (uiCfg  == null) { Log("❌ UISlotLayoutConfig.asset not found!"); issueCount++; }
            else
            {
                Log("✅ UISlotLayoutConfig.asset found.");
                var soUI = new SerializedObject(uiCfg);
                CheckNullRefs(soUI, "UISlotLayoutConfig", ref issueCount);
            }

            // Scan scene components
            var types = new System.Type[]
            {
                typeof(UIRootController),
                typeof(DragDropController),
                typeof(CombatHUDPanel),
                typeof(InventoryScreen),
                typeof(WeaponEquipmentPanel),
                typeof(ItemContextMenu),
                typeof(PlayerHUDPanel),
                typeof(PlatformManager),
            };

            foreach (var t in types)
            {
                var comp = (Component)Object.FindFirstObjectByType(t);
                if (comp == null) { Log($"○  {t.Name} — not in scene."); continue; }
                var so = new SerializedObject(comp);
                CheckNullRefs(so, t.Name, ref issueCount);
            }

            // Summary
            if (issueCount == 0)
                Log("🎉 All checks passed — no null references found!");
            else
                Log($"⚠️  {issueCount} issue(s) need manual assignment in the Inspector.");
        }

        private void CheckNullRefs(SerializedObject so, string label, ref int issueCount)
        {
            var nullFields = new List<string>();
            var it = so.GetIterator();
            it.NextVisible(true);
            while (it.NextVisible(false))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference
                    && it.objectReferenceValue == null
                    && it.name != "m_Script")
                    nullFields.Add(it.name);
            }
            if (nullFields.Count == 0)
                Log($"  ✅ {label} — all refs wired.");
            else
            {
                Log($"  ⚠️  {label} — {nullFields.Count} null field(s): {string.Join(", ", nullFields)}");
                issueCount += nullFields.Count;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Log panel
        // ═════════════════════════════════════════════════════════════════════

        private void DrawLogPanel()
        {
            var rect = new Rect(0, position.height - 140, position.width, 140);
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.06f, 0.09f));
            GUI.Label(new Rect(rect.x + 6, rect.y + 3, 80, 16),
                "Log", EditorStyles.miniLabel);

            if (GUI.Button(new Rect(rect.xMax - 50, rect.y + 2, 46, 16), "Clear", EditorStyles.miniButton))
                _log.Clear();

            var logStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false };
            _logScroll = GUI.BeginScrollView(
                new Rect(rect.x + 2, rect.y + 20, rect.width - 4, rect.height - 22),
                _logScroll,
                new Rect(0, 0, rect.width - 20, Mathf.Max(rect.height - 22, _log.Count * 16)));

            for (int i = _log.Count - 1; i >= 0; i--)
                GUI.Label(new Rect(4, ((_log.Count - 1 - i) * 16), rect.width - 20, 16),
                    _log[i], logStyle);
            GUI.EndScrollView();
        }

        private void Log(string msg)
        {
            _log.Add(msg);
            Debug.Log($"[NightHuntUIWizard] {msg}");
            Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  GUI helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void Section(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetLastRect();
            rect.y    += rect.height;
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.5f, 0.8f, 0.4f));
            EditorGUILayout.Space(4);
        }

        private static void HelpBox(string msg, MessageType type) =>
            EditorGUILayout.HelpBox(msg, type);

        // ═════════════════════════════════════════════════════════════════════
        //  Scene helpers
        // ═════════════════════════════════════════════════════════════════════

        private static GameObject FindOrCreate<T>(string goName) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null) return existing.gameObject;
            var go = new GameObject(goName);
            go.layer = 5;
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
            return go;
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
            return go;
        }

        private static T EnsureComponent<T>(GameObject go, System.Action<T> configure) where T : Component
        {
            var c = go.GetComponent<T>() ?? go.AddComponent<T>();
            configure?.Invoke(c);
            EditorUtility.SetDirty(c);
            return c;
        }

        private static T EnsureComponentAndWire<T>(GameObject go, System.Action<T> configure) where T : Component =>
            EnsureComponent(go, configure);

        // ═════════════════════════════════════════════════════════════════════
        //  Prefab helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static Sprite LoadSprite(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return null;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in sprites)
                if (a is Sprite s) return s;
            // If not sliced, create from texture
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RectTransform helpers
        // ═════════════════════════════════════════════════════════════════════

        private static GameObject MakeChild(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void CenterAnchor(GameObject go, Vector2 anchoredPos)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = anchoredPos;
        }

        private static void SetTopStretchAnchor(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SerializedObject helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void SetProp(SerializedObject so, string path, Object val)
        {
            var p = so.FindProperty(path);
            if (p != null) p.objectReferenceValue = val;
        }

        private static void SetProp(SerializedObject so, string path, bool val)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = val;
        }

        private static void SetProp(SerializedObject so, string path, int val)
        {
            var p = so.FindProperty(path);
            if (p != null) p.intValue = val;
        }

        private static void SetProp(SerializedObject so, string path, float val)
        {
            var p = so.FindProperty(path);
            if (p != null) p.floatValue = val;
        }

        private static void SetProp(SerializedObject so, string path, Vector2 val)
        {
            var p = so.FindProperty(path);
            if (p != null) p.vector2Value = val;
        }

        private static void SetProp(SerializedObject so, string path, Color val)
        {
            var p = so.FindProperty(path);
            if (p != null) p.colorValue = val;
        }

        private static void SetArrayEnum(SerializedProperty arrayProp, int idx, string fieldName, int enumIdx)
        {
            var elem = arrayProp.GetArrayElementAtIndex(idx);
            var field = elem.FindPropertyRelative(fieldName);
            if (field != null) field.enumValueIndex = enumIdx;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  IO helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void EnsureDir(string assetPath)
        {
            var abs = Path.Combine(Application.dataPath, "..", assetPath);
            if (!Directory.Exists(abs))
            {
                Directory.CreateDirectory(abs);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
