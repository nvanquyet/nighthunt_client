using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Stat;
using NightHunt.Utilities;
using NightHunt.UI;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Detailed item information panel. Tooltips stay short; this panel shows every
    /// computed item stat and every player modifier carried by the selected item.
    /// </summary>
    public class ItemStatUIPanel : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ItemStatUIConfig _itemStatConfig;

        [Header("UI")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private RectTransform _itemStatsContainer;
        [SerializeField] private RectTransform _playerModifiersContainer;
        [SerializeField] private GameObject _statRowPrefab;
        [SerializeField] private GameObject _itemStatsSection;
        [SerializeField] private GameObject _playerModifiersSection;

        private ItemInstance _currentItem;
        private readonly List<GameObject> _rows = new();

        public void Initialize(UIPlayerContext bridge)
        {
            EnsureRuntimeReferences();
            Clear();
        }

        public void RefreshForNewPlayer(UIPlayerContext bridge)
        {
            EnsureRuntimeReferences();

            if (_currentItem != null)
                Show(_currentItem);
            else
                Clear();
        }

        public void Show(ItemInstance item)
        {
            _currentItem = item;
            ClearRows();

            if (item == null)
            {
                SetVisible(false);
                return;
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                SetVisible(false);
                return;
            }

            bool hasItemStats = BuildItemStats(item, def);
            bool hasPlayerModifiers = BuildPlayerModifiers(def);

            if (_itemStatsSection != null)
                _itemStatsSection.SetActive(hasItemStats);

            if (_playerModifiersSection != null)
                _playerModifiersSection.SetActive(hasPlayerModifiers);

            SetVisible(hasItemStats || hasPlayerModifiers);
        }

        public void Clear()
        {
            _currentItem = null;
            ClearRows();
            SetVisible(false);
        }

        private bool BuildItemStats(ItemInstance item, ItemDefinition def)
        {
            if (_itemStatsContainer == null)
                return false;

            if (!item.HasValidComputedStats)
                ItemStatComputer.Compute(item);

            Dictionary<ItemStatType, float> finalStats = item.GetComputedStatsSnapshot();
            if (finalStats == null || finalStats.Count == 0)
                return false;

            var baseStats = ItemStatComputer.GetBaseStats(def);
            bool hasRows = false;

            foreach (var kvp in finalStats)
            {
                if (Mathf.Abs(kvp.Value) < 0.01f)
                    continue;

                var statDef = ResolveStatDefinition(kvp.Key);
                if (string.IsNullOrEmpty(statDef.DisplayName))
                    continue;

                var row = SpawnRow(_itemStatsContainer);
                if (row == null)
                    continue;

                baseStats.TryGetValue(kvp.Key, out float baseValue);
                row.SetItemStatComparison(statDef, baseValue, kvp.Value);
                hasRows = true;
            }

            return hasRows;
        }

        private bool BuildPlayerModifiers(ItemDefinition def)
        {
            if (_playerModifiersContainer == null)
                return false;

            PlayerStatModifier[] modifiers = null;
            if (def is WeaponDefinition weapon)
                modifiers = weapon.GetPlayerModifiers();
            else if (def is EquipmentDefinition equipment)
                modifiers = equipment.GetPlayerModifiers();

            if (modifiers == null || modifiers.Length == 0)
                return false;

            bool hasRows = false;
            foreach (var modifier in modifiers)
            {
                if (Mathf.Abs(modifier.Value) < 0.01f)
                    continue;

                var row = SpawnRow(_playerModifiersContainer);
                if (row == null)
                    continue;

                row.SetPlayerModifier(modifier);
                hasRows = true;
            }

            return hasRows;
        }

        private TooltipStatRow SpawnRow(RectTransform parent)
        {
            if (_statRowPrefab == null)
                return CreateRuntimeRow(parent);

            var go = Instantiate(_statRowPrefab, parent);
            _rows.Add(go);

            return ComponentResolver.Find<TooltipStatRow>(go)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[ItemStatUIPanel] TooltipStatRow not found.")
                .Resolve();
        }

        private TooltipStatRow CreateRuntimeRow(RectTransform parent)
        {
            var rowGO = new GameObject("RuntimeStatRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(TooltipStatRow));
            rowGO.transform.SetParent(parent, false);
            _rows.Add(rowGO);

            var rowRT = rowGO.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(1f, 1f);
            rowRT.pivot = new Vector2(0.5f, 1f);
            rowRT.sizeDelta = new Vector2(0f, 24f);

            var layout = rowGO.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = 8f;

            TextMeshProUGUI label = CreateText(rowGO.transform, "Label", TextAlignmentOptions.Left, FontStyles.Normal);
            TextMeshProUGUI value = CreateText(rowGO.transform, "Value", TextAlignmentOptions.Right, FontStyles.Normal);

            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.minWidth = 120f;

            var valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1f;
            valueLayout.minWidth = 140f;

            var row = rowGO.GetComponent<TooltipStatRow>();
            row.Bind(label, value);
            return row;
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row);
            }

            _rows.Clear();
        }

        private void SetVisible(bool visible)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(visible);
        }

        private void EnsureRuntimeReferences()
        {
            if (_panelRoot != null && _itemStatsContainer != null && _playerModifiersContainer != null)
                return;

            var root = new GameObject("Runtime_ItemStatPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(transform, false);
            _panelRoot = root;

            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(1f, 0.1f);
            rootRT.anchorMax = new Vector2(1f, 0.9f);
            rootRT.pivot = new Vector2(1f, 0.5f);
            rootRT.anchoredPosition = new Vector2(-24f, 0f);
            rootRT.sizeDelta = new Vector2(360f, 0f);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.06f, 0.92f);

            var rootLayout = root.GetComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 12, 12);
            rootLayout.spacing = 10f;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            _itemStatsSection = CreateSection(root.transform, "ItemStatsSection", "Item Stats", out _itemStatsContainer);
            _playerModifiersSection = CreateSection(root.transform, "PlayerModifiersSection", "Player Modifiers", out _playerModifiersContainer);
        }

        private GameObject CreateSection(Transform parent, string name, string title, out RectTransform container)
        {
            var section = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            section.transform.SetParent(parent, false);

            var layout = section.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI heading = CreateText(section.transform, title, TextAlignmentOptions.Left, FontStyles.Bold);
            heading.color = new Color(0.86f, 0.9f, 0.92f, 1f);

            var rows = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rows.transform.SetParent(section.transform, false);
            container = rows.GetComponent<RectTransform>();

            var rowsLayout = rows.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 2f;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            return section;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TextAlignmentOptions alignment, FontStyles fontStyle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = 14f;
            text.color = Color.white;
            text.alignment = alignment;
            text.fontStyle = fontStyle;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            if (name != "Label" && name != "Value")
                text.text = name;

            return text;
        }

        private ItemStatDefinition ResolveStatDefinition(ItemStatType type)
        {
            if (_itemStatConfig != null && _itemStatConfig.HasItemStat(type))
                return _itemStatConfig.GetItemStatDefinition(type);

            return new ItemStatDefinition
            {
                Type = type,
                DisplayName = FormatEnumName(type.ToString()),
                DisplayColor = Color.white,
                TextColor = new Color(0.9f, 0.92f, 0.95f, 1f),
                DisplayFormat = "0.##",
                IsPositiveStat = type != ItemStatType.SpreadBase
                              && type != ItemStatType.SpreadMax
                              && type != ItemStatType.SpreadPenalty
                              && type != ItemStatType.RecoilHorizontal
                              && type != ItemStatType.RecoilVertical
            };
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                    sb.Append(' ');
                sb.Append(c == '_' ? ' ' : c);
            }

            return sb.ToString();
        }
    }
}
