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
    /// Tooltip hiển thị thông tin item khi hover.
    /// Hiển thị: Name, Description, Item Stats, Player Modifiers (nếu equipped)
    /// </summary>
    public class ItemTooltip : MonoBehaviour
    {
        [Header("Config")] [SerializeField] private ItemStatUIConfig _itemStatConfig;

        [Header("UI Elements")] [SerializeField]
        private GameObject _tooltipRoot;

        [Tooltip("Optional label showing slot position info (e.g. 'Primary Weapon Slot'). Hide via inspector if not needed.")]
        [SerializeField] private TextMeshProUGUI _slotLabelText;
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _itemDescriptionText;
        [SerializeField] private RectTransform _itemStatsContainer;
        [SerializeField] private RectTransform _playerModifiersContainer;
        [SerializeField] private GameObject _statRowPrefab;

        [Header("Sections")] [SerializeField] private GameObject _itemStatsSection;
        [SerializeField] private GameObject _playerModifiersSection;

        [Header("Positioning")] [SerializeField]
        private Canvas _canvas;

        [SerializeField] private float _offsetX = 10f;
        [SerializeField] private float _offsetY = 10f;

        [Tooltip(
            "Nếu check: tooltip sẽ follow mouse khi hover. Nếu không check: tooltip spawn tại chỗ và không di chuyển.")]
        [SerializeField]
        private bool _followMouse = true;

        private ItemInstance _currentItem;
        private readonly List<GameObject> _statRows = new List<GameObject>();
        private UIDomainBridge _domainBridge;

        private void Awake()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);
        }

        public void Initialize(UIDomainBridge bridge)
        {
            _domainBridge = bridge;
        }

        /// <param name="slotLabel">Optional slot position info (e.g. "Primary Weapon Slot"). Pass null to hide.</param>
        public void Show(ItemInstance item, Vector3 screenPosition, string slotLabel = null)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            _currentItem = item;
            if (_slotLabelText != null)
            {
                bool hasLabel = !string.IsNullOrEmpty(slotLabel);
                _slotLabelText.gameObject.SetActive(hasLabel);
                if (hasLabel) _slotLabelText.text = slotLabel;
            }

            BuildTooltip(item);
            if (_followMouse) UpdatePosition(screenPosition);

            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(true);
        }

        public void Hide()
        {
            if (_tooltipRoot != null)
                _tooltipRoot.SetActive(false);

            ClearStats();
            _currentItem = null;
        }

        public void UpdatePosition(Vector3 screenPosition)
        {
            if (_tooltipRoot == null) return;

            // Try to get canvas from tooltip root or use main canvas
            Canvas targetCanvas = _canvas;
            if (targetCanvas == null)
                targetCanvas = ComponentResolver.Find<Canvas>(_tooltipRoot)
                    .InParent()
                    .InRootChildren()
                    .OrLogWarning("[Auto] Canvas not found")
                    .Resolve();

            if (targetCanvas == null)
            {
                // Fallback: use screen position directly
                _tooltipRoot.transform.position = screenPosition + new Vector3(_offsetX, _offsetY, 0);
                return;
            }

            var rectTransform = ComponentResolver.Find<RectTransform>(_tooltipRoot)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] RectTransform not found")
                .Resolve();
            if (rectTransform == null) return;

            // Handle different canvas render modes
            Camera cam = null;
            if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
                targetCanvas.renderMode == RenderMode.WorldSpace)
            {
                cam = targetCanvas.worldCamera ?? Camera.main;
            }
            // Screen Space - Overlay: cam = null

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform,
                screenPosition,
                cam,
                out Vector2 localPoint);

            rectTransform.anchoredPosition = new Vector2(
                localPoint.x + _offsetX,
                localPoint.y + _offsetY
            );
        }

        private void BuildTooltip(ItemInstance item)
        {
            ClearStats();

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null) return;

            // 1. Set item name và description
            if (_itemNameText != null)
                _itemNameText.text = def.DisplayName;

            if (_itemDescriptionText != null)
            {
                _itemDescriptionText.text = string.IsNullOrEmpty(def.Description)
                    ? "No description"
                    : def.Description;
            }

            // 2. Build Item Stats section
            bool hasItemStats = BuildItemStats(item, def);
            if (_itemStatsSection != null)
                _itemStatsSection.SetActive(hasItemStats);

            // 3. Build Player Modifiers section (nếu item đang equipped)
            bool hasModifiers = BuildPlayerModifiers(def);
            if (_playerModifiersSection != null)
                _playerModifiersSection.SetActive(hasModifiers);
        }

        private bool BuildItemStats(ItemInstance item, ItemDefinition def)
        {
            if (_itemStatsContainer == null || _statRowPrefab == null) return false;

            // Use the instance's pre-computed stats (populated by ItemStatComputer via SAO).
            // Fall back to an immediate compute if the tooltip is shown before SAO has run
            // (e.g. on a freshly-created item before the first equip cycle).
            if (!item.HasValidComputedStats)
                ItemStatComputer.Compute(item);
            var allStats = item.GetComputedStatsSnapshot();

            if (allStats == null || allStats.Count == 0) return false;

            bool hasStats = false;
            foreach (var kvp in allStats)
            {
                var statType = kvp.Key;
                var value = kvp.Value;

                // Skip zero values
                if (value == 0) continue;

                if (_itemStatConfig == null) continue;

                var statDef = _itemStatConfig.GetItemStatDefinition(statType);

                // Check if stat definition is valid (has DisplayName)
                if (string.IsNullOrEmpty(statDef.DisplayName)) continue;

                var go = Instantiate(_statRowPrefab, _itemStatsContainer);
                var rowView = ComponentResolver.Find<TooltipStatRow>(go)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] TooltipStatRow not found")
                    .Resolve();

                if (rowView != null)
                {
                    rowView.SetItemStat(statDef, value);
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
            if (def is WeaponDefinition weaponDef)
                modifiers = weaponDef.GetPlayerModifiers();
            else if (def is EquipmentDefinition equipmentDef)
                modifiers = equipmentDef.GetPlayerModifiers();

            if (modifiers == null || modifiers.Length == 0) return false;

            bool hasModifiers = false;
            foreach (var modifier in modifiers)
            {
                if (modifier.Value == 0) continue;

                var go = Instantiate(_statRowPrefab, _playerModifiersContainer);
                var rowView = ComponentResolver.Find<TooltipStatRow>(go)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] TooltipStatRow not found")
                    .Resolve();

                if (rowView != null)
                {
                    rowView.SetPlayerModifier(modifier);
                    _statRows.Add(go);
                    hasModifiers = true;
                }
            }

            return hasModifiers;
        }

        private void ClearStats()
        {
            foreach (var go in _statRows)
            {
                if (go != null)
                    Destroy(go);
            }

            _statRows.Clear();
        }

        private void Update()
        {
            // Chỉ follow mouse nếu _followMouse = true
            if (_followMouse && _tooltipRoot != null && _tooltipRoot.activeSelf && _currentItem != null)
            {
                Vector3 mousePos = Input.mousePosition;
                UpdatePosition(mousePos);
            }
        }

