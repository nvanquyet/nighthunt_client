using System;
using NightHunt.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// MapSelectView — carousel-style map selector for the Home screen.
    ///
    /// Behaviour:
    ///   • Prev / Next arrows cycle through available MapConfig entries.
    ///   • Displays: map icon (sprite), display name, optional description.
    ///   • Locked maps are skipped (or shown as disabled if showLocked = true).
    ///   • When the active game mode changes, the carousel filters to maps that
    ///     support that mode (resets to first valid map).
    ///   • Fires OnMapSelected(mapId) whenever the selection changes (HomeView listens).
    ///
    /// SETUP (Prefab / hierarchy):
    ///   MapSelectView (this script)
    ///   ├── MapIcon (Image)
    ///   ├── MapNameText (TMP)
    ///   ├── MapDescText (TMP — optional)
    ///   ├── LockedOverlay (GameObject — shown when current entry isLocked)
    ///   ├── Btn_Prev (Button)
    ///   └── Btn_Next (Button)
    /// </summary>
    public class MapSelectView : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Display")]
        [SerializeField] private Image           mapIcon;
        [SerializeField] private TextMeshProUGUI mapNameText;
        [SerializeField] private TextMeshProUGUI mapDescText;
        [SerializeField] private GameObject      lockedOverlay;

        [Header("Navigation")]
        [SerializeField] private Button btnPrev;
        [SerializeField] private Button btnNext;

        [Header("Settings")]
        [Tooltip("If true, locked maps are shown (greyed out) but cannot be selected.")]
        [SerializeField] private bool showLocked = false;

        [Header("Fallback")]
        [SerializeField] private Sprite fallbackIcon;

        // ── Events ─────────────────────────────────────────────────────────────
        /// <summary>Fired when the selected map changes. Arg = mapId string.</summary>
        public event Action<string> OnMapSelected;

        // ── Runtime ────────────────────────────────────────────────────────────
        private MapEntry[] _displayList = Array.Empty<MapEntry>();
        private int        _index       = 0;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (btnPrev != null) btnPrev.onClick.AddListener(OnPrevClicked);
            if (btnNext != null) btnNext.onClick.AddListener(OnNextClicked);
        }

        private void Start()
        {
            // Load all maps initially (no mode filter)
            SetModeFilter(null);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Filter carousel to maps that support the given mode key (e.g. "2v2").
        /// Pass null or empty to show all available maps.
        /// Called by HomeView when the game mode selector changes.
        /// </summary>
        public void SetModeFilter(string modeKey)
        {
            _displayList = string.IsNullOrEmpty(modeKey)
                ? BuildDisplayList(MapConfig.GetAvailable())
                : BuildDisplayList(MapConfig.GetByMode(modeKey));

            _index = 0;
            RefreshDisplay();
        }

        /// <summary>Currently selected map id. Empty string if no maps available.</summary>
        public string SelectedMapId => _displayList.Length > 0 ? _displayList[_index].mapId : "";

        /// <summary>Currently selected MapEntry. Default if empty list.</summary>
        public bool TryGetSelected(out MapEntry entry)
        {
            if (_displayList.Length == 0) { entry = default; return false; }
            entry = _displayList[_index];
            return true;
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void OnPrevClicked()
        {
            if (_displayList.Length == 0) return;
            _index = (_index - 1 + _displayList.Length) % _displayList.Length;
            RefreshDisplay();
            FireSelected();
        }

        private void OnNextClicked()
        {
            if (_displayList.Length == 0) return;
            _index = (_index + 1) % _displayList.Length;
            RefreshDisplay();
            FireSelected();
        }

        // ── Display ────────────────────────────────────────────────────────────

        private void RefreshDisplay()
        {
            bool hasEntries = _displayList.Length > 0;

            // Nav arrows — hide when only 1 or 0 entries
            if (btnPrev != null) btnPrev.gameObject.SetActive(hasEntries && _displayList.Length > 1);
            if (btnNext != null) btnNext.gameObject.SetActive(hasEntries && _displayList.Length > 1);

            if (!hasEntries)
            {
                if (mapNameText    != null) mapNameText.text = "No maps available";
                if (mapDescText    != null) mapDescText.text = "";
                if (mapIcon        != null) mapIcon.sprite   = fallbackIcon;
                if (lockedOverlay  != null) lockedOverlay.SetActive(false);
                return;
            }

            MapEntry entry = _displayList[_index];

            if (mapIcon     != null) mapIcon.sprite   = entry.icon != null ? entry.icon : fallbackIcon;
            if (mapNameText != null) mapNameText.text  = entry.displayName;
            if (mapDescText != null) mapDescText.text  = entry.description;
            if (lockedOverlay != null) lockedOverlay.SetActive(entry.isLocked);

            FireSelected();
        }

        private void FireSelected()
        {
            if (_displayList.Length > 0)
                OnMapSelected?.Invoke(_displayList[_index].mapId);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private MapEntry[] BuildDisplayList(MapEntry[] source)
        {
            if (showLocked) return source;

            // Filter out locked when showLocked = false
            int count = 0;
            foreach (var m in source) if (!m.isLocked) count++;
            var result = new MapEntry[count];
            int i = 0;
            foreach (var m in source) if (!m.isLocked) result[i++] = m;
            return result;
        }
    }
}
