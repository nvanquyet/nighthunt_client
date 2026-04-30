using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.UI
{
    // ── Stat bar mapping ─────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="PlayerStatType"/> to a UI bar and label.</summary>
    [Serializable]
    public struct StatBarEntry
    {
        [Tooltip("The stat that drives this bar's current value.")]
        public PlayerStatType CurrentType;

        [Tooltip("The stat that drives this bar's max value (leave as none/-1 if fixed).")]
        public PlayerStatType MaxType;

        [Tooltip("Fixed max value used when MaxType is not meaningful (e.g. Armor).")]
        public float FixedMax;

        [Tooltip("Slider displaying fill ratio (value / max).")]
        public Slider Bar;

        [Tooltip("Optional text label showing numeric value.")]
        public TextMeshProUGUI ValueLabel;

        [Tooltip("Optional icon image — tinted when value is critically low.")]
        public Image Icon;

        [Tooltip("Ratio below which the icon/bar tints to the critical color.")]
        [Range(0f, 1f)] public float CriticalThreshold;

        public Color NormalColor;
        public Color CriticalColor;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// PlayerStatsHUD — compact HP / Armor / Stamina bars for the in-combat overlay.
    ///
    /// ROLE IN NEW ARCHITECTURE:
    ///   Works alongside <see cref="NightHunt.GameplaySystems.UI.Inventory.PlayerStatUIPanel"/>.
    ///
    ///   This panel is the COMPACT in-combat version:
    ///   • Visible during UIState.Combat and UIState.Inventory.
    ///   • Configurable via the <see cref="StatBarEntry"/> array — drag any stat to any bar.
    ///   • Weight warning icon reacts to <see cref="UIPlayerContext.OnOverweightChanged"/>.
    ///
    ///   The expanded stat detail panel lives in
    ///   <see cref="NightHunt.GameplaySystems.UI.Inventory.PlayerStatUIPanel"/>.
    ///
    /// SETUP:
    ///   Configure _statBars in Inspector:
    ///   • HP bar    : CurrentType = Health,  MaxType = MaxHealth
    ///   • Stamina   : CurrentType = Stamina, MaxType = MaxStamina
    ///   • Armor     : CurrentType = Armor,   FixedMax = 100 (or set MaxType)
    /// </summary>
    public sealed class PlayerStatsHUD : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Stat Bars")]
        [SerializeField] private StatBarEntry[] _statBars;

        [Header("Weight Warning")]
        [Tooltip("Shown when carry weight exceeds capacity.")]
        [SerializeField] private GameObject _overweightWarning;

        [Tooltip("Optional text label showing current/max weight.")]
        [SerializeField] private TextMeshProUGUI _weightLabel;

        // ── Runtime ───────────────────────────────────────────────────────────

        private UIPlayerContext _context;

        // Track current values so max changes also update the display correctly.
        private readonly Dictionary<PlayerStatType, float> _currentValues = new();

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_overweightWarning != null) _overweightWarning.SetActive(false);
        }

        private void OnDestroy()
        {
            UnsubscribeContext();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this panel to a UIPlayerContext.
        /// Immediately reads current stat values and refreshes all bars.
        /// Safe to call multiple times (re-bind on player/spectate switch).
        /// </summary>
        public void Bind(UIPlayerContext context)
        {
            UnsubscribeContext();
            _currentValues.Clear();

            _context = context;
            if (context == null || !context.IsReady) return;

            SubscribeContext(context);

            // Seed current values from a full snapshot push (UIPlayerContext.Bind already
            // calls PushSnapshot, but we may have subscribed after the first push).
            context.PushStatsSnapshot();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void SubscribeContext(UIPlayerContext context)
        {
            context.OnStatChanged      += HandleStatChanged;
            context.OnWeightChanged    += HandleWeightChanged;
            context.OnOverweightChanged += HandleOverweightChanged;
        }

        private void UnsubscribeContext()
        {
            if (_context == null) return;
            _context.OnStatChanged      -= HandleStatChanged;
            _context.OnWeightChanged    -= HandleWeightChanged;
            _context.OnOverweightChanged -= HandleOverweightChanged;
            _context = null;
        }

        // ── Event Handlers ────────────────────────────────────────────────────

        private void HandleStatChanged(PlayerStatType type, float oldVal, float newVal)
        {
            _currentValues[type] = newVal;
            RefreshBarsForType(type);
        }

        private void HandleWeightChanged(float current, float max)
        {
            if (_weightLabel != null)
                _weightLabel.text = $"{current:F1} / {max:F1}";
        }

        private void HandleOverweightChanged(bool isOverweight, float ratio)
        {
            if (_overweightWarning != null)
                _overweightWarning.SetActive(isOverweight);
        }

        // ── Display Logic ─────────────────────────────────────────────────────

        private void RefreshBarsForType(PlayerStatType changedType)
        {
            if (_statBars == null) return;

            for (int i = 0; i < _statBars.Length; i++)
            {
                ref StatBarEntry entry = ref _statBars[i];
                if (entry.CurrentType != changedType && entry.MaxType != changedType)
                    continue;

                float current = _currentValues.TryGetValue(entry.CurrentType, out float c) ? c : 0f;

                float max;
                if (entry.MaxType != entry.CurrentType &&
                    _currentValues.TryGetValue(entry.MaxType, out float m) && m > 0f)
                    max = m;
                else if (entry.FixedMax > 0f)
                    max = entry.FixedMax;
                else
                    max = Mathf.Max(current, 1f);

                float ratio = max > 0f ? current / max : 0f;

                if (entry.Bar != null)
                    entry.Bar.value = ratio;

                if (entry.ValueLabel != null)
                    entry.ValueLabel.text = Mathf.CeilToInt(current).ToString();

                // Critical tint
                if (entry.Icon != null && entry.CriticalThreshold > 0f)
                    entry.Icon.color = ratio <= entry.CriticalThreshold
                        ? entry.CriticalColor
                        : entry.NormalColor;
            }
        }
    }
}
