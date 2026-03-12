using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Config-driven HUD panel for player stats.
    ///
    /// At runtime, Initialize() reads <see cref="PlayerStatUIConfig.Stats"/> and
    /// spawns one <see cref="StatRowEntry"/> prefab per stat that has ShowInUI = true.
    /// Adding or removing a stat requires only an SO change — no code, no scene edits.
    ///
    /// Inspector setup:
    ///   • _statUIConfig  – assign PlayerStatUIConfig SO
    ///   • _statRowPrefab – assign a prefab with a StatRowEntry component
    ///   • _rowContainer  – assign a Transform with VerticalLayoutGroup
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        [Header("Config")] [Tooltip("Drives which stats are shown and their display format / color.")] [SerializeField]
        private PlayerStatUIConfig _statUIConfig;

        [Header("Dynamic Row Spawning")]
        [Tooltip("Prefab instantiated once per visible stat. Must have a StatRowEntry component.")]
        [SerializeField]
        private StatRowEntry _statRowPrefab;

        [Tooltip("Parent transform (VerticalLayoutGroup) where stat rows are spawned.")] [SerializeField]
        private Transform _rowContainer;

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
            // Destroy previously spawned rows
            if (_rowContainer != null)
                foreach (Transform child in _rowContainer)
                    Destroy(child.gameObject);
            _rows.Clear();

            if (_statRowPrefab == null || _rowContainer == null || _statUIConfig == null)
            {
                Debug.LogWarning(
                    "[PlayerHUDPanel] Missing _statRowPrefab, _rowContainer, or _statUIConfig — no rows built.");
                return;
            }

            foreach (var uiDef in _statUIConfig.Stats)
            {
                if (!uiDef.ShowInUI) continue;

                var row = Instantiate(_statRowPrefab, _rowContainer);
                row.name = $"Row_{uiDef.Type}";
                row.StatType = uiDef.Type;

                // Apply accent color from config to slider fill and accent image
                if (row.Slider != null && row.Slider.fillRect != null)
                {
                    var fill = ComponentResolver.Find<UnityEngine.UI.Image>(row.Slider.fillRect)
                        .OnSelf()
                        .InChildren()
                        .OrLogWarning("[Auto] UnityEngine.UI.Image not found")
                        .Resolve();
                    if (fill != null) fill.color = uiDef.DisplayColor;
                }

                if (row.AccentImage != null)
                    row.AccentImage.color = uiDef.DisplayColor;
                if (row.ValueText != null)
                    row.ValueText.color = uiDef.TextColor.a < 0.01f ? uiDef.DisplayColor : uiDef.TextColor;

                _rows[uiDef.Type] = row;

                //Active the row gameobject after setup to avoid showing uninitialized values
                row.gameObject.SetActive(true);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Initial snapshot — fill rows once after bind
        // ─────────────────────────────────────────────────────────────────────

        private void PushInitialSnapshot()
        {
            if (_domainBridge == null || !_domainBridge.IsReady || _statUIConfig == null) return;

            foreach (var uiDef in _statUIConfig.Stats)
            {
                if (!uiDef.ShowInUI) continue;
                float val = _domainBridge.Bridge.GetStat(uiDef.Type);
                RefreshStatUI(uiDef.Type, val);
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
                if (uiDef.ShowInUI &&
                    uiDef.RelatedMaxStatType == changedType &&
                    uiDef.Type != changedType)
                {
                    float val = _domainBridge.Bridge.GetStat(uiDef.Type);
                    RefreshStatUI(uiDef.Type, val);
                }
            }
        }

        private void RefreshStatUI(PlayerStatType type, float newValue)
        {
            if (_statUIConfig == null || !_rows.TryGetValue(type, out var row)) return;

            var uiDef = _statUIConfig.GetUIDefinition(type);
            if (!uiDef.ShowInUI) return;

            switch (uiDef.DisplayType)
            {
                case StatDisplayType.SliderWithMax:
                {
                    float max = GetRelatedMaxValue(uiDef);
                    if (row.Slider != null)
                        row.Slider.value = max > 0f ? newValue / max : 0f;
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