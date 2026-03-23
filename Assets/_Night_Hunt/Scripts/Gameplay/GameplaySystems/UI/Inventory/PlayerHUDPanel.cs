using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// In-game HUD panel for player stats.
    ///
    /// Two modes:
    ///   Scene rows  — assign <see cref="_sceneStatRows"/> with pre-placed StatRowEntry components.
    ///                 The panel uses exactly those rows (no spawning). StatType on each entry
    ///                 determines which stat it tracks. Colors/format still read from _statUIConfig.
    ///   Spawn mode  — leave _sceneStatRows empty. Rows are spawned from _statRowPrefab for every
    ///                 stat with ShowInUI=true in _statUIConfig (legacy behaviour).
    ///
    /// The inventory stat panel (PlayerStatUIPanel) always shows all ShowInUI stats;
    /// the in-game HUD is controlled independently via _sceneStatRows or ShowInUI.
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        [Header("Config")] [Tooltip("Drives which stats are shown and their display format / color.")] [SerializeField]
        private PlayerStatUIConfig _statUIConfig;

        [Header("Dynamic Row Spawning")]
        [Tooltip("Prefab instantiated once per visible stat. Must have a StatRowEntry component.")]
        [SerializeField] private StatRowEntry _statRowPrefab;

        [Tooltip("Parent transform (VerticalLayoutGroup) where stat rows are spawned.")]
        [SerializeField] private Transform _rowContainer;

        [Header("Scene Stat Rows (optional)")]
        [Tooltip("Pre-placed StatRowEntry components already in the scene.\n" +
                 "If populated these are used directly instead of spawning from the prefab. " +
                 "Each row must have its StatType set.")]
        [SerializeField] private StatRowEntry[] _sceneStatRows;

        // ── Runtime ───────────────────────────────────────────────────────────
        private readonly Dictionary<PlayerStatType, StatRowEntry> _rows =
            new Dictionary<PlayerStatType, StatRowEntry>();

        private UIDomainBridge _domainBridge;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this panel to a <see cref="UIDomainBridge"/>.
        /// Safe to call again (e.g. spectator switch, respawn) — re-builds rows and re-subscribes.
        /// </summary>
        public void Initialize(UIDomainBridge bridge)
        {
            SubscribeBridgeEvents(false); // unsub old
            _domainBridge = bridge;
            BuildRows();
            SubscribeBridgeEvents(true);
            PushInitialSnapshot();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            SubscribeBridgeEvents(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Row Builder — runs once per Initialize call
        // ─────────────────────────────────────────────────────────────────────

        private void BuildRows()
        {
            _rows.Clear();

            // Use pre-placed scene rows when provided — no spawning needed.
            if (_sceneStatRows != null && _sceneStatRows.Length > 0)
            {
                foreach (var row in _sceneStatRows)
                {
                    if (row == null) continue;
                    ApplyRowStyle(row);
                    _rows[row.StatType] = row;
                }
                return;
            }

            // Fallback: spawn from prefab using ShowInUI flag from config.
            if (_rowContainer != null)
                foreach (Transform child in _rowContainer)
                    Destroy(child.gameObject);

            if (_statRowPrefab == null || _rowContainer == null || _statUIConfig == null)
            {
                Debug.LogWarning("[PlayerHUDPanel] Missing _statRowPrefab, _rowContainer, or _statUIConfig — no rows built.");
                return;
            }

            foreach (var uiDef in _statUIConfig.Stats)
            {
                if (!uiDef.ShowInUI) continue;

                var row       = Instantiate(_statRowPrefab, _rowContainer);
                row.name      = $"Row_{uiDef.Type}";
                row.StatType  = uiDef.Type;

                ApplyRowStyle(row);
                _rows[uiDef.Type] = row;
                row.gameObject.SetActive(true);
            }
        }

        private void ApplyRowStyle(StatRowEntry row)
        {
            if (_statUIConfig == null || !_statUIConfig.HasUIDefinition(row.StatType)) return;
            var uiDef = _statUIConfig.GetUIDefinition(row.StatType);

            if (row.Slider?.fillRect != null)
            {
                var fill = row.Slider.fillRect.GetComponent<UnityEngine.UI.Image>();
                if (fill != null) fill.color = uiDef.DisplayColor;
            }
            if (row.AccentImage != null)
                row.AccentImage.color = uiDef.DisplayColor;
            if (row.ValueText != null)
                row.ValueText.color = uiDef.TextColor.a < 0.01f ? uiDef.DisplayColor : uiDef.TextColor;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Initial snapshot — fill rows once after bind
        // ─────────────────────────────────────────────────────────────────────

        private void PushInitialSnapshot()
        {
            if (_domainBridge == null || !_domainBridge.IsReady) return;
            foreach (var type in _rows.Keys)
            {
                float val = _domainBridge.Bridge.GetStat(type);
                RefreshStatUI(type, val);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Wiring
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null) return;

            if (subscribe)
            {
                _domainBridge.OnStatChanged += HandleStatChanged;
                _domainBridge.OnWeightChanged += HandleWeightChanged;
            }
            else
            {
                _domainBridge.OnStatChanged -= HandleStatChanged;
                _domainBridge.OnWeightChanged -= HandleWeightChanged;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (_domainBridge == null || !_domainBridge.IsReady) return;

            // When a max-stat changes (e.g. MaxHealth) → also refresh the dependent current-stat row
            RefreshRelatedCurrentStats(type);
            RefreshStatUI(type, newValue);
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            // Weight is routed through the generic path via CurrentWeight stat
            if (_rows.TryGetValue(PlayerStatType.CurrentWeight, out var row))
            {
                if (row.Slider != null)
                {
                    row.Slider.value = capacity > 0f ? current / capacity : 0f;
                    row.Slider.maxValue = 1.5f; // allow over-weight visual
                }

                if (row.ValueText != null)
                    row.ValueText.text = $"{current:F1}/{capacity:F1} kg";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshRelatedCurrentStats(PlayerStatType changedType)
        {
            if (_statUIConfig == null) return;
            foreach (var uiDef in _statUIConfig.Stats)
            {
                if (uiDef.RelatedMaxStatType == changedType && uiDef.Type != changedType)
                {
                    float val = _domainBridge.Bridge.GetStat(uiDef.Type);
                    RefreshStatUI(uiDef.Type, val);
                }
            }
        }

        private void RefreshStatUI(PlayerStatType type, float newValue)
        {
            if (!_rows.TryGetValue(type, out var row)) return;
            if (_statUIConfig == null) return;

            var uiDef = _statUIConfig.GetUIDefinition(type);

            switch (uiDef.DisplayType)
            {
                case StatDisplayType.SliderWithMax:
                {
                    float max = GetRelatedMaxValue(uiDef);
                    if (row.Slider != null && max > 0f)
                        row.Slider.value = newValue / max;
                    // If max == 0, max-stat hasn't synced yet — skip slider update to avoid showing 0%.
                    // RefreshRelatedCurrentStats will re-drive this row once the max-stat arrives.
                    if (row.ValueText != null)
                    {
                        row.ValueText.text = uiDef.ShowMaxValue && max > 0f
                            ? $"{newValue.ToString(uiDef.DisplayFormat)}/{max.ToString(uiDef.DisplayFormat)}"
                            : newValue.ToString(uiDef.DisplayFormat);
                    }

                    break;
                }

                case StatDisplayType.SliderWithRange:
                    if (row.ValueText != null)
                        row.ValueText.text = newValue.ToString(uiDef.DisplayFormat);
                    break;

                case StatDisplayType.Text:
                    if (row.ValueText != null)
                        row.ValueText.text = newValue.ToString(uiDef.DisplayFormat);
                    break;

                case StatDisplayType.ProgressBar:
                {
                    float max = GetRelatedMaxValue(uiDef);
                    if (row.Slider != null)
                    {
                        row.Slider.value = max > 0f ? newValue / max : 0f;
                        row.Slider.maxValue = 1.5f;
                    }

                    if (row.ValueText != null)
                    {
                        row.ValueText.text = uiDef.ShowMaxValue && max > 0f
                            ? $"{newValue:F1}/{max:F1}"
                            : $"{newValue.ToString(uiDef.DisplayFormat)}";
                    }

                    break;
                }
            }
        }

        private float GetRelatedMaxValue(PlayerStatUIDefinition uiDef)
        {
            if (uiDef.RelatedMaxStatType == uiDef.Type) return 0f;
            if (_domainBridge == null || !_domainBridge.IsReady) return 0f;
            return _domainBridge.Bridge.GetStat(uiDef.RelatedMaxStatType);
        }
    }
}