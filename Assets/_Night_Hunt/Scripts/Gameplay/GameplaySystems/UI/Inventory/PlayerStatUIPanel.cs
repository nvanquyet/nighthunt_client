using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Utilities;
using NightHunt.UI;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Manages player stat display rows and carry-weight penalty indicators.
    ///
    /// LAYOUT:
    ///   Weight Bar (always visible at top of panel):
    ///     [===========|..........] 8.5 / 10.0 kg   ⚠ OVERWEIGHT
    ///
    ///   Stat rows (spawned from PlayerStatUIConfig, ShowInUI = true):
    ///     Health   [████████░░]  85 / 100
    ///     Armor    [███░░░░░░░]  30 / 100
    ///     MoveSpeed [██████████]  5.0 m/s
    ///     …
    ///
    /// Weight is displayed as a normal stat row. Penalty label and warning icon are driven by WeightPenaltyConfig tiers.
    /// Subscribes to UIDomainBridge.OnOverweightChanged and OnWeightChanged.
    /// </summary>
    public class PlayerStatUIPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Stat Rows")]
        [Tooltip("Row prefab — must have a PlayerStatUIView component.")]
        [SerializeField] private GameObject _statRowPrefab;
        [Tooltip("Container for spawned rows (VerticalLayoutGroup recommended).")]
        [SerializeField] private RectTransform _statContainer;
        [SerializeField] private PlayerStatUIConfig _statUIConfig;

        [Header("Weight Penalty")]
        [Tooltip("Label showing the active penalty tier name (e.g. 'Overweight'). Hidden when none.")]
        [SerializeField] private TextMeshProUGUI _penaltyLabel;
        [SerializeField] private GameObject _overweightWarningIcon;

        // ── Runtime ───────────────────────────────────────────────────────────

        private UIPlayerContext _bridge;
        private WeightPenaltyConfig _weightPenaltyConfig;
        private bool _isInitialized;

        private readonly Dictionary<PlayerStatType, PlayerStatUIView> _statViews = new();

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void OnDestroy() => HookBridgeEvents(false);

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        public void Initialize(UIPlayerContext bridge)
        {
            _bridge = bridge;

            if (_statUIConfig == null)
            {
                Debug.LogError("[PlayerStatUIPanel] PlayerStatUIConfig is not assigned!");
                return;
            }

            _weightPenaltyConfig = InventoryConfig.Instance?.WeightPenalties;

            BuildStatUI();
            HookBridgeEvents(true);
            RefreshAllStats();

            _isInitialized = true;
        }

        public void RefreshForNewPlayer(UIPlayerContext bridge)
        {
            HookBridgeEvents(false);
            _bridge = bridge;

            if (!_isInitialized) { Initialize(bridge); return; }

            BuildStatUI();
            HookBridgeEvents(true);
            RefreshAllStats();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Build

        private void BuildStatUI()
        {
            if (_statRowPrefab == null || _statContainer == null || _statUIConfig == null) return;

            ClearStatViews();

            foreach (var def in _statUIConfig.Stats)
            {
                if (def.ShowInUI)
                    SpawnStatRow(def);
            }
        }

        private void SpawnStatRow(PlayerStatUIDefinition def)
        {
            var go = Instantiate(_statRowPrefab, _statContainer, false);
            var view = ComponentResolver.Find<PlayerStatUIView>(go)
                .OnSelf().InChildren()
                .OrLogWarning("[PlayerStatUIPanel] PlayerStatUIView not found on prefab.")
                .Resolve();

            if (view == null) { Destroy(go); return; }

            view.Initialize(def.Type, def, _bridge);
            _statViews[def.Type] = view;
            go.SetActive(true);
        }

        private void ClearStatViews()
        {
            foreach (var kvp in _statViews)
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            _statViews.Clear();
        }

        private void RefreshAllStats()
        {
            if (_bridge == null || !_bridge.IsReady || _bridge.Bridge == null) return;

            foreach (var kvp in _statViews)
            {
                float value = _bridge.Bridge.GetStat(kvp.Key);
                kvp.Value?.UpdateValue(value);
            }

            // Pull weight values from the bridge directly.
            float w = _bridge.Bridge.GetStat(PlayerStatType.CurrentWeight);
            float cap = _bridge.Bridge.GetStat(PlayerStatType.WeightCapacity);
            UpdateWeightPenalty(w, cap);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Bridge Events

        private void HookBridgeEvents(bool subscribe)
        {
            if (_bridge == null) return;

            if (subscribe)
            {
                _bridge.OnStatChanged       += HandleStatChanged;
                _bridge.OnWeightChanged     += HandleWeightChanged;
                _bridge.OnOverweightChanged += HandleOverweightChanged;
            }
            else
            {
                _bridge.OnStatChanged       -= HandleStatChanged;
                _bridge.OnWeightChanged     -= HandleWeightChanged;
                _bridge.OnOverweightChanged -= HandleOverweightChanged;
            }
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (_statViews.TryGetValue(type, out var view))
                view.UpdateValue(newValue);

            // If a RelatedMaxStatType changed, refresh dependent rows.
            if (_statUIConfig != null)
            {
                foreach (var def in _statUIConfig.Stats)
                {
                    if (!def.ShowInUI || def.RelatedMaxStatType != type || def.Type == type) continue;
                    if (_statViews.TryGetValue(def.Type, out var rel)) rel.UpdateValue();
                }
            }
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            UpdateWeightPenalty(current, capacity);
        }

        private void HandleOverweightChanged(bool isOverweight, float ratio)
        {
            UpdateWeightPenaltyRatio(ratio);

            // Penalty label is updated from the current weight ratio.
            // Icon visibility is updated based on the ratio.
            if (_overweightWarningIcon != null)
                _overweightWarningIcon.SetActive(isOverweight);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Weight Penalty

        private void UpdateWeightPenalty(float current, float capacity)
        {
            float ratio = capacity > 0f ? current / capacity : 0f;
            UpdateWeightPenaltyRatio(ratio);

            if (_overweightWarningIcon != null)
                _overweightWarningIcon.SetActive(ratio >= 1.0f);
        }

        private void UpdateWeightPenaltyRatio(float ratio)
        {
            if (_penaltyLabel != null)
            {
                var tier = _weightPenaltyConfig?.GetActiveTier(ratio);
                _penaltyLabel.gameObject.SetActive(tier != null);
                if (tier != null)
                    _penaltyLabel.text = tier.Label;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Editor

#if UNITY_EDITOR
        [ContextMenu("NightHunt/Auto-Assign Stat Row Prefab")]
        private void Editor_AutoAssignStatRowPrefab()
        {
            if (_statRowPrefab != null) return;
            string[] candidates =
            {
                "Assets/_Night_Hunt/Prefabs/UI/StatPrefabs.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/StatRow.prefab",
                "Assets/_Night_Hunt/Prefabs/UI/TooltipStatRow.prefab",
            };
            foreach (var p in candidates)
            {
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (found == null) continue;
                _statRowPrefab = found;
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[PlayerStatUIPanel] Auto-assigned _statRowPrefab from {p}");
                return;
            }
            Debug.LogWarning("[PlayerStatUIPanel] Stat row prefab not found.");
        }
#endif

        #endregion
    }
}
