using UnityEngine;
using TMPro;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.GameplaySystems.Stat;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Hover tooltip showing item stats, player modifiers, and slot label.
    ///
    /// TOOLTIP MODES (set via UISlotLayoutConfig.TooltipMode):
    ///   FollowMouse — updates position every frame to track the cursor.
    ///   SnapToSlot  — positions itself at the slot on Show(), then stays.
    ///   Fixed       — always at UISlotLayoutConfig.TooltipFixedPosition.
    ///
    /// SHOW/HIDE RULES:
    ///   Show  : on SlotHoverEnter when item != null.
    ///   Hide  : on SlotHoverExit, on DragStart (if !ShowTooltipDuringDrag), on ContextMenu.Show.
    ///
    /// CONTENT:
    ///   • Item name + optional slot label.
    ///   • Item description.
    ///   • Item stats (from ItemInstance.ComputedStats via ItemStatComputer).
    ///   • Player stat modifiers (from WeaponDefinition / EquipmentDefinition).
    /// </summary>
    public class ItemTooltip : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Config")]
        [SerializeField] private ItemStatUIConfig _itemStatConfig;

        [Header("UI Elements")]
        [SerializeField] private GameObject        _tooltipRoot;
        [SerializeField] private TextMeshProUGUI   _slotLabelText;
        [SerializeField] private TextMeshProUGUI   _itemNameText;
        [SerializeField] private TextMeshProUGUI   _itemDescriptionText;
        [SerializeField] private RectTransform     _itemStatsContainer;
        [SerializeField] private RectTransform     _playerModifiersContainer;
        [SerializeField] private GameObject        _statRowPrefab;

        [Header("Sections")]
        [SerializeField] private GameObject _itemStatsSection;
        [SerializeField] private GameObject _playerModifiersSection;

        [Header("Positioning")]
        [SerializeField] private Canvas _canvas;

        // ── Runtime ───────────────────────────────────────────────────────────

        private ItemInstance             _currentItem;
        private UISlotLayoutConfig       _uiConfig;
        private UIDomainBridge           _bridge;
        private RectTransform            _currentSlotRect;
        private TooltipMode              _activeMode = TooltipMode.FollowMouse;
        private readonly List<GameObject> _statRows = new();

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);
        }

        private void Update()
        {
            if (_tooltipRoot == null || !_tooltipRoot.activeSelf || _currentItem == null) return;

            if (_activeMode == TooltipMode.FollowMouse)
                ApplyPosition(Input.mousePosition);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>Called by InventoryScreen once at startup.</summary>
        public void Initialize(UIDomainBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>
        /// Show the tooltip for <paramref name="item"/>.
        /// Position is determined by the <see cref="UISlotLayoutConfig.TooltipMode"/>.
        /// </summary>
        public void Show(
            ItemInstance       item,
            Vector3            mouseScreenPos,
            RectTransform      slotRect  = null,
            string             slotLabel = null)
        {
            // Pick mode from config if available.
            _uiConfig        = UISlotLayoutConfig.Instance;
            _activeMode      = _uiConfig?.TooltipMode ?? TooltipMode.FollowMouse;
            _currentSlotRect = slotRect;

            if (item == null) { Hide(); return; }
            _currentItem = item;

            if (_slotLabelText != null)
            {
                bool hasLabel = !string.IsNullOrEmpty(slotLabel);
                _slotLabelText.gameObject.SetActive(hasLabel);
                if (hasLabel) _slotLabelText.text = slotLabel;
            }

            BuildTooltip(item);

            // Initial position before enabling so layout is computed.
            switch (_activeMode)
            {
                case TooltipMode.FollowMouse:
                    ApplyPosition(mouseScreenPos);
                    break;
                case TooltipMode.SnapToSlot:
                    if (slotRect != null) ApplyPositionFromRect(slotRect);
                    else ApplyPosition(mouseScreenPos);
                    break;
                case TooltipMode.Fixed:
                    ApplyFixed();
                    break;
            }

            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(true);
        }

        public void Hide()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);
            ClearStats();
            _currentItem     = null;
            _currentSlotRect = null;
        }

        /// <summary>
        /// Called by DragDropController at drag-start if ShowTooltipDuringDrag = false.
        /// </summary>
        public void HideIfNotDragVisible()
        {
            bool showDuring = _uiConfig?.ShowTooltipDuringDrag ?? false;
            if (!showDuring) Hide();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Positioning

        private void ApplyPosition(Vector3 screenPos)
        {
            var rt = GetTooltipRect();
            if (rt == null) return;

            Canvas canvas = ResolveCanvas();
            if (canvas == null)
            {
                _tooltipRoot.transform.position = screenPos;
                return;
            }

            Camera cam = CanvasCamera(canvas);
            Vector2 offset = _uiConfig?.TooltipOffset ?? new Vector2(16f, -16f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPos, cam, out Vector2 local);

            rt.anchoredPosition = ClampToCanvas(local + offset, rt, canvas);
        }

        private void ApplyPositionFromRect(RectTransform slotRect)
        {
            Canvas canvas = ResolveCanvas();
            if (canvas == null) return;
            Camera cam = CanvasCamera(canvas);

            var corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(cam, corners[2]); // top-right

            ApplyPosition(screenPt);
        }

        private void ApplyFixed()
        {
            var rt = GetTooltipRect();
            if (rt == null) return;
            rt.anchoredPosition = _uiConfig?.TooltipFixedPosition ?? Vector2.zero;
        }

        private RectTransform GetTooltipRect()
            => _tooltipRoot != null ? _tooltipRoot.GetComponent<RectTransform>() : null;

        private Canvas ResolveCanvas()
        {
            if (_canvas != null) return _canvas;
            return ComponentResolver.Find<Canvas>(_tooltipRoot)
                .InParent().InRootChildren()
                .OrLogWarning("[ItemTooltip] Canvas not found.")
                .Resolve();
        }

        private static Camera CanvasCamera(Canvas c)
        {
            if (c.renderMode == RenderMode.ScreenSpaceCamera ||
                c.renderMode == RenderMode.WorldSpace)
                return c.worldCamera ?? Camera.main;
            return null;
        }

        private static Vector2 ClampToCanvas(Vector2 pos, RectTransform tooltipRect, Canvas canvas)
        {
            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return pos;

            float hw = canvasRect.rect.width  * 0.5f;
            float hh = canvasRect.rect.height * 0.5f;
            float tw = tooltipRect.rect.width;
            float th = tooltipRect.rect.height;

            pos.x = Mathf.Clamp(pos.x, -hw,      hw - tw);
            pos.y = Mathf.Clamp(pos.y, -hh + th, hh);
            return pos;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Content Builder

        private void BuildTooltip(ItemInstance item)
        {
            ClearStats();

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null) return;

            if (_itemNameText != null)        _itemNameText.text        = def.DisplayName;
            if (_itemDescriptionText != null) _itemDescriptionText.text =
                string.IsNullOrEmpty(def.Description) ? "No description." : def.Description;

            bool hasItemStats  = BuildItemStats(item);
            bool hasModifiers  = BuildPlayerModifiers(def);

            if (_itemStatsSection       != null) _itemStatsSection.SetActive(hasItemStats);
            if (_playerModifiersSection != null) _playerModifiersSection.SetActive(hasModifiers);
        }

        private bool BuildItemStats(ItemInstance item)
        {
            if (_itemStatsContainer == null || _statRowPrefab == null) return false;

            if (!item.HasValidComputedStats)
                ItemStatComputer.Compute(item);

            var allStats = item.GetComputedStatsSnapshot();
            if (allStats == null || allStats.Count == 0) return false;

            bool hasStats = false;
            foreach (var kvp in allStats)
            {
                if (kvp.Value == 0) continue;
                if (_itemStatConfig == null) continue;

                var statDef = _itemStatConfig.GetItemStatDefinition(kvp.Key);
                if (string.IsNullOrEmpty(statDef.DisplayName)) continue;

                var go = Instantiate(_statRowPrefab, _itemStatsContainer);
                var row = ComponentResolver.Find<TooltipStatRow>(go)
                    .OnSelf().InChildren()
                    .OrLogWarning("[ItemTooltip] TooltipStatRow not found.")
                    .Resolve();

                if (row != null)
                {
                    row.SetItemStat(statDef, kvp.Value);
                    _statRows.Add(go);
                    hasStats = true;
                }
            }
            return hasStats;
        }

        private bool BuildPlayerModifiers(ItemDefinition def)
        {
            if (_playerModifiersContainer == null || _statRowPrefab == null) return false;

            PlayerStatModifier[] modifiers = null;
            if      (def is WeaponDefinition    wd) modifiers = wd.GetPlayerModifiers();
            else if (def is EquipmentDefinition ed) modifiers = ed.GetPlayerModifiers();

            if (modifiers == null || modifiers.Length == 0) return false;

            bool hasModifiers = false;
            foreach (var mod in modifiers)
            {
                if (mod.Value == 0) continue;

                var go = Instantiate(_statRowPrefab, _playerModifiersContainer);
                var row = ComponentResolver.Find<TooltipStatRow>(go)
                    .OnSelf().InChildren()
                    .OrLogWarning("[ItemTooltip] TooltipStatRow not found.")
                    .Resolve();

                if (row != null)
                {
                    row.SetPlayerModifier(mod);
                    _statRows.Add(go);
                    hasModifiers = true;
                }
            }
            return hasModifiers;
        }

        private void ClearStats()
        {
            foreach (var go in _statRows)
                if (go != null) Destroy(go);
            _statRows.Clear();
        }

        #endregion
    }
}