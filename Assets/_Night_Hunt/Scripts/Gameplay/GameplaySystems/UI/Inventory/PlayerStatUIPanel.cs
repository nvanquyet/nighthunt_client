using System.Collections.Generic;
using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Dynamically spawns and manages PlayerStatUIView rows based on PlayerStatUIConfig.
    /// Only stats with ShowInUI = true are rendered.
    /// </summary>
    public class PlayerStatUIPanel : MonoBehaviour
    {
        [Header("Prefab")]
        [Tooltip("Row prefab – must have a PlayerStatUIView component")]
        [SerializeField] private GameObject _statRowPrefab;

        [Header("Layout")]
        [Tooltip("Container for spawned rows (VerticalLayoutGroup recommended)")]
        [SerializeField] private RectTransform _statContainer;

        [Header("Config")]
        [Tooltip("UI config for stat display. If null, assign manually before Initialize is called.")]
        [SerializeField] private PlayerStatUIConfig _statUIConfig;

        private UIDomainBridge _domainBridge;
        private Dictionary<PlayerStatType, PlayerStatUIView> _statViews = new Dictionary<PlayerStatType, PlayerStatUIView>();
        private bool _isInitialized = false;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(UIDomainBridge bridge)
        {
            _domainBridge = bridge;

            if (_statUIConfig == null)
            {
                Debug.LogError("[PlayerStatUIPanel] PlayerStatUIConfig is not assigned!");
                return;
            }

            Debug.Log($"[PlayerStatUIPanel] Initialize: bridge.IsReady={bridge?.IsReady}, bridge.Bridge={bridge?.Bridge}");

            BuildStatUI();
            SubscribeBridgeEvents(true);

            // Refresh all stats after subscribing to ensure we have current values
            RefreshAllStats();

            _isInitialized = true;

            Debug.Log($"[PlayerStatUIPanel] Initialize complete: {_statViews.Count} stat views created");
        }

        /// <summary>
        /// Call when switching to a different player (e.g. spectate mode).
        /// </summary>
        public void RefreshForNewPlayer(UIDomainBridge bridge)
        {
            // Unsubscribe from previous bridge (nếu có)
            if (_domainBridge != null)
            {
                _domainBridge.OnStatChanged -= HandleStatChanged;
                Debug.Log($"[PlayerStatUIPanel] RefreshForNewPlayer: Unsubscribed from old UIDomainBridge #{_domainBridge.GetHashCode()}");
            }

            // Gán bridge mới
            _domainBridge = bridge;

            // Nếu panel chưa từng Initialize trước đó → dùng lại flow Initialize
            if (!_isInitialized)
            {
                Debug.Log("[PlayerStatUIPanel] RefreshForNewPlayer: Panel not initialized yet, calling Initialize with new bridge.");
                Initialize(bridge);
                return;
            }

            // Rebuild UI cho player mới
            BuildStatUI();

            // Đăng ký lại event với bridge mới
            _domainBridge.OnStatChanged += HandleStatChanged;
            Debug.Log($"[PlayerStatUIPanel] RefreshForNewPlayer: Subscribed to OnStatChanged on UIDomainBridge #{_domainBridge.GetHashCode()}");

            // Refresh snapshot hiện tại vào UI
            RefreshAllStats();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            SubscribeBridgeEvents(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Build
        // ─────────────────────────────────────────────────────────────────────

        private void BuildStatUI()
        {
            if (_statRowPrefab == null || _statContainer == null || _statUIConfig == null)
            {
                Debug.LogError("[PlayerStatUIPanel] Missing prefab, container, or UI config!");
                return;
            }

            ClearStatViews();

            foreach (var uiDef in _statUIConfig.Stats)
            {
                if (uiDef.ShowInUI)
                {
                    SpawnStatRow(uiDef);
                }
            }
        }

        private void SpawnStatRow(PlayerStatUIDefinition uiDef)
        {
            var rowGO = Instantiate(_statRowPrefab, _statContainer, false);
            var statView = rowGO.GetComponent<PlayerStatUIView>();

            if (statView == null)
            {
                Debug.LogError("[PlayerStatUIPanel] Stat row prefab is missing PlayerStatUIView component!");
                Destroy(rowGO);
                return;
            }

            statView.Initialize(uiDef.Type, uiDef, _domainBridge);
            _statViews[uiDef.Type] = statView;
            rowGO.SetActive(true); // Activate after initialization to avoid showing uninitialized values
        }

        private void ClearStatViews()
        {
            foreach (var kvp in _statViews)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            _statViews.Clear();
        }

        /// <summary>
        /// Refresh all stat views with current values from bridge
        /// </summary>
        private void RefreshAllStats()
        {
            if (_domainBridge == null || !_domainBridge.IsReady || _domainBridge.Bridge == null)
            {
                Debug.LogWarning("[PlayerStatUIPanel] RefreshAllStats: bridge not ready");
                return;
            }

            Debug.Log($"[PlayerStatUIPanel] RefreshAllStats: refreshing {_statViews.Count} stat views");

            foreach (var kvp in _statViews)
            {
                var statType = kvp.Key;
                var view = kvp.Value;

                if (view != null)
                {
                    // Get current value from bridge and update view
                    float currentValue = _domainBridge.Bridge.GetStat(statType);
                    view.UpdateValue(currentValue);

                    if (statType == PlayerStatType.CurrentWeight || statType == PlayerStatType.WeightCapacity)
                    {
                        Debug.Log($"[PlayerStatUIPanel] RefreshAllStats: {statType} = {currentValue:F1}");
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Events
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null)
            {
                Debug.LogWarning("[PlayerStatUIPanel] SubscribeBridgeEvents: _domainBridge is null!");
                return;
            }

            if (subscribe)
            {
                _domainBridge.OnStatChanged += HandleStatChanged;
                Debug.Log($"[PlayerStatUIPanel] Subscribed to OnStatChanged on UIDomainBridge #{_domainBridge.GetHashCode()}");
            }
            else
            {
                _domainBridge.OnStatChanged -= HandleStatChanged;
                Debug.Log($"[PlayerStatUIPanel] Unsubscribed from OnStatChanged on UIDomainBridge #{_domainBridge.GetHashCode()}");
            }
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            // Debug log để xác nhận event được fire
            if (type == PlayerStatType.CurrentWeight || type == PlayerStatType.WeightCapacity)
            {
                Debug.Log($"[PlayerStatUIPanel] HandleStatChanged: {type} {oldValue:F1} -> {newValue:F1}");
            }

            // Update the row that directly owns this stat type
            if (_statViews.TryGetValue(type, out var view))
            {
                view.UpdateValue(newValue);
            }
            else
            {
                Debug.LogWarning($"[PlayerStatUIPanel] No view found for stat type: {type} (old={oldValue:F1}, new={newValue:F1})");
            }

            // If this type is used as a RelatedMaxStatType by another visible row,
            // that row also needs a refresh (its slider fill ratio changed).
            if (_statUIConfig != null)
            {
                foreach (var uiDef in _statUIConfig.Stats)
                {
                    if (!uiDef.ShowInUI) continue;
                    if (uiDef.RelatedMaxStatType != type) continue;
                    if (uiDef.Type == type) continue;   // Avoid re-triggering the same stat

                    if (_statViews.TryGetValue(uiDef.Type, out var relatedView))
                    {
                        relatedView.UpdateValue();
                    }
                }
            }
        }
    }
}