#if UNITY_EDITOR
        // ── Editor — Context Menu: Auto-assign / Create Tooltip Stat Row Prefab

        [ContextMenu("NightHunt/Auto-Assign Tooltip Stat Row Prefab")]
        private void Editor_AutoAssignTooltipStatRowPrefab()
        {
            if (_statRowPrefab != null) { Debug.Log("[ItemTooltip] _statRowPrefab already assigned."); return; }

            string[] candidates =
            {
                "Assets/_Night_Hunt/Prefabs/UI/TooltipStatRow.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/TooltipStatRow 2.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/StatPrefabs.prefab",
            };
            foreach (var p in candidates)
            {
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (found != null)
                {
                    _statRowPrefab = found;
                    UnityEditor.EditorUtility.SetDirty(this);
                    Debug.Log($"[ItemTooltip] Auto-assigned _statRowPrefab from {p}");
                    return;
                }
            }
            Debug.LogWarning("[ItemTooltip] TooltipStatRow prefab not found — use 'Create Tooltip Stat Row Template Prefab'.");
        }

        [ContextMenu("NightHunt/Create Tooltip Stat Row Template Prefab")]
        private void Editor_CreateTooltipStatRowPrefab()
        {
            const string dir  = "Assets/_Night_Hunt/Prefabs/UI";
            const string path = dir + "/TooltipStatRow_Template.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[ItemTooltip] TooltipStatRow_Template already exists at {path}");
                return;
            }

            var go  = new GameObject("TooltipStatRow_Template");
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(240f, 22f);
            var hlg = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.spacing = 4f;

            // Stat name
            var nameGo = new GameObject("StatName", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            nameGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 120f;
            nameGo.GetComponent<TMPro.TextMeshProUGUI>().text = "Damage";

            // Stat value
            var valGo = new GameObject("StatVal", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            valGo.transform.SetParent(go.transform, false);
            valGo.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 60f;
            var valTmp = valGo.GetComponent<TMPro.TextMeshProUGUI>();
            valTmp.text = "30"; valTmp.alignment = TMPro.TextAlignmentOptions.Right;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (_statRowPrefab == null)
            {
                _statRowPrefab = saved;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[ItemTooltip] Created TooltipStatRow_Template at {path}. " +
                      "Add TooltipStatRow component and wire StatName/StatVal fields.");
        }
#endif
    }
}