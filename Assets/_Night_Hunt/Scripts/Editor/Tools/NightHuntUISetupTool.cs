using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.Gameplay.Input.Handlers.Movement;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// NightHunt UI Setup Tool
    ///
    /// Menu: NightHunt / Tools / Setup Full UI Hierarchy
    ///
    /// Create toàn bộ cây Canvas → Sub-panels hoàn chỉnh:
    ///   ① GameHUD_Canvas (Screen Space Overlay, sortOrder=0)
    ///       ├─ [PlayerHUD]     HP/Stamina/Armor/Speed/Weight sliders
    ///       ├─ [CombatHUD]     FireButton + WeaponSlots + QuickSlots (mobile joystick)
    ///       ├─ [Crosshair]     Aim crosshair dot
    ///       ├─ [Minimap]       RawImage + mask
    ///       ├─ [KillFeed]      Scrollable kill feed list
    ///       ├─ [BossHUD]       Boss HP bar + name (hidden by default)
    ///       ├─ [MatchUI]       Phase timer + team scores
    ///       ├─ [InteractionPrompt] "[E] Interact" label
    ///       └─ [DamageFeedback]   (empty — DamageFeedbackSystem places rects)
    ///
    ///   ② InventoryScreen_Canvas (Screen Space Overlay, sortOrder=1, hidden)
    ///       ├─ [InventoryScreen] Inventory grid + equipment slots + stat panel
    ///       └─ [DragDropGhost]  (script ref only)
    ///
    ///   ③ DeathScreen_Canvas (sortOrder=2, hidden)
    ///       ├─ [DeathOverlay]  Dark overlay
    ///       ├─ [KillerText]    "Killed by: X"
    ///       ├─ [BtnSpectate]   Spectate button
    ///       └─ [BtnWaitRespawn] Wait for Respawn button
    ///
    ///   ④ LootContainer_Canvas (sortOrder=3, hidden)
    ///
    ///   ⑤ PersistentUICanvas (DontDestroyOnLoad, sortOrder=10)
    ///       ├─ [LoadingOverlay]
    ///       ├─ [ToastService]
    ///       └─ [GameModalWindow]
    ///
    /// Tất cả GameObjects được tổ chức dưới [UI_Root] parent.
    ///
    /// SAU KHI GENERATE:
    ///   1. Gán các ScriptableObject configs (InventoryConfig, PlayerStatUIConfig)
    ///   2. Gán MinimapUI._minimapCamera + _minimapTexture qua Inspector
    ///   3. Gán KillFeedUI.killFeedItemPrefab
    ///   4. Kéo GameHUD.cs script vào GameHUD_Canvas root
    ///   5. Kéo các sub-panel refs vào GameHUD Inspector slots
    /// </summary>
    public static class NightHuntUISetupTool
    {
        // ── Common sizes ─────────────────────────────────────────────────────────
        private const float BarH   = 20f;
        private const float BtnSz  = 80f;
        private const float Pad    = 10f;

        // ── Colors ───────────────────────────────────────────────────────────────
        private static readonly Color C_HP       = new Color(0.2f, 0.8f, 0.2f, 1f);
        private static readonly Color C_Stamina  = new Color(0.2f, 0.6f, 1.0f, 1f);
        private static readonly Color C_Armor    = new Color(0.9f, 0.7f, 0.1f, 1f);
        private static readonly Color C_Boss     = new Color(0.9f, 0.2f, 0.2f, 1f);
        private static readonly Color C_Dark     = new Color(0f,   0f,   0f,   0.7f);
        private static readonly Color C_Panel    = new Color(0.05f,0.05f,0.05f,0.8f);
        private static readonly Color C_Btn      = new Color(0.15f,0.15f,0.15f,0.9f);

        // ── Menu entry ────────────────────────────────────────────────────────────

        [MenuItem("NightHunt/Tools/Setup Full UI Hierarchy", priority = 20)]
        public static void SetupUI()
        {
            if (!EditorUtility.DisplayDialog(
                    "NightHunt UI Setup",
                    "This will create (or refresh) the full UI hierarchy in the current scene.\n" +
                    "Existing [UI_Root] GameObject will be re-used if found.",
                    "Create", "Cancel")) return;

            // Find or create root
            var uiRoot = GameObject.Find("[UI_Root]") ?? new GameObject("[UI_Root]");
            Undo.RegisterCreatedObjectUndo(uiRoot, "Create UI Root");

            var log = new List<string> { "=== NightHunt UI Setup ===" };

            CreateGameHUDCanvas(uiRoot.transform, log);
            CreateInventoryCanvas(uiRoot.transform, log);
            CreateDeathScreenCanvas(uiRoot.transform, log);
            CreateLootContainerCanvas(uiRoot.transform, log);
            CreatePersistentUICanvas(uiRoot.transform, log);

            log.Add("\n✅ Done. Assign Inspector refs & prefabs before Play mode.");
            Debug.Log(string.Join("\n", log));

            EditorUtility.DisplayDialog(
                "UI Setup Complete",
                "Full UI hierarchy created.\n\n" +
                "NEXT STEPS:\n" +
                "1. Drag GameHUD.cs onto GameHUD_Canvas root\n" +
                "2. Wire sub-panel references in GameHUD Inspector\n" +
                "3. Assign MinimapCamera + MinimapTexture on MinimapUI\n" +
                "4. Assign KillFeedItemPrefab on KillFeedUI\n" +
                "5. Assign InventoryConfig, PlayerStatUIConfig SOs\n",
                "OK");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  ① GameHUD Canvas
        // ──────────────────────────────────────────────────────────────────────────

        private static void CreateGameHUDCanvas(Transform parent, List<string> log)
        {
            var go = GetOrCreate("GameHUD_Canvas", parent);
            var canvas = EnsureComp<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            EnsureComp<CanvasScaler>(go).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ((CanvasScaler)go.GetComponent<CanvasScaler>()).referenceResolution = new Vector2(1920, 1080);
            EnsureComp<GraphicRaycaster>(go);

            log.Add("  ✅ GameHUD_Canvas");

            // ── Player Stat HUD (top-left) ─────────────────────────────────────
            var playerHUD = GetOrCreate("[PlayerHUD]", go.transform);
            SetAnchors(playerHUD, Vector2.zero, new Vector2(0, 1));
            SetRectSize(playerHUD, new Vector2(260, 200));
            SetRectOffset(playerHUD, new Vector2(Pad, -Pad));
            AddImage(playerHUD, C_Panel);
            CreateStatBar(playerHUD.transform, "HP_Bar",     "HP",      C_HP,      0,  0);
            CreateStatBar(playerHUD.transform, "Stamina_Bar","Stamina", C_Stamina, 1,  0);
            CreateStatBar(playerHUD.transform, "Armor_Bar",  "Armor",   C_Armor,   2,  0);
            CreateLabel(playerHUD.transform,   "Weight_Text","Weight: 0/60 kg", 14, new Vector2(10, -135));
            CreateLabel(playerHUD.transform,   "Speed_Text", "Speed: 100%",     12, new Vector2(10, -158));
            log.Add("  ✅ [PlayerHUD] — HP/Stamina/Armor bars");

            // ── Boss HUD (top-center, hidden) ──────────────────────────────────
            var bossHUD = GetOrCreate("[BossHUD]", go.transform);
            bossHUD.SetActive(false);
            SetAnchors(bossHUD, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f));
            SetRectSize(bossHUD, new Vector2(0, 70));
            SetRectOffset(bossHUD, new Vector2(0, -Pad));
            AddImage(bossHUD, C_Panel);
            CreateLabel(bossHUD.transform,    "BossName_Text", "BOSS NAME", 18, new Vector2(0, -8));
            CreateStatBar(bossHUD.transform,  "BossHP_Bar",    "HP",  C_Boss, 0, 0, true);
            log.Add("  ✅ [BossHUD] — hidden by default");

            // ── Match UI (top-right) ───────────────────────────────────────────
            var matchUI = GetOrCreate("[MatchUI]", go.transform);
            SetAnchors(matchUI, new Vector2(1, 1), Vector2.one);
            SetRectSize(matchUI, new Vector2(220, 120));
            SetRectOffset(matchUI, new Vector2(-Pad, -Pad));
            AddImage(matchUI, C_Panel);
            CreateLabel(matchUI.transform, "Phase_Text",  "PHASE: Hunt",  16, new Vector2(-10, -10));
            CreateLabel(matchUI.transform, "Timer_Text",  "05:00",        24, new Vector2(-10, -38));
            CreateLabel(matchUI.transform, "Score_Text",  "0  |  0",  14, new Vector2(-10, -70));
            log.Add("  ✅ [MatchUI] — phase + timer + score");

            // ── KillFeed (right side) ──────────────────────────────────────────
            var killFeed = GetOrCreate("[KillFeed]", go.transform);
            SetAnchors(killFeed, new Vector2(1, 0.5f), Vector2.one);
            SetRectSize(killFeed, new Vector2(380, 300));
            SetRectOffset(killFeed, new Vector2(-Pad, -140));
            var kfScroll = EnsureComp<ScrollRect>(killFeed);
            var kfContent = GetOrCreate("Content", killFeed.transform);
            var kfVlg = EnsureComp<VerticalLayoutGroup>(kfContent);
            kfVlg.childAlignment = TextAnchor.UpperRight;
            kfVlg.spacing = 2f;
            EnsureComp<ContentSizeFitter>(kfContent).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            kfScroll.content = kfContent.GetComponent<RectTransform>();
            kfScroll.vertical = true; kfScroll.horizontal = false;
            kfScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            log.Add("  ✅ [KillFeed] — scrollable kill list");

            // ── Crosshair (center) ─────────────────────────────────────────────
            var xhair = GetOrCreate("[Crosshair]", go.transform);
            SetAnchors(xhair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            SetRectSize(xhair, new Vector2(20, 20));
            var xImg = EnsureComp<Image>(xhair);
            xImg.color = new Color(1, 1, 1, 0.85f);
            log.Add("  ✅ [Crosshair]");

            // ── Interaction Prompt (lower-center) ─────────────────────────────
            var iprompt = GetOrCreate("[InteractionPrompt]", go.transform);
            SetAnchors(iprompt, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            SetRectSize(iprompt, new Vector2(350, 50));
            SetRectOffset(iprompt, new Vector2(0, 100));
            iprompt.SetActive(false);
            AddImage(iprompt, C_Panel);
            CreateLabel(iprompt.transform, "Prompt_Text", "[E] Interact", 18, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;
            log.Add("  ✅ [InteractionPrompt]");

            // ── Minimap (bottom-right) ─────────────────────────────────────────
            var minimap = GetOrCreate("[Minimap]", go.transform);
            SetAnchors(minimap, Vector2.right, Vector2.one);
            SetRectSize(minimap, new Vector2(200, 200));
            SetRectOffset(minimap, new Vector2(-Pad - 200, -Pad - 200));
            AddImage(minimap, new Color(0, 0, 0, 0.5f));
            var mmRaw = GetOrCreate("MinimapImage", minimap.transform);
            SetAnchors(mmRaw, Vector2.zero, Vector2.one);
            SetRectPadding(mmRaw, 4);
            EnsureComp<RawImage>(mmRaw).color = Color.white;
            // Add circular mask
            var mmMask = GetOrCreate("MinimapMask", minimap.transform);
            SetAnchors(mmMask, Vector2.zero, Vector2.one);
            AddImage(mmMask, Color.white);
            EnsureComp<Mask>(mmMask).showMaskGraphic = false;
            log.Add("  ✅ [Minimap]");

            // ── CombatHUD (bottom area) ────────────────────────────────────────
            var combatHUD = GetOrCreate("[CombatHUD]", go.transform);
            SetAnchors(combatHUD, Vector2.zero, Vector2.right);
            SetRectSize(combatHUD, new Vector2(0, 220));
            AddImage(combatHUD, new Color(0, 0, 0, 0.01f)); // nearly invisible bg for layout

            // Fire Button (bottom-right of combat area) — Mobile only
            var fireBtn = GetOrCreate("[FireButton]", combatHUD.transform);
            SetAnchors(fireBtn, Vector2.right, Vector2.right);
            SetRectSize(fireBtn, new Vector2(120, 120));
            SetRectOffset(fireBtn, new Vector2(-140, 50));
            var fireBtnImg = AddImage(fireBtn, new Color(0.8f, 0.15f, 0.1f, 0.85f));
            fireBtnImg.raycastTarget = true;
            CreateLabel(fireBtn.transform, "FireLabel", "FIRE", 20, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;
            // FireButton.cs + VariableJoystick child for aim-while-fire
            var fbComp   = EnsureComp<FireButton>(fireBtn);
            var fjGo     = GetOrCreate("FireAimJoystick", fireBtn.transform);
            fjGo.SetActive(false);
            var fjBgGo   = GetOrCreate("Background", fjGo.transform);
            SetAnchors(fjBgGo, Vector2.zero, Vector2.one);
            AddImage(fjBgGo, new Color(1f, 1f, 1f, 0.2f));
            var fjHandGo = GetOrCreate("Handle", fjBgGo.transform);
            SetAnchors(fjHandGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            SetRectSize(fjHandGo, new Vector2(50f, 50f));
            AddImage(fjHandGo, new Color(1f, 1f, 1f, 0.5f));
            var fjComp   = EnsureComp<VariableJoystick>(fjGo);
            var fjSO     = new SerializedObject(fjComp);
            fjSO.FindProperty("background").objectReferenceValue = fjBgGo.GetComponent<RectTransform>();
            fjSO.FindProperty("handle").objectReferenceValue     = fjHandGo.GetComponent<RectTransform>();
            fjSO.ApplyModifiedProperties();
            var fbSO = new SerializedObject(fbComp);
            fbSO.FindProperty("_joystick").objectReferenceValue = fjComp;
            fbSO.ApplyModifiedProperties();

            // Weapon Slot Buttons row (bottom-center)
            var wSlotRow = GetOrCreate("[WeaponSlots]", combatHUD.transform);
            SetAnchors(wSlotRow, new Vector2(0.3f, 0), new Vector2(0.7f, 0));
            SetRectSize(wSlotRow, new Vector2(0, 100));
            SetRectOffset(wSlotRow, new Vector2(0, 10));
            var wHlg = EnsureComp<HorizontalLayoutGroup>(wSlotRow);
            wHlg.spacing = 8; wHlg.childForceExpandWidth = false;
            for (int i = 0; i < 3; i++)
            {
                var ws = GetOrCreate($"WeaponSlot_{i}", wSlotRow.transform);
                SetRectSize(ws, new Vector2(BtnSz, BtnSz));
                AddImage(ws, C_Btn);
                CreateLabel(ws.transform, "Label", i == 0 ? "P" : i == 1 ? "S" : "M", 16, Vector2.zero)
                    .alignment = TextAlignmentOptions.Center;
                var ammo = GetOrCreate("Ammo_Text", ws.transform);
                SetAnchors(ammo, Vector2.zero, Vector2.right);
                SetRectSize(ammo, new Vector2(0, 18));
                SetRectOffset(ammo, new Vector2(0, 4));
                CreateTMPText(ammo, "30/300", 11, Color.white);
            }

            // QuickSlot Buttons row (bottom-left)
            var qSlotRow = GetOrCreate("[QuickSlots]", combatHUD.transform);
            SetAnchors(qSlotRow, Vector2.zero, new Vector2(0.3f, 0));
            SetRectSize(qSlotRow, new Vector2(0, 100));
            SetRectOffset(qSlotRow, new Vector2(10, 10));
            var qHlg = EnsureComp<HorizontalLayoutGroup>(qSlotRow);
            qHlg.spacing = 8; qHlg.childForceExpandWidth = false;
            for (int i = 0; i < 4; i++)
            {
                var qs = GetOrCreate($"QuickSlot_{i}", qSlotRow.transform);
                SetRectSize(qs, new Vector2(BtnSz, BtnSz));
                AddImage(qs, C_Btn);
                var qIcon = GetOrCreate("Icon", qs.transform);
                SetAnchors(qIcon, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
                AddImage(qIcon, new Color(1,1,1,0.15f));
                var qCount = GetOrCreate("Count", qs.transform);
                SetAnchors(qCount, Vector2.right, Vector2.one);
                SetRectSize(qCount, new Vector2(24, 18));
                SetRectOffset(qCount, new Vector2(-2, -2));
                CreateTMPText(qCount, "0", 11, Color.yellow);
            }

            // Movement Joystick (left side, mobile) ───────────────────────────
            var joyLeft = GetOrCreate("[MoveJoystick]", go.transform);
            SetAnchors(joyLeft, Vector2.zero, new Vector2(0, 0));
            SetRectSize(joyLeft, new Vector2(200, 200));
            SetRectOffset(joyLeft, new Vector2(50, 50));
            // Background ring — separate child so FixedJoystick can reference it
            var joyBgGo = GetOrCreate("Background", joyLeft.transform);
            SetAnchors(joyBgGo, Vector2.zero, Vector2.one);
            var joyBgImg = AddImage(joyBgGo, new Color(1, 1, 1, 0.12f));
            joyBgImg.raycastTarget = true;
            // Handle thumb (inside Background)
            var jHandle = GetOrCreate("Handle", joyBgGo.transform);
            SetAnchors(jHandle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            SetRectSize(jHandle, new Vector2(70, 70));
            AddImage(jHandle, new Color(1, 1, 1, 0.5f));
            // FixedJoystick + MobileMovementBridge (both on root [MoveJoystick] GO)
            var fixedJoy = EnsureComp<FixedJoystick>(joyLeft);
            var joySO    = new SerializedObject(fixedJoy);
            joySO.FindProperty("background").objectReferenceValue = joyBgGo.GetComponent<RectTransform>();
            joySO.FindProperty("handle").objectReferenceValue     = jHandle.GetComponent<RectTransform>();
            joySO.ApplyModifiedProperties();
            var bridge   = EnsureComp<MobileMovementBridge>(joyLeft);
            var bridgeSO = new SerializedObject(bridge);
            bridgeSO.FindProperty("_joystick").objectReferenceValue = fixedJoy;
            bridgeSO.ApplyModifiedProperties();
            log.Add("  ✅ [CombatHUD] — FireButton(+script+joystick) + WeaponSlots + QuickSlots + MoveJoystick(FixedJoystick+Bridge)");

            // ── Damage Feedback (empty root — DamageFeedbackSystem places text) ──
            var dmgFB = GetOrCreate("[DamageFeedback]", go.transform);
            SetAnchors(dmgFB, Vector2.zero, Vector2.one);
            log.Add("  ✅ [DamageFeedback] anchor");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  ② Inventory Screen Canvas
        // ──────────────────────────────────────────────────────────────────────────

        private static void CreateInventoryCanvas(Transform parent, List<string> log)
        {
            var go = GetOrCreate("InventoryScreen_Canvas", parent);
            go.SetActive(false);
            var canvas = EnsureComp<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            EnsureComp<CanvasScaler>(go).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            EnsureComp<GraphicRaycaster>(go);

            // Full-screen dark overlay
            var overlay = GetOrCreate("[InventoryOverlay]", go.transform);
            SetAnchors(overlay, Vector2.zero, Vector2.one);
            AddImage(overlay, new Color(0,0,0,0.85f));

            // Grid area (left 60%)
            var gridArea = GetOrCreate("[GridArea]", overlay.transform);
            SetAnchors(gridArea, Vector2.zero, new Vector2(0.6f, 1f));
            SetRectPadding(gridArea, 20);
            AddImage(gridArea, C_Panel);
            // Grid scroll view
            var grid = GetOrCreate("InventoryGrid", gridArea.transform);
            SetAnchors(grid, Vector2.zero, Vector2.one);
            SetRectPadding(grid, 10);
            var glg = EnsureComp<GridLayoutGroup>(grid);
            glg.cellSize = new Vector2(64, 64);
            glg.spacing = new Vector2(4, 4);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 6;
            log.Add("  ✅ [InventoryScreen] — 6-column grid");

            // Equipment panel (right 40%)
            var eqArea = GetOrCreate("[EquipmentArea]", overlay.transform);
            SetAnchors(eqArea, new Vector2(0.6f, 0), Vector2.one);
            AddImage(eqArea, C_Panel);
            // Equipment slot placeholders
            string[] eqSlots = { "Head", "Body", "Legs", "Feet", "Back" };
            for (int i = 0; i < eqSlots.Length; i++)
            {
                var slot = GetOrCreate($"EqSlot_{eqSlots[i]}", eqArea.transform);
                SetAnchors(slot, new Vector2(0.1f, 0), new Vector2(0.9f, 0));
                SetRectSize(slot, new Vector2(0, 64));
                SetRectOffset(slot, new Vector2(0, 10 + i * 74));
                AddImage(slot, C_Btn);
                CreateLabel(slot.transform, "SlotName", eqSlots[i], 14, new Vector2(-10, 0));
            }

            // Stat panel (bottom of equipment area)
            var statPanel = GetOrCreate("[StatPanel]", eqArea.transform);
            SetAnchors(statPanel, new Vector2(0, 0), new Vector2(1, 0));
            SetRectSize(statPanel, new Vector2(0, 200));
            AddImage(statPanel, new Color(0.03f, 0.03f, 0.03f, 0.9f));
            string[] statNames = { "Health", "Stamina", "Armor", "Speed", "Weight" };
            for (int i = 0; i < statNames.Length; i++)
            {
                var row = GetOrCreate($"Stat_{statNames[i]}", statPanel.transform);
                SetAnchors(row, Vector2.zero, Vector2.right);
                SetRectSize(row, new Vector2(0, 26));
                SetRectOffset(row, new Vector2(10, 10 + i * 32));
                CreateLabel(row.transform, "Name", statNames[i], 12, new Vector2(0, 0));
                CreateLabel(row.transform, "Value", "100", 12, new Vector2(-10, 0));
            }

            // Close button
            var closeBtn = GetOrCreate("[CloseBtn]", go.transform);
            SetAnchors(closeBtn, Vector2.one, Vector2.one);
            SetRectSize(closeBtn, new Vector2(50, 50));
            SetRectOffset(closeBtn, new Vector2(-10, -10));
            AddImage(closeBtn, new Color(0.7f, 0.1f, 0.1f, 0.9f));
            CreateLabel(closeBtn.transform, "X", "×", 22, Vector2.zero).alignment = TextAlignmentOptions.Center;

            log.Add("  ✅ InventoryScreen_Canvas");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  ③ Death Screen Canvas
        // ──────────────────────────────────────────────────────────────────────────

        private static void CreateDeathScreenCanvas(Transform parent, List<string> log)
        {
            var go = GetOrCreate("DeathScreen_Canvas", parent);
            go.SetActive(false);
            var canvas = EnsureComp<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            EnsureComp<CanvasScaler>(go).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            EnsureComp<GraphicRaycaster>(go);

            // Dark overlay
            var overlay = GetOrCreate("[DeathOverlay]", go.transform);
            SetAnchors(overlay, Vector2.zero, Vector2.one);
            AddImage(overlay, new Color(0.5f, 0, 0, 0.7f));

            // Center panel
            var panel = GetOrCreate("[DeathPanel]", go.transform);
            SetAnchors(panel, new Vector2(0.25f, 0.3f), new Vector2(0.75f, 0.8f));
            AddImage(panel, C_Panel);

            CreateLabel(panel.transform, "YouDied", "YOU DIED", 36, new Vector2(0, -30))
                .alignment = TextAlignmentOptions.Center;
            CreateLabel(panel.transform, "KillerText", "Killed by: Unknown", 18, new Vector2(0, -80))
                .alignment = TextAlignmentOptions.Center;

            // Buttons
            string[] btnLabels = { "Spectate", "Wait for Respawn", "Back to Lobby" };
            for (int i = 0; i < btnLabels.Length; i++)
            {
                var btn = GetOrCreate($"Btn_{btnLabels[i].Replace(" ", "")}", panel.transform);
                SetAnchors(btn, new Vector2(0.1f, 0), new Vector2(0.9f, 0));
                SetRectSize(btn, new Vector2(0, 50));
                SetRectOffset(btn, new Vector2(0, 30 + i * 60));
                AddImage(btn, C_Btn);
                EnsureComp<Button>(btn);
                CreateLabel(btn.transform, "Label", btnLabels[i], 16, Vector2.zero)
                    .alignment = TextAlignmentOptions.Center;
            }

            log.Add("  ✅ DeathScreen_Canvas");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  ④ Loot Container Canvas
        // ──────────────────────────────────────────────────────────────────────────

        private static void CreateLootContainerCanvas(Transform parent, List<string> log)
        {
            var go = GetOrCreate("LootContainer_Canvas", parent);
            go.SetActive(false);
            var canvas = EnsureComp<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2;
            EnsureComp<CanvasScaler>(go).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            EnsureComp<GraphicRaycaster>(go);

            var panel = GetOrCreate("[LootPanel]", go.transform);
            SetAnchors(panel, new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.85f));
            AddImage(panel, C_Panel);

            CreateLabel(panel.transform, "Title", "LOOT", 20, new Vector2(0, -20))
                .alignment = TextAlignmentOptions.Center;

            var scroll = GetOrCreate("LootScroll", panel.transform);
            SetAnchors(scroll, new Vector2(0, 0.1f), new Vector2(1, 0.85f));
            EnsureComp<ScrollRect>(scroll).vertical = true;
            var content = GetOrCreate("Content", scroll.transform);
            var vlg = EnsureComp<VerticalLayoutGroup>(content);
            vlg.spacing = 4; vlg.padding = new RectOffset(8, 8, 8, 8);
            EnsureComp<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var closeBtn = GetOrCreate("CloseBtn", panel.transform);
            SetAnchors(closeBtn, new Vector2(0, 0), new Vector2(1, 0.1f));
            AddImage(closeBtn, new Color(0.6f, 0.1f, 0.1f, 0.9f));
            EnsureComp<Button>(closeBtn);
            CreateLabel(closeBtn.transform, "Label", "Close", 16, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;

            log.Add("  ✅ LootContainer_Canvas");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  ⑤ Persistent UI Canvas
        // ──────────────────────────────────────────────────────────────────────────

        private static void CreatePersistentUICanvas(Transform parent, List<string> log)
        {
            var go = GetOrCreate("PersistentUICanvas", parent);
            var canvas = EnsureComp<Canvas>(go);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            EnsureComp<CanvasScaler>(go).uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            EnsureComp<GraphicRaycaster>(go);

            // Loading Overlay
            var loading = GetOrCreate("[LoadingOverlay]", go.transform);
            SetAnchors(loading, Vector2.zero, Vector2.one);
            loading.SetActive(false);
            AddImage(loading, C_Dark);
            CreateLabel(loading.transform, "LoadingText", "Loading...", 24, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;

            // Toast Notification
            var toast = GetOrCreate("[ToastService]", go.transform);
            SetAnchors(toast, new Vector2(0.25f, 0), new Vector2(0.75f, 0));
            SetRectSize(toast, new Vector2(0, 60));
            SetRectOffset(toast, new Vector2(0, 100));
            toast.SetActive(false);
            AddImage(toast, new Color(0.15f, 0.15f, 0.15f, 0.95f));
            CreateLabel(toast.transform, "ToastText", "Toast Message", 15, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;

            // Modal Window
            var modal = GetOrCreate("[GameModalWindow]", go.transform);
            modal.SetActive(false);
            SetAnchors(modal, Vector2.zero, Vector2.one);
            AddImage(modal, C_Dark);
            var modalPanel = GetOrCreate("ModalPanel", modal.transform);
            SetAnchors(modalPanel, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
            AddImage(modalPanel, C_Panel);
            CreateLabel(modalPanel.transform, "Title", "Confirm?", 20, new Vector2(0, -30))
                .alignment = TextAlignmentOptions.Center;
            var btnOK = GetOrCreate("BtnOK", modalPanel.transform);
            SetAnchors(btnOK, new Vector2(0.1f, 0.1f), new Vector2(0.45f, 0.25f));
            AddImage(btnOK, new Color(0.1f, 0.55f, 0.1f));
            EnsureComp<Button>(btnOK);
            CreateLabel(btnOK.transform, "Label", "OK", 16, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;
            var btnCancel = GetOrCreate("BtnCancel", modalPanel.transform);
            SetAnchors(btnCancel, new Vector2(0.55f, 0.1f), new Vector2(0.9f, 0.25f));
            AddImage(btnCancel, new Color(0.55f, 0.1f, 0.1f));
            EnsureComp<Button>(btnCancel);
            CreateLabel(btnCancel.transform, "Label", "Cancel", 16, Vector2.zero)
                .alignment = TextAlignmentOptions.Center;

            log.Add("  ✅ PersistentUICanvas (LoadingOverlay + ToastService + GameModalWindow)");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private static GameObject GetOrCreate(string name, Transform parent)
        {
            var existing = parent?.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static T EnsureComp<T>(GameObject go) where T : Component
        {
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var img = EnsureComp<Image>(go);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void SetRectSize(GameObject go, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = size;
        }

        private static void SetRectOffset(GameObject go, Vector2 pos)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;
        }

        private static void SetRectPadding(GameObject go, float padding)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(padding, padding);
                rt.offsetMax = new Vector2(-padding, -padding);
            }
        }

        /// <summary>
        /// Create một thanh stat (Slider) + label trong parent.
        /// rowIndex = 0, 1, 2… dùng để tính Y offset.
        /// </summary>
        private static void CreateStatBar(Transform parent, string goName, string label, Color fillColor,
                                          int rowIndex, float xOffset, bool fullWidth = false)
        {
            float yOffset = -15f - rowIndex * 52f;

            var row = GetOrCreate(goName + "_Row", parent).transform;
            SetAnchors(row.gameObject, new Vector2(0, 1), new Vector2(1, 1));
            SetRectSize(row.gameObject, new Vector2(-20, 44));
            SetRectOffset(row.gameObject, new Vector2(10, yOffset));

            // Label
            var lbl = GetOrCreate("Label", row);
            SetAnchors(lbl.gameObject, Vector2.zero, new Vector2(0, 1));
            SetRectSize(lbl.gameObject, new Vector2(70, 0));
            CreateTMPText(lbl.gameObject, label, 13, Color.white);

            // Slider
            var sliderGO = GetOrCreate("Slider", row);
            SetAnchors(sliderGO.gameObject, new Vector2(0, 0), Vector2.one);
            SetRectOffset(sliderGO.gameObject, new Vector2(72, 0));
            var slider = EnsureComp<Slider>(sliderGO.gameObject);
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            slider.transition = Selectable.Transition.None;

            var bgImg = GetOrCreate("Background", sliderGO.transform);
            SetAnchors(bgImg.gameObject, Vector2.zero, Vector2.one);
            AddImage(bgImg.gameObject, new Color(0.15f, 0.15f, 0.15f, 1f));

            var fillArea = GetOrCreate("Fill Area", sliderGO.transform);
            SetAnchors(fillArea.gameObject, Vector2.zero, Vector2.one);
            var fill = GetOrCreate("Fill", fillArea.transform);
            SetAnchors(fill.gameObject, Vector2.zero, Vector2.one);
            AddImage(fill.gameObject, fillColor);
            slider.fillRect = fill.GetComponent<RectTransform>();

            // Value text
            var valTxt = GetOrCreate("Value_Text", row);
            SetAnchors(valTxt.gameObject, Vector2.right, new Vector2(1, 1));
            SetRectSize(valTxt.gameObject, new Vector2(55, 0));
            SetRectOffset(valTxt.gameObject, new Vector2(-5, 0));
            CreateTMPText(valTxt.gameObject, "100/100", 11, Color.white);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string goName, string text,
                                                   int size, Vector2 anchorPos)
        {
            var go = GetOrCreate(goName, parent).transform;
            SetAnchors(go.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            SetRectSize(go.gameObject, new Vector2(180, 30));
            SetRectOffset(go.gameObject, anchorPos);
            return CreateTMPText(go.gameObject, text, size, Color.white);
        }

        private static TextMeshProUGUI CreateTMPText(GameObject go, string text, int size, Color color)
        {
            var tmp = EnsureComp<TextMeshProUGUI>(go);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
