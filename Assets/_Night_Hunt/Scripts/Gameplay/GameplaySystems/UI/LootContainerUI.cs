using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Networking;

namespace NightHunt.GameplaySystems.UI
{
    /// <summary>
    /// Loot panel that opens when the local player interacts with a
    /// <see cref="WorldContainer"/> or a <see cref="WorldCorpse"/>.
    ///
    /// Setup (Inspector):
    ///   - containerPanel      : root CanvasGroup / GameObject to show/hide
    ///   - containerNameText   : TMP label for the container title
    ///   - slotsParent         : VerticalLayoutGroup parent for item rows
    ///   - itemSlotPrefab      : (optional) custom row prefab matching ItemSlotRow layout
    ///   - takeAllButton       : "Take All" button
    ///   - closeButton         : "Close" / "X" button
    ///
    /// If <see cref="itemSlotPrefab"/> is null, rows are built from raw uGUI.
    /// </summary>
    public class LootContainerUI : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────

        public static LootContainerUI Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Panel")]
        [SerializeField] private GameObject  _containerPanel;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _containerNameText;

        [Header("Item list")]
        [SerializeField] private Transform  _slotsParent;
        [SerializeField] private GameObject _itemSlotPrefab; // optional

        [Header("Buttons")]
        [SerializeField] private Button _takeAllButton;
        [SerializeField] private Button _closeButton;

        // ── Runtime ───────────────────────────────────────────────────────────

        private NetworkObject _localNob;

        /// <summary>
        /// Wraps the concrete RequestTakeItem call for the currently open lootable.
        /// Signature: (storageIndex, quantity) → void.
        /// </summary>
        private Action<int, int> _takeItemAction;

        /// <summary>Live snapshot of the current storage (kept in sync via events).</summary>
        private IReadOnlyList<ItemInstanceData> _currentStorage;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_containerPanel != null) _containerPanel.SetActive(false);

            _takeAllButton?.onClick.AddListener(OnTakeAll);
            _closeButton?.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            WorldContainer.OnContainerOpened += HandleContainerOpened;
            WorldCorpse.OnCorpseOpened       += HandleCorpseOpened;
        }

        private void OnDisable()
        {
            WorldContainer.OnContainerOpened -= HandleContainerOpened;
            WorldCorpse.OnCorpseOpened       -= HandleCorpseOpened;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Call from GameHUD.Initialize() with the local NetworkPlayer's NetworkObject.</summary>
        public void SetLocalPlayer(NetworkPlayer player)
        {
            _localNob = player?.NetworkObject;
        }

        public void Hide()
        {
            if (_containerPanel != null) _containerPanel.SetActive(false);
            _takeItemAction  = null;
            _currentStorage  = null;
            ClearRows();
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void HandleContainerOpened(WorldContainer container, FishNet.Connection.NetworkConnection conn)
        {
            if (container == null) return;

            _takeItemAction = (idx, qty) => container.RequestTakeItem(_localNob, idx, qty);
            ShowLoot("Container", container.GetStorage());
        }

        private void HandleCorpseOpened(WorldCorpse corpse, FishNet.Connection.NetworkConnection conn)
        {
            if (corpse == null) return;

            _takeItemAction = (idx, qty) => corpse.RequestTakeItem(_localNob, idx, qty);
            ShowLoot("Corpse", corpse.GetStorage());
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void ShowLoot(string title, IReadOnlyList<ItemInstanceData> storage)
        {
            _currentStorage = storage;

            if (_containerPanel != null) _containerPanel.SetActive(true);

            if (_containerNameText != null) _containerNameText.text = title;

            BuildRows(storage);
        }

        private void BuildRows(IReadOnlyList<ItemInstanceData> storage)
        {
            ClearRows();

            if (storage == null) return;

            for (int i = 0; i < storage.Count; i++)
            {
                var item   = storage[i];
                var rowGo  = SpawnRow(item, i);
                if (rowGo != null) _spawnedRows.Add(rowGo);
            }
        }

        private GameObject SpawnRow(ItemInstanceData item, int storageIndex)
        {
            var def         = ItemDatabase.GetDefinition(item.DefinitionID);
            string itemName = def != null ? def.DisplayName : item.DefinitionID;

            GameObject row;
            TextMeshProUGUI nameText = null;
            TextMeshProUGUI qtyText  = null;
            Button takeBtn           = null;

            if (_itemSlotPrefab != null && _slotsParent != null)
            {
                row      = Instantiate(_itemSlotPrefab, _slotsParent);
                nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                qtyText  = row.transform.Find("QtyText")?.GetComponent<TextMeshProUGUI>();
                takeBtn  = row.transform.Find("TakeButton")?.GetComponent<Button>();
            }
            else
            {
                // Build a minimal row at runtime
                row = new GameObject($"Row_{storageIndex}", typeof(RectTransform),
                                      typeof(HorizontalLayoutGroup));
                if (_slotsParent != null)
                    row.transform.SetParent(_slotsParent, false);

                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                hlg.childControlHeight = false;
                hlg.childControlWidth  = false;
                hlg.spacing            = 6f;

                var rt = row.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400f, 36f);

                nameText = CreateLabel(row.transform,  "NameText",  new Vector2(200f, 36f));
                qtyText  = CreateLabel(row.transform,  "QtyText",   new Vector2(60f,  36f));
                takeBtn  = CreateButton(row.transform, "TakeButton",new Vector2(80f,  36f), "Take");
            }

            if (nameText != null) nameText.text = itemName;
            if (qtyText  != null) qtyText.text  = $"×{item.Quantity}";

            // Capture for closure
            int  idx = storageIndex;
            int  qty = item.Quantity;

            if (takeBtn != null)
                takeBtn.onClick.AddListener(() => TakeItem(idx, qty));

            return row;
        }

        private void TakeItem(int storageIndex, int quantity)
        {
            if (_localNob == null) { Debug.LogWarning("[LootContainerUI] LocalNob not set"); return; }
            _takeItemAction?.Invoke(storageIndex, quantity);
        }

        private void OnTakeAll()
        {
            if (_takeItemAction == null || _currentStorage == null || _localNob == null) return;

            // Take from last to first so earlier indices are not affected by removals
            for (int i = _currentStorage.Count - 1; i >= 0; i--)
            {
                int qty = _currentStorage[i].Quantity;
                _takeItemAction.Invoke(i, qty);
            }
        }

        private void ClearRows()
        {
            foreach (var row in _spawnedRows)
                if (row != null) Destroy(row);
            _spawnedRows.Clear();
        }

        // ── Minimal uGUI helpers (used when no prefab is assigned) ────────────

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 size)
        {
            var go  = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize   = 16f;
            tmp.alignment  = TextAlignmentOptions.MidlineLeft;
            tmp.color      = Color.white;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 size, string label)
        {
            var go  = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.25f, 1f);

            var textGo  = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRt  = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            return go.GetComponent<Button>();
        }
    }
}
