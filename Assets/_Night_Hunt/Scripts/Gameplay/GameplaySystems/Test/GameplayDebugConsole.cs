using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameplaySystems.Core;
using GameplaySystems.Core.Data;
using GameplaySystems.Inventory;
using GameplaySystems.Stat;

namespace GameplaySystems.Tests
{
    /// <summary>
    /// 3-tab runtime debug console.
    ///
    /// ▸ KHÔNG gắn vào Player prefab – đặt trên GameObject riêng trong scene (e.g. "DebugManager").
    /// ▸ Lấy player qua: SpectateManager.Instance.GetLocalPlayer() → NetworkPlayer.GameplayBridge
    /// ▸ Mọi action đều qua IGameplayBridge, KHÔNG gọi thẳng system.
    /// ▸ No EditorStyles – tất cả style tạo runtime (chạy được trong build).
    ///
    /// Tab 0 – Commands : buttons thao tác nhanh + text-field assign
    /// Tab 1 – Overview : grid realtime inventory / equipment / weapon / quickslot + inline actions
    /// Tab 2 – Stats    : bảng stat đầy đủ có bar, modifier highlight, item-use state
    ///
    /// Toggle: F1.  Window: draggable, mỗi tab có scrollview riêng.
    /// </summary>
    public class GameplayDebugConsole : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Toggle")]
        [SerializeField] private KeyCode _toggleKey    = KeyCode.F1;
        [SerializeField] private bool    _startVisible = false;

        [Header("Window")]
        [SerializeField] private float _winW = 660f;
        [SerializeField] private float _winH = 780f;

        // ── State ──────────────────────────────────────────────────────────────
        private bool    _visible;
        private int     _tab;          // 0 | 1 | 2
        private Rect    _rect;
        private Vector2 _sv0, _sv1, _sv2;

        // Tab-0 text fields
        private string   _addDefID  = "weapon_ak47";
        private string   _rmDefID   = "weapon_ak47";
        private string   _swapID1   = "";
        private string   _swapID2   = "";
        private string   _eqDefID   = "armor_vest";
        private string   _wDef1     = "weapon_ak47";
        private string   _wDef2     = "weapon_ak47";
        private string[] _qsDef     = { "consumable_medkit", "consumable_medkit",
                                         "consumable_medkit", "consumable_medkit" };

        // ── Styles (built once at first OnGUI) ────────────────────────────────
        private bool     _stylesReady;
        private GUIStyle _sWin, _sHdr, _sSec, _sLbl, _sTiny, _sTF;
        private GUIStyle _sTabOn, _sTabOff;
        private GUIStyle _bGray, _bGreen, _bRed, _bBlue, _bOrange;
        private Texture2D _lineTex;

