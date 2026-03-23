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
using NightHunt.GameplaySystems.Core.Configs;

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
        // â”€â”€ Singleton â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public static LootContainerUI Instance { get; private set; }

        // â”€â”€ Inspector â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("Panel")]
        [SerializeField] private GameObject  _containerPanel;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _containerNameText;

        [Header("Item list")]
        [SerializeField] private Transform  _slotsParent;
        [SerializeField] private LootItemRow _itemRowPrefab; // optional â€” assign a prefab with LootItemRow component

        [Header("Buttons")]
        [SerializeField] private Button _takeAllButton;
        [SerializeField] private Button _closeButton;

        [Header("Interaction")]
        [Tooltip("Max distance (world units) between player and container before the loot panel auto-closes.")]
        [SerializeField] private float _maxLootDistance = 4f;
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        // â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private NetworkObject _localNob;

        /// <summary>
        /// Wraps the concrete RequestTakeItem call for the currently open lootable.
        /// Signature: (storageIndex, quantity) â†’ void.
        /// </summary>
        private Action<int, int> _takeItemAction;

        /// <summary>Live snapshot of the current storage (kept in sync via events).</summary>
        private IReadOnlyList<ItemInstanceData> _currentStorage;

        // Track which lootable is currently open so we can unsubscribe storage-change events.
        private WorldContainer _openContainer;
        private WorldCorpse    _openCorpse;

        /// <summary>
        /// True when the panel was opened by the player explicitly holding E (interaction RPC).
        /// In that case hover-exit should NOT close the panel â€” the player needs to click the buttons.
        /// False when opened purely by hover-preview (already-open container).
        /// </summary>
        private bool _openedViaInteraction;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        // â”€â”€ Unity lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_containerPanel != null) _containerPanel.SetActive(false);
            // Ensure CanvasGroup starts non-interactable so it matches the hidden state.
            if (_canvasGroup != null) { _canvasGroup.interactable = false; _canvasGroup.blocksRaycasts = false; }

            _takeAllButton?.onClick.AddListener(OnTakeAll);
            _closeButton?.onClick.AddListener(Hide);

            string panelInfo      = _containerPanel  != null ? "ok" : "NULL";
            string cgInfo         = _canvasGroup      != null ? "ok" : "NULL";
            string takeAllInfo    = _takeAllButton    != null ? "ok" : "NULL";
            string closeBtnInfo   = _closeButton      != null ? "ok" : "NULL";
            string prefabInfo     = _itemRowPrefab    != null ? "ok" : "NULL";
            string slotsInfo      = _slotsParent      != null ? _slotsParent.name : "NULL";
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] Awake: panel={panelInfo} canvasGroup={cgInfo} takeAllBtn={takeAllInfo} closeBtn={closeBtnInfo} rowPrefab={prefabInfo} slotsParent={slotsInfo}");
        }

        private void OnEnable()
        {
            NetworkPlayer.OnOwnerReady           += HandleOwnerReady;
            WorldContainer.OnContainerOpened     += HandleContainerOpened;
            WorldCorpse.OnCorpseOpened           += HandleCorpseOpened;
            WorldContainer.OnAnyHoverEnter       += HandleContainerHoverEnter;
            WorldContainer.OnAnyHoverExit        += HandleContainerHoverExit;
            WorldCorpse.OnAnyHoverEnter          += HandleCorpseHoverEnter;
            WorldCorpse.OnAnyHoverExit           += HandleCorpseHoverExit;
        }

        private void OnDisable()
        {
            NetworkPlayer.OnOwnerReady           -= HandleOwnerReady;
            WorldContainer.OnContainerOpened     -= HandleContainerOpened;
            WorldCorpse.OnCorpseOpened           -= HandleCorpseOpened;
            WorldContainer.OnAnyHoverEnter       -= HandleContainerHoverEnter;
            WorldContainer.OnAnyHoverExit        -= HandleContainerHoverExit;
            WorldCorpse.OnAnyHoverEnter          -= HandleCorpseHoverEnter;
            WorldCorpse.OnAnyHoverExit           -= HandleCorpseHoverExit;
        }

        // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Call from GameHUD.Initialize() with the local NetworkPlayer's NetworkObject.</summary>
        public void SetLocalPlayer(NetworkPlayer player)
        {
            // Kept for backward compatibility with GameHUD.Initialize().
            // Real assignment now happens via NetworkPlayer.OnOwnerReady in OnEnable.
            if (player != null) HandleOwnerReady(player);
        }

        private void HandleOwnerReady(NetworkPlayer player)
        {
            // OnOwnerReady fires only on the owning client, so this is always the local player.
            _localNob = player.NetworkObject;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] HandleOwnerReady: _localNob={(_localNob != null ? _localNob.ObjectId.ToString() : "NULL")} player={player.name}");
        }

        public void Hide()
        {
            UnsubscribeOpenLootable();
            if (_containerPanel != null) _containerPanel.SetActive(false);
            // Disable CanvasGroup so clicks don't pass through a hidden panel.
            if (_canvasGroup != null) { _canvasGroup.interactable = false; _canvasGroup.blocksRaycasts = false; }
            _takeItemAction      = null;
            _currentStorage      = null;
            _openedViaInteraction = false;
            ClearRows();
        }

        private void Update()
        {
            // When the panel was opened via Hold-E interaction, hover-exit is suppressed
            // (player needs to look at screen to click buttons). Instead we close the panel
            // when the player physically walks too far from the lootable.
            if (!_openedViaInteraction || _localNob == null) return;

            Vector3 playerPos = _localNob.transform.position;
            if (_openContainer != null &&
                Vector3.Distance(playerPos, _openContainer.transform.position) > _maxLootDistance)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log("[LootContainerUI] Update: player walked too far from container â€” closing panel.");
                Hide();
            }
            else if (_openCorpse != null &&
                     Vector3.Distance(playerPos, _openCorpse.transform.position) > _maxLootDistance)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log("[LootContainerUI] Update: player walked too far from corpse â€” closing panel.");
                Hide();
            }
        }

        private void UnsubscribeOpenLootable()
        {
            if (_openContainer != null)
            {
                _openContainer.OnClientStorageChanged -= RebuildRows;
                _openContainer = null;
            }
            if (_openCorpse != null)
            {
                _openCorpse.OnClientStorageChanged -= RebuildRows;
                _openCorpse = null;
            }
        }

        // â”€â”€ Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleContainerOpened(WorldContainer container, FishNet.Connection.NetworkConnection conn)
        {
            if (container == null) return;
            // Strict null guard: nếu _localNob chưa được set → UI chưa sẵn sàng, bỏ qua.
            // Nếu không có guard này, khi _localNob == null condition "_localNob != null && ... " = false
            // → không skip → LootUI bật trên TẤT CẢ clients.
            if (_localNob == null) return;
            // Only open for the local player who triggered the open.
            if (conn != null && conn != _localNob.Owner)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[LootContainerUI] HandleContainerOpened: SKIP â€” conn.ClientId={conn?.ClientId} != localOwner={_localNob.Owner?.ClientId}");
                return;
            }

            var storageList = container.GetStorage();
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] HandleContainerOpened: SHOW â€” localNob={(_localNob != null ? _localNob.ObjectId.ToString() : "NULL")} conn={conn?.ClientId} storage.Count={storageList.Count}");

            UnsubscribeOpenLootable();
            _openContainer = container;
            _openContainer.OnClientStorageChanged += RebuildRows;

            _takeItemAction = (idx, qty) => container.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = true;
            ShowLoot("Container", storageList);
        }

        private void HandleCorpseOpened(WorldCorpse corpse, FishNet.Connection.NetworkConnection conn)
        {
            if (corpse == null) return;
            // Strict null guard: chưa khởi tạo → bỏ qua.
            if (_localNob == null) return;
            // Only open for the local player who triggered the open.
            if (conn != null && conn != _localNob.Owner)
            {
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[LootContainerUI] HandleCorpseOpened: SKIP â€” conn.ClientId={conn?.ClientId} != localOwner={_localNob.Owner?.ClientId}");
                return;
            }

            var storageList = corpse.GetStorage();
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] HandleCorpseOpened: SHOW â€” localNob={(_localNob != null ? _localNob.ObjectId.ToString() : "NULL")} conn={conn?.ClientId} storage.Count={storageList.Count}");

            UnsubscribeOpenLootable();
            _openCorpse = corpse;
            _openCorpse.OnClientStorageChanged += RebuildRows;

            _takeItemAction = (idx, qty) => corpse.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = true;
            ShowLoot("Corpse", storageList);
        }

        // -- Hover handlers: close when looking away, reopen when looking back --

        private void HandleContainerHoverEnter(WorldContainer container)
        {
            if (container == null || container.IsLooted) return;
            // Already showing this container â€” nothing to do.
            if (_openContainer == container) return;

            var storageList = container.GetStorage();
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] HandleContainerHoverEnter: storage.Count={storageList.Count}");
            UnsubscribeOpenLootable();
            _openContainer = container;
            _openContainer.OnClientStorageChanged += RebuildRows;
            _takeItemAction = (idx, qty) => container.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = false; // hover-preview only
            ShowLoot("Container", storageList);
        }

        private void HandleContainerHoverExit(WorldContainer container)
        {
            // If the panel was opened by explicit interaction (Hold E), keep it open
            // so the player can click Take / Take All without losing the panel when
            // the crosshair drifts off the 3D container.
            if (_openedViaInteraction) return;
            if (_openContainer == container)
                Hide();
        }

        private void HandleCorpseHoverEnter(WorldCorpse corpse)
        {
            if (corpse == null || corpse.IsLooted) return;
            if (_openCorpse == corpse) return;

            UnsubscribeOpenLootable();
            _openCorpse = corpse;
            _openCorpse.OnClientStorageChanged += RebuildRows;
            _takeItemAction = (idx, qty) => corpse.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = false; // hover-preview only
            ShowLoot("Corpse", corpse.GetStorage());
        }

        private void HandleCorpseHoverExit(WorldCorpse corpse)
        {
            if (_openedViaInteraction) return;
            if (_openCorpse == corpse)
                Hide();
        }

        // â”€â”€ Internal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ShowLoot(string title, IReadOnlyList<ItemInstanceData> storage)
        {
            _currentStorage = storage;

            if (_containerPanel != null) _containerPanel.SetActive(true);
            // Enable CanvasGroup so buttons are clickable.
            if (_canvasGroup != null) { _canvasGroup.interactable = true; _canvasGroup.blocksRaycasts = true; }

            if (_containerNameText != null) _containerNameText.text = title;

            string showPanelInfo = _containerPanel != null ? "ok" : "NULL";
            string showCgInfo    = _canvasGroup    != null ? "ok" : "NULL";
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] ShowLoot: title='{title}' items={storage?.Count ?? 0} panel={showPanelInfo} canvasGroup={showCgInfo}");
            BuildRows(storage);
        }

        /// <summary>Called when the open lootable's SyncList changes â€” refreshes the item rows.</summary>
        private void RebuildRows()
        {
            IReadOnlyList<ItemInstanceData> src = null;
            if (_openContainer != null) src = _openContainer.GetStorage();
            else if (_openCorpse != null) src = _openCorpse.GetStorage();
            if (src == null) return;
            _currentStorage = src;
            BuildRows(src);
        }

        private void BuildRows(IReadOnlyList<ItemInstanceData> storage)
        {
            ClearRows();

            if (storage == null) { Debug.LogWarning("[LootContainerUI] BuildRows: storage NULL"); return; }

            string prefabInfo = _itemRowPrefab != null ? "assigned" : "NULL";
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] BuildRows: {storage.Count} item(s) â€” slotsParent={(_slotsParent != null ? _slotsParent.name : "NULL")} rowPrefab={prefabInfo}");

            for (int i = 0; i < storage.Count; i++)
            {
                var item   = storage[i];
                var rowGo  = SpawnRow(item, i);
                if (rowGo != null) _spawnedRows.Add(rowGo);
            }
        }

        private GameObject SpawnRow(ItemInstanceData item, int storageIndex)
        {
            var def        = ItemDatabase.GetDefinition(item.DefinitionID);
            string itemName = def != null ? def.DisplayName : item.DefinitionID;
            Sprite  icon    = def != null ? def.Icon       : null;

            GameObject row;
            Image           iconImg  = null;
            TextMeshProUGUI nameText = null;
            TextMeshProUGUI qtyText  = null;
            Button          takeBtn  = null;

            if (_itemRowPrefab != null && _slotsParent != null)
            {
                var rowComp = Instantiate(_itemRowPrefab, _slotsParent);
                row      = rowComp.gameObject;
                iconImg  = rowComp.Icon;
                nameText = rowComp.NameText;
                qtyText  = rowComp.QtyText;
                takeBtn  = rowComp.TakeButton;
                rowComp.gameObject.SetActive(true); // deactivate prefab instance until fully setup to avoid showing uninitialized values
            }
            else
            {
                // Fallback: build a minimal row at runtime
                row = new GameObject($"Row_{storageIndex}", typeof(RectTransform),
                                      typeof(HorizontalLayoutGroup));
                if (_slotsParent != null)
                    row.transform.SetParent(_slotsParent, false);

                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                hlg.childControlHeight = false;
                hlg.childControlWidth  = false;
                hlg.spacing            = 6f;

                var rt = row.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(440f, 40f);

                iconImg  = CreateIconSlot(row.transform, "Icon",       new Vector2(40f,  40f));
                nameText = CreateLabel(   row.transform, "NameText",   new Vector2(220f, 40f));
                qtyText  = CreateLabel(   row.transform, "QtyText",    new Vector2(60f,  40f));
                takeBtn  = CreateButton(  row.transform, "TakeButton", new Vector2(80f,  40f), "Take");
            }

            if (iconImg  != null)
            {
                iconImg.sprite  = icon;
                iconImg.enabled = icon != null;
            }
            if (nameText != null) nameText.text = itemName;
            if (qtyText  != null) qtyText.text  = item.Quantity.ToString();

            // Capture for closure
            int idx = storageIndex;
            int qty = item.Quantity;
            if (takeBtn != null)
            {
                takeBtn.onClick.AddListener(() => TakeItem(idx, qty));
                if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                    Debug.Log($"[LootContainerUI] SpawnRow[{storageIndex}]: takeBtn wired â€” item='{itemName}' qty={qty} interactable={takeBtn.interactable}");
            }
            else
            {
                string rowPrefabInfo = _itemRowPrefab != null ? "assigned" : "NULL";
                Debug.LogWarning($"[LootContainerUI] SpawnRow[{storageIndex}]: takeBtn is NULL â€” button onClick will NOT fire! prefab={rowPrefabInfo}");
            }

            return row;
        }

        private void TakeItem(int storageIndex, int quantity)
        {
            string nobInfo = _localNob != null ? _localNob.ObjectId.ToString() : "NULL";
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] â–¶ TakeItem CALLED: idx={storageIndex} qty={quantity} localNob={nobInfo} action={_takeItemAction != null}");
            if (_localNob == null) { Debug.LogWarning("[LootContainerUI] TakeItem: localNob not set"); return; }
            if (_takeItemAction == null) { Debug.LogWarning("[LootContainerUI] TakeItem: _takeItemAction null"); return; }
            _takeItemAction.Invoke(storageIndex, quantity);
        }

        private void OnTakeAll()
        {
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] â–¶ OnTakeAll CALLED: action={_takeItemAction != null} storage={_currentStorage != null} nob={_localNob != null}");
            if (_takeItemAction == null || _currentStorage == null || _localNob == null)
            {
                Debug.LogWarning($"[LootContainerUI] OnTakeAll: SKIP â€” action={_takeItemAction != null} storage={_currentStorage != null} nob={_localNob != null}");
                return;
            }

            // Snapshot indices + quantities BEFORE sending any RPCs.
            // In host mode the server may process each RPC synchronously, mutating storage
            // mid-iteration if we read _currentStorage directly in the loop.
            int count = _currentStorage.Count;
            if (_debugConfig != null && _debugConfig.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] OnTakeAll: taking {count} item(s)");
            var snapshot = new (int idx, int qty)[count];
            for (int i = 0; i < count; i++)
                snapshot[i] = (i, _currentStorage[i].Quantity);

            // Send from last to first so server-side removal doesn't shift earlier indices.
            for (int i = count - 1; i >= 0; i--)
                _takeItemAction.Invoke(snapshot[i].idx, snapshot[i].qty);
        }

        private void ClearRows()
        {
            foreach (var row in _spawnedRows)
                if (row != null) Destroy(row);
            _spawnedRows.Clear();
        }

        // â”€â”€ Minimal uGUI helpers (used when no prefab is assigned) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static Image CreateIconSlot(Transform parent, string name, Vector2 size)
        {
            var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.25f, 0.8f); // dark placeholder bg
            return img;
        }

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

