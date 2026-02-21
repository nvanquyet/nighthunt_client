using UnityEngine;
using TMPro;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Data;
using NightHunt.StatSystem.Systems;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Tooltip hiển thị thông tin item khi hover.
    /// Hiển thị: Name, Description, Item Stats, Player Modifiers (nếu equipped)
    /// </summary>
    public class ItemTooltip : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ItemStatConfig _itemStatConfig;
        
        [Header("UI Elements")]
        [SerializeField] private GameObject _tooltipRoot;
        [SerializeField] private TextMeshProUGUI _itemNameText;
        [SerializeField] private TextMeshProUGUI _itemDescriptionText;
        [SerializeField] private RectTransform _itemStatsContainer;
        [SerializeField] private RectTransform _playerModifiersContainer;
        [SerializeField] private GameObject _statRowPrefab;
        
        [Header("Sections")]
        [SerializeField] private GameObject _itemStatsSection;
        [SerializeField] private GameObject _playerModifiersSection;
        
        [Header("Positioning")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private float _offsetX = 10f;
        [SerializeField] private float _offsetY = 10f;
        
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
        
        public void Show(ItemInstance item, Vector3 screenPosition)
        {
            if (item == null)
            {
                Hide();
                return;
            }
            
            _currentItem = item;
            BuildTooltip(item);
            UpdatePosition(screenPosition);
            
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
                targetCanvas = _tooltipRoot.GetComponentInParent<Canvas>();
            
            if (targetCanvas == null)
            {
                // Fallback: use screen position directly
                _tooltipRoot.transform.position = screenPosition + new Vector3(_offsetX, _offsetY, 0);
                return;
            }
            
            var rectTransform = _tooltipRoot.GetComponent<RectTransform>();
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
            
            // Get all item stats từ ItemStatSystem
            var allStats = ItemStatSystem.GetAllItemStats(item);
            
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
                var rowView = go.GetComponent<TooltipStatRow>();
                
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
            
            // Get PlayerModifiers từ definition
            PlayerStatModifier[] modifiers = null;
            
            if (def is WeaponDefinition weaponDef)
                modifiers = weaponDef.PlayerModifiers;
            else if (def is EquipmentDefinition equipmentDef)
                modifiers = equipmentDef.PlayerModifiers;
            
            if (modifiers == null || modifiers.Length == 0) return false;
            
            bool hasModifiers = false;
            foreach (var modifier in modifiers)
            {
                if (modifier.Value == 0) continue;
                
                var go = Instantiate(_statRowPrefab, _playerModifiersContainer);
                var rowView = go.GetComponent<TooltipStatRow>();
                
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
            // Follow mouse khi tooltip đang hiển thị
            if (_tooltipRoot != null && _tooltipRoot.activeSelf && _currentItem != null)
            {
                Vector3 mousePos = Input.mousePosition;
                UpdatePosition(mousePos);
            }
        }
    }
}