        // ─────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _visible = _startVisible;
            _rect    = new Rect(20, 20, _winW, _winH);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey)) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();
            _rect = GUI.Window(9901, _rect, DrawWindow, GUIContent.none, _sWin);
            // Keep on screen
            _rect.x = Mathf.Clamp(_rect.x, 0, Screen.width  - _rect.width);
            _rect.y = Mathf.Clamp(_rect.y, 0, Screen.height - _rect.height);
        }

        // ══════════════════════════════════════════════════════════════════════
        private void DrawWindow(int _id)
        {
            var bridge = GetBridge();

            // ── Title bar ──────────────────────────────────────────────────────
            GUILayout.BeginHorizontal(_sHdr);
            GUILayout.Label("⚙  GAMEPLAY DEBUG CONSOLE", _sHdr, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            string ps = bridge != null
                ? "<color=#55ff88>● Player OK</color>"
                : "<color=#ff4444>○ No Player</color>";
            GUILayout.Label(ps, _sTiny);
            GUILayout.Space(6);
            if (GUILayout.Button("✕", _bRed, GUILayout.Width(24), GUILayout.Height(22)))
                _visible = false;
            GUILayout.EndHorizontal();

            // ── Tab strip ──────────────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            DrawTab(0, "① Commands");
            DrawTab(1, "② Overview");
            DrawTab(2, "③ Stats");
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            // ── Tab content ────────────────────────────────────────────────────
            switch (_tab)
            {
                case 0: Tab0_Commands(bridge); break;
                case 1: Tab1_Overview(bridge);  break;
                case 2: Tab2_Stats(bridge);     break;
            }

            GUI.DragWindow(new Rect(0, 0, _winW, 30));
        }

        // ══════════════════════════════════════════════════════════════════════
        #region TAB 0 – Commands

        private void Tab0_Commands(IGameplayBridge b)
        {
            float contentH = _winH - 108f;
            _sv0 = GUILayout.BeginScrollView(_sv0, false, true, GUILayout.Height(contentH));

            // ── INVENTORY ─────────────────────────────────────────────────────
            Sec("📦  INVENTORY");

            // Add quick buttons
            GUILayout.BeginHorizontal();
            Btn("+ AK-47",      _bGreen, () => b?.AddItem("weapon_ak47", 1));
            Btn("+ Vest",       _bGreen, () => b?.AddItem("armor_vest", 1));
            Btn("+ Helmet",     _bGreen, () => b?.AddItem("armor_helmet", 1));
            Btn("+ Backpack",   _bGreen, () => b?.AddItem("armor_backpack", 1));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Btn("+ Medkit ×5",  _bGreen,  () => b?.AddItem("consumable_medkit", 5));
            Btn("+ Grenade ×3", _bGreen,  () => b?.AddItem("throwable_grenade", 3));
            Btn("+ Scope",      _bGreen,  () => b?.AddItem("attachment_reddot", 1));
            Btn("Clear All",    _bRed,    () => b?.ClearInventory());
            GUILayout.EndHorizontal();

            // Add by ID
            TFRow("Add DefID:",  ref _addDefID, "+ Add",
                () => b?.AddItem(_addDefID, 1), _bGreen, 120);

            // Remove by DefID
            TFRow("Remove DefID:", ref _rmDefID, "Remove",
                () => b?.RemoveItemByDef(_rmDefID, 1), _bRed, 90);

            // Swap by InstanceIDs
            GUILayout.BeginHorizontal();
            GUILayout.Label("Swap ID1:", _sTiny, GUILayout.Width(60));
            _swapID1 = GUILayout.TextField(_swapID1, _sTF, GUILayout.Width(130));
            GUILayout.Label("ID2:", _sTiny, GUILayout.Width(28));
            _swapID2 = GUILayout.TextField(_swapID2, _sTF, GUILayout.Width(130));
            if (GUILayout.Button("Swap", _bOrange, GUILayout.Width(56), GUILayout.Height(22)))
                b?.SwapItems(_swapID1, _swapID2);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Btn("Log Items",  _bGray, () => b?.GetAllItems().ToList().ForEach(i =>
                Debug.Log($"  {ItemDatabase.GetDefinition(i.DefinitionID)?.DisplayName ?? i.DefinitionID}" +
                          $" ×{i.Quantity}  [{i.InstanceID}]")));
            Btn("Log Weight", _bGray, () => {
                if (b == null) return;
                var w = b.GetWeightInfo();
                Debug.Log($"[Weight] {w.current:F1} / {w.capacity:F1} ({w.percent:P0})");
            });
            GUILayout.EndHorizontal();

            Space();

            // ── EQUIPMENT ─────────────────────────────────────────────────────
            Sec("👔  EQUIPMENT");

            TFRow("DefID:", ref _eqDefID, "Add & Equip",
                () => b?.AddAndEquip(_eqDefID), _bBlue, 110);

            GUILayout.BeginHorizontal();
            Btn("Vest",    _bBlue, () => b?.AddAndEquip("armor_vest"));
            Btn("Helmet",  _bBlue, () => b?.AddAndEquip("armor_helmet"));
            Btn("Backpack",_bBlue, () => b?.AddAndEquip("armor_backpack"));
            Btn("Unequip All", _bRed, () => b?.UnequipAll());
            GUILayout.EndHorizontal();

            // Per-slot unequip
            GUILayout.BeginHorizontal();
            foreach (EquipmentSlotType s in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                if (GUILayout.Button($"Rm {s}", _bRed, GUILayout.Height(21)))
                    b?.UnequipItem(s);
            }
            GUILayout.EndHorizontal();

            Space();

            // ── WEAPONS ───────────────────────────────────────────────────────
            Sec("🔫  WEAPONS");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Slot 1:", _sTiny, GUILayout.Width(46));
            _wDef1 = GUILayout.TextField(_wDef1, _sTF, GUILayout.Width(180));
            if (GUILayout.Button("Assign → Weapon 1", _bBlue, GUILayout.Width(140), GUILayout.Height(22)))
                b?.AddAndEquipWeapon(_wDef1, WeaponSlotType.Primary);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Slot 2:", _sTiny, GUILayout.Width(46));
            _wDef2 = GUILayout.TextField(_wDef2, _sTF, GUILayout.Width(180));
            if (GUILayout.Button("Assign → Weapon 2", _bBlue, GUILayout.Width(140), GUILayout.Height(22)))
                b?.AddAndEquipWeapon(_wDef2, WeaponSlotType.Secondary);
            GUILayout.EndHorizontal();

            // Select / Holster
            GUILayout.BeginHorizontal();
            Btn("Select Weapon 1", _bGreen,  () => b?.SelectWeapon(WeaponSlotType.Primary));
            Btn("Select Weapon 2", _bGreen,  () => b?.SelectWeapon(WeaponSlotType.Secondary));
            Btn("Select Melee",    _bGreen,  () => b?.SelectWeapon(WeaponSlotType.Melee));
            Btn("Holster (Idle)",  _bOrange, () => b?.HolsterWeapon());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Btn("Reload 1",    _bGray, () => b?.Reload(WeaponSlotType.Primary));
            Btn("Reload 2",    _bGray, () => b?.Reload(WeaponSlotType.Secondary));
            Btn("Unequip W1",  _bRed,  () => b?.UnequipWeapon(WeaponSlotType.Primary));
            Btn("Unequip W2",  _bRed,  () => b?.UnequipWeapon(WeaponSlotType.Secondary));
            GUILayout.EndHorizontal();

            Space();

            // ── QUICKSLOTS ────────────────────────────────────────────────────
            Sec("⚡  QUICKSLOTS");

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var cur = b?.QuickSlot?.GetQuickSlotItem(idx);
                string curText = cur != null
                    ? $"← {ItemDatabase.GetDefinition(cur.DefinitionID)?.DisplayName ?? cur.DefinitionID} ×{cur.Quantity}"
                    : "← (empty)";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"QS{idx + 1}:", _sTiny, GUILayout.Width(30));
                _qsDef[idx] = GUILayout.TextField(_qsDef[idx], _sTF, GUILayout.Width(148));
                if (GUILayout.Button("Add & Assign", _bBlue, GUILayout.Width(96), GUILayout.Height(22)))
                    b?.AddAndAssignQuickSlot(_qsDef[idx], idx);
                if (GUILayout.Button("Clear", _bRed, GUILayout.Width(44), GUILayout.Height(22)))
                    b?.RemoveFromQuickSlot(idx);
                GUILayout.Label(curText, _sTiny, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);

            // Use item buttons
            GUILayout.BeginHorizontal();
            Btn("Use QS1", _bOrange, () => b?.UseQuickSlot(0));
            Btn("Use QS2", _bOrange, () => b?.UseQuickSlot(1));
            Btn("Use QS3", _bOrange, () => b?.UseQuickSlot(2));
            Btn("Use QS4", _bOrange, () => b?.UseQuickSlot(3));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Btn("Cancel Use",    _bRed,    () => b?.CancelItemUse());
            Btn("Execute Throw", _bOrange, () => b?.ExecuteThrow());
            Btn("Clear All QS",  _bRed,    () => b?.QuickSlot?.ClearAllQuickSlots());
            GUILayout.EndHorizontal();

            Space();

            // ── SCENARIOS ─────────────────────────────────────────────────────
            Sec("🎮  SCENARIOS");

            GUILayout.BeginHorizontal();
            Btn("Full Loadout",  _bGreen,  () => b?.ScenarioFullLoadout());
            Btn("Overweight",    _bOrange, () => b?.ScenarioOverweight());
            Btn("Log All Stats", _bGray, () =>
            {
                if (b == null) return;
                foreach (var kv in b.GetAllStats().OrderBy(k => k.Key.ToString()))
                    Debug.Log($"  {kv.Key,-20} cur={kv.Value:F2}  base={b.GetBaseStat(kv.Key):F2}" +
                              $"  mod={b.GetStatModifier(kv.Key):+0.##;-0.##;0}");
            });
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region TAB 1 – Overview

        private void Tab1_Overview(IGameplayBridge b)
        {
            float contentH = _winH - 108f;
            _sv1 = GUILayout.BeginScrollView(_sv1, false, true, GUILayout.Height(contentH));

            if (b == null) { GUILayout.Label("No player.", _sTiny); GUILayout.EndScrollView(); return; }

            float col = (_winW - 28f) / 2f;

            // ── Row A: Inventory | Equipment ─────────────────────────────────
            GUILayout.BeginHorizontal();

            // INVENTORY
            GUILayout.BeginVertical(GUILayout.Width(col));
            Sec("📦 Inventory");
            var wInf = b.GetWeightInfo();
            GUILayout.Label($"Weight: {wInf.current:F1}/{wInf.capacity:F1}  ({wInf.percent:P0})", _sTiny);
            HLine();
            var items = b.GetAllItems();
            if (items.Count == 0) GUILayout.Label("  (empty)", _sTiny);
            else
            {
                foreach (var item in items)
                {
                    string nm = ItemDatabase.GetDefinition(item.DefinitionID)?.DisplayName ?? item.DefinitionID;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{nm} ×{item.Quantity}", _sLbl, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Eq",   _bBlue,  GUILayout.Width(26), GUILayout.Height(20))) b.EquipItem(item.InstanceID);
                    if (GUILayout.Button("⚡",   _bOrange,GUILayout.Width(22), GUILayout.Height(20)))
                    {
                        // assign to first free quickslot
                        for (int s = 0; s < 4; s++)
                            if (b.QuickSlot?.GetQuickSlotItem(s) == null)
                            { b.AssignToQuickSlot(item.InstanceID, s); break; }
                    }
                    if (GUILayout.Button("🗑",   _bRed,   GUILayout.Width(22), GUILayout.Height(20))) b.DropItem(item.InstanceID, 1);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // EQUIPMENT
            GUILayout.BeginVertical(GUILayout.Width(col));
            Sec("👔 Equipment");
            HLine();
            var eqMap = b.GetAllEquipped();
            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                eqMap.TryGetValue(slot, out var ei);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{slot,-9}", _sTiny, GUILayout.Width(68));
                if (ei != null)
                {
                    string nm = ItemDatabase.GetDefinition(ei.DefinitionID)?.DisplayName ?? ei.DefinitionID;
                    GUILayout.Label(nm, _sLbl, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Rm", _bRed, GUILayout.Width(26), GUILayout.Height(20)))
                        b.UnequipItem(slot);
                }
                else GUILayout.Label("—", _sTiny, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // ── Row B: Weapons | QuickSlots ──────────────────────────────────
            GUILayout.BeginHorizontal();

            // WEAPONS
            GUILayout.BeginVertical(GUILayout.Width(col));
            Sec("🔫 Weapons");
            HLine();
            var wepMap = b.GetAllWeapons();
            var active = b.GetActiveSlot();
            foreach (WeaponSlotType slot in Enum.GetValues(typeof(WeaponSlotType)))
            {
                wepMap.TryGetValue(slot, out var wi);
                bool isAct = active == slot;
                GUILayout.BeginHorizontal();
                GUILayout.Label((isAct ? "▶ " : "   ") + $"{slot,-9}", _sTiny, GUILayout.Width(82));
                if (wi != null)
                {
                    string nm = ItemDatabase.GetDefinition(wi.DefinitionID)?.DisplayName ?? wi.DefinitionID;
                    GUILayout.Label(nm, _sLbl, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Sel", _bGreen,  GUILayout.Width(30), GUILayout.Height(20))) b.SelectWeapon(slot);
                    if (GUILayout.Button("Rm",  _bRed,    GUILayout.Width(26), GUILayout.Height(20))) b.UnequipWeapon(slot);
                }
                else GUILayout.Label("—", _sTiny, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (active.HasValue && GUILayout.Button("Holster", _bOrange, GUILayout.Height(22))) b.HolsterWeapon();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // QUICKSLOTS
            GUILayout.BeginVertical(GUILayout.Width(col));
            Sec("⚡ QuickSlots");
            HLine();
            int qc = b.QuickSlot?.GetQuickSlotCount() ?? 4;
            for (int i = 0; i < qc; i++)
            {
                int idx = i;
                var qi = b.QuickSlot?.GetQuickSlotItem(idx);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"[{idx + 1}]", _sTiny, GUILayout.Width(22));
                if (qi != null)
                {
                    string nm = ItemDatabase.GetDefinition(qi.DefinitionID)?.DisplayName ?? qi.DefinitionID;
                    GUILayout.Label($"{nm} ×{qi.Quantity}", _sLbl, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Use", _bOrange, GUILayout.Width(34), GUILayout.Height(20))) b.UseQuickSlot(idx);
                    if (GUILayout.Button("Rm",  _bRed,    GUILayout.Width(26), GUILayout.Height(20))) b.RemoveFromQuickSlot(idx);
                }
                else GUILayout.Label("—", _sTiny, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            Btn("Cancel Use",    _bRed,    () => b.CancelItemUse());
            Btn("Execute Throw", _bOrange, () => b.ExecuteThrow());
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region TAB 2 – Stats

        private void Tab2_Stats(IGameplayBridge b)
        {
            float contentH = _winH - 108f;
            _sv2 = GUILayout.BeginScrollView(_sv2, false, true, GUILayout.Height(contentH));

            if (b == null) { GUILayout.Label("No player.", _sTiny); GUILayout.EndScrollView(); return; }

            // ── Stat table ────────────────────────────────────────────────────
            Sec("📊  Player Stats");

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Label("Stat",          _sTiny, GUILayout.Width(160));
            GUILayout.Label("Current",       _sTiny, GUILayout.Width(80));
            GUILayout.Label("Base",          _sTiny, GUILayout.Width(80));
            GUILayout.Label("Modifier",      _sTiny, GUILayout.Width(80));
            GUILayout.Label("Bar",           _sTiny, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            HLine();

            foreach (var kv in b.GetAllStats().OrderBy(k => k.Key.ToString()))
            {
                float cur = kv.Value;
                float bas = b.GetBaseStat(kv.Key);
                float mod = b.GetStatModifier(kv.Key);

                Color prev = GUI.color;

                GUILayout.BeginHorizontal();
                GUILayout.Label(kv.Key.ToString(), _sLbl, GUILayout.Width(160));

                // Current (coloured by modifier)
                GUI.color = mod > 0.01f ? new Color(0.4f, 1f, 0.5f)
                           : mod < -0.01f ? new Color(1f, 0.45f, 0.45f)
                                           : new Color(0.88f, 0.88f, 0.88f);
                GUILayout.Label($"{cur:F2}", _sLbl, GUILayout.Width(80));
                GUI.color = prev;

                GUILayout.Label($"{bas:F2}", _sTiny, GUILayout.Width(80));

                // Modifier with sign
                string ms = mod >= 0 ? $"+{mod:F2}" : $"{mod:F2}";
                GUI.color = mod > 0.01f ? new Color(0.4f, 1f, 0.5f)
                           : mod < -0.01f ? new Color(1f, 0.45f, 0.45f)
                                           : new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label(ms, _sTiny, GUILayout.Width(80));
                GUI.color = prev;

                // Inline mini-bar (only if max > 0)
                Rect barRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                if (bas > 0)
                {
                    float statPct = Mathf.Clamp01(cur / bas);
                    GUI.DrawTexture(barRect, Tex(new Color(0.18f, 0.18f, 0.2f)));
                    Color bc = statPct >= 0.8f ? new Color(0.25f, 0.78f, 0.3f)
                              : statPct >= 0.4f ? new Color(0.9f, 0.72f, 0.1f)
                                            : new Color(0.9f, 0.2f, 0.2f);
                    GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * statPct, barRect.height), Tex(bc));
                }

                GUILayout.EndHorizontal();
            }

            HLine();
            GUILayout.Space(4);

            // ── Weight summary ────────────────────────────────────────────────
            Sec("⚖  Weight & Movement");
            float wc  = b.GetCurrentWeight();
            float cap = b.GetWeightCapacity();
            float pct = b.GetWeightPercent();
            float spd = b.GetMovementSpeedMultiplier();

            GUILayout.Label($"Carrying : {wc:F1} kg  /  {cap:F1} kg  ({pct:P0})", _sLbl);
            GUILayout.Label($"Speed ×  : {spd:P0}", _sLbl);

            // Weight bar
            Rect wBar = GUILayoutUtility.GetRect(_winW - 24, 16, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(wBar, Tex(new Color(0.16f, 0.16f, 0.18f)));
            Color wCol = pct < 0.8f ? new Color(0.25f, 0.78f, 0.3f)
                        : pct < 1f  ? new Color(0.9f,  0.72f, 0.1f)
                                    : new Color(0.9f,  0.22f, 0.22f);
            GUI.DrawTexture(new Rect(wBar.x, wBar.y, wBar.width * Mathf.Clamp01(pct), wBar.height), Tex(wCol));
            GUILayout.Space(4);

            // ── ItemUse live status ───────────────────────────────────────────
            if (b.ItemUse != null)
            {
                Sec("🎯  Item Use");
                GUILayout.Label($"In use : {b.ItemUse.IsUsingItem}", _sLbl);
                if (b.ItemUse.IsUsingItem && b.ItemUse.CurrentItem != null)
                    GUILayout.Label($"Item   : {b.ItemUse.CurrentItem.DefinitionID}", _sLbl);

                GUILayout.BeginHorizontal();
                Btn("Cancel Use",    _bRed,    () => b.CancelItemUse());
                Btn("Execute Throw", _bOrange, () => b.ExecuteThrow());
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Bridge Resolver

        /// <summary>
        /// Lấy bridge từ SpectateManager.Instance.GetLocalPlayer().GameplayBridge
        /// Dùng reflection để tránh hard compile dependency vào NightHunt namespace.
        /// </summary>
        private IGameplayBridge GetBridge()
        {
            try
            {
                // SpectateManager.Instance
                var smType = Type.GetType("NightHunt.Gameplay.Spectator.SpectateManager, Assembly-CSharp");
                if (smType == null) return null;

                var inst = smType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetValue(null);
                if (inst == null) return null;

                // .GetLocalPlayer()  ← method name from NetworkPlayer source: SetLocalPlayer / GetLocalPlayer
                var netPlayer = smType.GetMethod("GetLocalPlayer")?.Invoke(inst, null);
                if (netPlayer == null) return null;

                // NetworkPlayer.GameplayBridge  (property you add)
                return netPlayer.GetType()
                    .GetProperty("GameplayBridge")
                    ?.GetValue(netPlayer) as IGameplayBridge;
            }
            catch { return null; }
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region GUI Helpers

        private void DrawTab(int idx, string label)
        {
            bool on = _tab == idx;
            if (GUILayout.Button(label, on ? _sTabOn : _sTabOff,
                GUILayout.Height(26), GUILayout.ExpandWidth(true)))
                _tab = idx;
        }

        private void Sec(string title)
        {
            GUILayout.Space(4);
            GUILayout.Label(title, _sSec);
        }

        private void Space() => GUILayout.Space(10);

        private void HLine()
        {
            Rect r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            if (_lineTex != null)
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), _lineTex);
            GUILayout.Space(2);
        }

        private void Btn(string label, GUIStyle style, Action action)
        {
            if (GUILayout.Button(label, style, GUILayout.Height(23), GUILayout.ExpandWidth(true)))
                action?.Invoke();
        }

        /// <summary>Label + TextField + Button on one line.</summary>
        private void TFRow(string label, ref string field, string btnLabel, Action onPress, GUIStyle btnStyle, float btnW)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _sTiny, GUILayout.Width(82));
            field = GUILayout.TextField(field, _sTF, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(btnLabel, btnStyle, GUILayout.Width(btnW), GUILayout.Height(22)))
                onPress?.Invoke();
            GUILayout.EndHorizontal();
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply(); return t;
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        #region Style Builder

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _sWin = new GUIStyle { padding = new RectOffset(6, 6, 4, 6) };
            _sWin.normal.background = Tex(new Color(0.08f, 0.08f, 0.10f, 0.97f));

            _sHdr = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 13,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 4, 4) };
            _sHdr.normal.background = Tex(new Color(0.06f, 0.06f, 0.12f));
            _sHdr.normal.textColor  = new Color(0.85f, 0.85f, 1f);

            _sTabOn = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4,4,3,3) };
            _sTabOn.normal.background = Tex(new Color(0.20f, 0.32f, 0.54f));
            _sTabOn.normal.textColor  = Color.white;

            _sTabOff = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4,4,3,3) };
            _sTabOff.normal.background = Tex(new Color(0.14f, 0.14f, 0.18f));
            _sTabOff.hover.background  = Tex(new Color(0.20f, 0.20f, 0.26f));
            _sTabOff.normal.textColor  = new Color(0.65f, 0.65f, 0.65f);

            _sSec = new GUIStyle { fontStyle = FontStyle.Bold, fontSize = 12 };
            _sSec.normal.textColor = new Color(0.95f, 0.78f, 0.25f);

            _sLbl = new GUIStyle { fontSize = 11 };
            _sLbl.normal.textColor = new Color(0.88f, 0.88f, 0.88f);

            _sTiny = new GUIStyle { fontSize = 10, richText = true };
            _sTiny.normal.textColor = new Color(0.58f, 0.58f, 0.58f);

            _sTF = new GUIStyle { fontSize = 11, padding = new RectOffset(4,4,3,3) };
            _sTF.normal.background = Tex(new Color(0.17f, 0.17f, 0.21f));
            _sTF.normal.textColor  = Color.white;

            _lineTex = Tex(new Color(0.35f, 0.35f, 0.38f));

            _bGray   = MBtn(new Color(0.26f,0.26f,0.28f), new Color(0.34f,0.34f,0.36f));
            _bGreen  = MBtn(new Color(0.12f,0.36f,0.16f), new Color(0.16f,0.46f,0.20f));
            _bRed    = MBtn(new Color(0.40f,0.10f,0.10f), new Color(0.54f,0.14f,0.14f));
            _bBlue   = MBtn(new Color(0.10f,0.22f,0.44f), new Color(0.13f,0.30f,0.56f));
            _bOrange = MBtn(new Color(0.42f,0.24f,0.04f), new Color(0.56f,0.32f,0.06f));
        }

        private GUIStyle MBtn(Color n, Color h)
        {
            var s = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            s.normal.background = Tex(n);
            s.hover.background  = Tex(h);
            s.active.background = Tex(n * 0.6f);
            s.normal.textColor  = Color.white;
            s.padding = new RectOffset(3,3,2,2);
            return s;
        }

        #endregion
    }
}