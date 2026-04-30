using System;
using System.Collections;
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
using NightHunt.Networking.Player;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.UI.Inventory;

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

        [SerializeField] private ItemTooltip _itemTooltip; // assign in inspector or left null to find at runtime

        [Header("Panel")]
        [SerializeField] private GameObject  _containerPanel;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _containerNameText;

        [Header("Item list")]
        [SerializeField] private Transform  _slotsParent;
        [Tooltip("Prefab containing ItemSlotView and ItemSlotInput (like Inventory slot)")]
        [SerializeField] private GameObject _itemSlotPrefab;

        [Header("Buttons")]
        [SerializeField] private Button _takeAllButton;
        [SerializeField] private Button _closeButton;

        [Header("Interaction")]
        [Tooltip("Max distance (world units) between player and container before the loot panel auto-closes.")]
        [SerializeField] private float _maxLootDistance = 4f;


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
        private readonly List<WorldItem> _openWorldItems = new List<WorldItem>();

        /// <summary>
        /// True when the panel was opened by the player explicitly holding E (interaction RPC).
        /// In that case hover-exit should NOT close the panel â€” the player needs to click the buttons.
        /// False when opened purely by hover-preview (already-open container).
        /// </summary>
        private bool _openedViaInteraction;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();

        // Track collapse state — close button toggles this, NOT the whole panel.
        private Coroutine _takeAllCoroutine;

        // Debounce hover-exit to prevent flicker when RaycastDetector alternates between
        // hitting and missing a container collider on its edge (happens ~every frame).
        private Coroutine _hoverExitDebounce;


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
            string prefabInfo     = _itemSlotPrefab    != null ? "ok" : "NULL";
            string slotsInfo      = _slotsParent      != null ? _slotsParent.name : "NULL";
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] Awake: panel={panelInfo} canvasGroup={cgInfo} takeAllBtn={takeAllInfo} closeBtn={closeBtnInfo} slotPrefab={prefabInfo} slotsParent={slotsInfo}");
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
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] HandleOwnerReady: _localNob={(_localNob != null ? _localNob.ObjectId.ToString() : "NULL")} player={player.name}");
        }

        public void Hide()
        {
            Debug.Log($"[LOOT_TAB_FLOW] LootContainerUI.Hide: panel hidden. openContainer={(_openContainer != null ? _openContainer.ObjectId.ToString() : "null")} openCorpse={(_openCorpse != null ? _openCorpse.ObjectId.ToString() : "null")} worldItems={_openWorldItems.Count} rows={_spawnedRows.Count} storage={FormatStorage(_currentStorage)}");
            if (_hoverExitDebounce != null) { StopCoroutine(_hoverExitDebounce); _hoverExitDebounce = null; }
            StopTakeAllCoroutine();
            UnsubscribeOpenLootable();
            if (_containerPanel != null) _containerPanel.SetActive(false);
            // Disable CanvasGroup so clicks don't pass through a hidden panel.
            if (_canvasGroup != null) { _canvasGroup.interactable = false; _canvasGroup.blocksRaycasts = false; }
            _takeItemAction      = null;
            _currentStorage      = null;
            _openWorldItems.Clear();
            _openedViaInteraction = false;
            ClearRows();
        }

        public void ShowWorldItems(IReadOnlyList<WorldItem> worldItems)
        {
            if (MatchesOpenWorldItems(worldItems))
                return;

            if (_localNob == null)
            {
                Debug.LogWarning("[LOOT_TAB_FLOW] LootContainerUI.ShowWorldItems skipped: local NetworkObject is null.");
                return;
            }

            UnsubscribeOpenLootable();
            _openWorldItems.Clear();

            var storage = new List<ItemInstanceData>();
            if (worldItems != null)
            {
                for (int i = 0; i < worldItems.Count; i++)
                {
                    var worldItem = worldItems[i];
                    if (worldItem == null || worldItem.IsPickedUp || worldItem.IsPickupPending)
                        continue;

                    if (!worldItem.CanInteract(_localNob.gameObject))
                        continue;

                    _openWorldItems.Add(worldItem);
                    storage.Add(worldItem.ItemData);
                }
            }

            if (_openWorldItems.Count == 0)
            {
                Debug.Log("[LOOT_TAB_FLOW] ShowWorldItems found no pickable world items.");
                return;
            }

            _takeItemAction = (idx, qty) =>
            {
                if (idx < 0 || idx >= _openWorldItems.Count)
                {
                    Debug.LogWarning($"[LOOT_TAB_FLOW] Ground loot take index out of range: {idx}/{_openWorldItems.Count}.");
                    return;
                }

                var item = _openWorldItems[idx];
                if (item == null || item.IsPickedUp)
                {
                    Debug.LogWarning($"[LOOT_TAB_FLOW] Ground loot take skipped: item at index {idx} is gone.");
                    return;
                }

                Debug.Log($"[LOOT_TAB_FLOW] RequestPickup ground item idx={idx} def={item.ItemDefinitionID} qty={item.Quantity}.");
                item.RequestPickupFromUI(_localNob);
                _openWorldItems.RemoveAt(idx);
                RebuildRows();
            };

            _openedViaInteraction = true;
            Debug.Log($"[LOOT_TAB_FLOW] Showing {_openWorldItems.Count} ground item(s) in LootContainerUI.");
            ShowLoot("Nearby Items", storage);
        }

        public bool IsShowingWorldItems => _openWorldItems.Count > 0 && _openContainer == null && _openCorpse == null;

        public bool IsShowingOpenedLootable =>
            (_openContainer != null && _openContainer.IsOpen) ||
            (_openCorpse != null && _openCorpse.IsOpen);

        public bool ShowOpenedLootableFromInventory(ILootable lootable, GameObject requester, string reason)
        {
            if (lootable == null)
            {
                Debug.Log($"[LOOT_TAB_FLOW] ShowOpenedLootableFromInventory skipped: lootable=null reason={reason}");
                return false;
            }

            if (_localNob == null)
            {
                Debug.LogWarning($"[LOOT_TAB_FLOW] ShowOpenedLootableFromInventory skipped: local NetworkObject is null reason={reason}");
                return false;
            }

            if (!lootable.IsOpen)
            {
                Debug.Log($"[LOOT_TAB_FLOW] ShowOpenedLootableFromInventory skipped: lootable is not open type={lootable.GetType().Name} reason={reason}");
                return false;
            }

            if (lootable is IInteractable interactable && !interactable.CanInteract(requester ?? _localNob.gameObject))
            {
                Debug.Log($"[LOOT_TAB_FLOW] ShowOpenedLootableFromInventory skipped: CanInteract=false label='{interactable.InteractLabel}' reason={reason}");
                return false;
            }

            if (lootable is WorldContainer container)
            {
                UnsubscribeOpenLootable();
                _openContainer = container;
                _openContainer.OnClientStorageChanged += RebuildRows;
                _takeItemAction = (idx, qty) => container.RequestTakeItem(_localNob, idx, qty);
                _openedViaInteraction = true;
                Debug.Log($"[LOOT_TAB_FLOW] Show opened container reason={reason} items={container.GetStorage()?.Count ?? 0} storage={FormatStorage(container.GetStorage())}");
                ShowLoot("Container", container.GetStorage());
                return true;
            }

            if (lootable is WorldCorpse corpse)
            {
                UnsubscribeOpenLootable();
                _openCorpse = corpse;
                _openCorpse.OnClientStorageChanged += RebuildRows;
                _takeItemAction = (idx, qty) => corpse.RequestTakeItem(_localNob, idx, qty);
                _openedViaInteraction = true;
                Debug.Log($"[LOOT_TAB_FLOW] Show opened corpse reason={reason} items={corpse.GetStorage()?.Count ?? 0} storage={FormatStorage(corpse.GetStorage())}");
                ShowLoot("Corpse", corpse.GetStorage());
                return true;
            }

            Debug.LogWarning($"[LOOT_TAB_FLOW] ShowOpenedLootableFromInventory skipped: unsupported lootable type={lootable.GetType().Name} reason={reason}");
            return false;
        }

        private bool MatchesOpenWorldItems(IReadOnlyList<WorldItem> worldItems)
        {
            if (!IsShowingWorldItems || worldItems == null)
                return false;

            int validCount = 0;
            for (int i = 0; i < worldItems.Count; i++)
            {
                var item = worldItems[i];
                if (item == null || item.IsPickedUp || item.IsPickupPending)
                    continue;
                if (_localNob != null && !item.CanInteract(_localNob.gameObject))
                    continue;
                validCount++;
            }

            if (validCount != _openWorldItems.Count)
                return false;

            int matched = 0;
            for (int i = 0; i < worldItems.Count; i++)
            {
                var item = worldItems[i];
                if (item == null || item.IsPickedUp || item.IsPickupPending)
                    continue;
                if (_localNob != null && !item.CanInteract(_localNob.gameObject))
                    continue;
                if (!_openWorldItems.Contains(item))
                    return false;
                matched++;
            }

            return matched == _openWorldItems.Count;
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
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log("[LootContainerUI] Update: player walked too far from container â€” closing panel.");
                Hide();
            }
            else if (_openCorpse != null &&
                     Vector3.Distance(playerPos, _openCorpse.transform.position) > _maxLootDistance)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
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
            _openWorldItems.Clear();
        }

        // â”€â”€ Handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleContainerOpened(WorldContainer container, FishNet.Connection.NetworkConnection conn)
        {
            if (container == null) return;
            if (_localNob == null)
            {
                Debug.LogWarning($"[LOOT_FLOW] UI [05][ContainerOpen.Skip] localNob=null container={container.ObjectId} conn={conn?.ClientId}");
                return;
            }
            // Only open for the local player who triggered the open.
            if (conn != null && _localNob.Owner != null && conn.ClientId != _localNob.Owner.ClientId)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[LOOT_FLOW] UI [05][ContainerOpen.SkipRemote] conn={conn.ClientId} localOwner={_localNob.Owner.ClientId}");
                return;
            }

            var storageList = container.GetStorage();
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LOOT_FLOW] UI [06][ContainerOpen.Show] localNob={_localNob.ObjectId} conn={conn?.ClientId} storage={storageList.Count}");

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
            if (_localNob == null)
            {
                Debug.LogWarning($"[LOOT_FLOW] UI [05][CorpseOpen.Skip] localNob=null corpse={corpse.ObjectId} conn={conn?.ClientId}");
                return;
            }
            // Only open for the local player who triggered the open.
            if (conn != null && _localNob.Owner != null && conn.ClientId != _localNob.Owner.ClientId)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[LOOT_FLOW] UI [05][CorpseOpen.SkipRemote] conn={conn.ClientId} localOwner={_localNob.Owner.ClientId}");
                return;
            }

            var storageList = corpse.GetStorage();
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LOOT_FLOW] UI [06][CorpseOpen.Show] localNob={_localNob.ObjectId} conn={conn?.ClientId} storage={storageList.Count}");

            UnsubscribeOpenLootable();
            _openCorpse = corpse;
            _openCorpse.OnClientStorageChanged += RebuildRows;

            _takeItemAction = (idx, qty) => corpse.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = true;
            ShowLoot("Corpse", storageList);
        }

        private void HandleContainerHoverEnter(WorldContainer container)
        {
            // Cancel any pending debounced hide — hover re-entered before delay expired.
            if (_hoverExitDebounce != null) { StopCoroutine(_hoverExitDebounce); _hoverExitDebounce = null; }

            if (container == null || _localNob == null || !container.IsOpen)
                return;

            if (!container.CanInteract(_localNob.gameObject))
                return;

            // Already showing this exact container via hover — skip to avoid duplicate ShowLoot spam.
            if (_openContainer == container && !_openedViaInteraction
                && _containerPanel != null && _containerPanel.activeSelf)
                return;

            UnsubscribeOpenLootable();
            _openContainer = container;
            _openContainer.OnClientStorageChanged += RebuildRows;

            _takeItemAction = (idx, qty) => container.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = false;
            ShowLoot("Container", container.GetStorage());
        }

        private void HandleContainerHoverExit(WorldContainer container)
        {
            if (!_openedViaInteraction && _openContainer == container)
            {
                // Use 2-frame debounce: if HoverEnter fires again within 2 frames
                // (RaycastDetector collider-edge flicker), cancel the hide.
                if (_hoverExitDebounce != null) StopCoroutine(_hoverExitDebounce);
                _hoverExitDebounce = StartCoroutine(DebounceHide());
            }
        }

        private System.Collections.IEnumerator DebounceHide()
        {
            yield return null; // wait frame 1
            yield return null; // wait frame 2
            _hoverExitDebounce = null;
            Hide();
        }

        private void HandleCorpseHoverEnter(WorldCorpse corpse)
        {
            if (corpse == null || _localNob == null || !corpse.IsOpen)
                return;

            if (!corpse.CanInteract(_localNob.gameObject))
                return;

            UnsubscribeOpenLootable();
            _openCorpse = corpse;
            _openCorpse.OnClientStorageChanged += RebuildRows;

            _takeItemAction = (idx, qty) => corpse.RequestTakeItem(_localNob, idx, qty);
            _openedViaInteraction = false;
            ShowLoot("Corpse", corpse.GetStorage());
        }

        private void HandleCorpseHoverExit(WorldCorpse corpse)
        {
            if (!_openedViaInteraction && _openCorpse == corpse)
                Hide();
        }

        private void ShowLoot(string title, IReadOnlyList<ItemInstanceData> storage)
        {
            _currentStorage = storage;

            if (_containerPanel != null) _containerPanel.SetActive(true);
            // Enable CanvasGroup so buttons are clickable.
            if (_canvasGroup != null) { _canvasGroup.interactable = true; _canvasGroup.blocksRaycasts = true; }

            if (_containerNameText != null) _containerNameText.text = title;

            string showPanelInfo = _containerPanel != null ? "ok" : "NULL";
            string showCgInfo    = _canvasGroup    != null ? "ok" : "NULL";
            Debug.Log($"[LOOT_TAB_FLOW] ShowLoot title='{title}' items={storage?.Count ?? 0} storage={FormatStorage(storage)} " +
                      $"componentActive={isActiveAndEnabled} componentGO.activeSelf={gameObject.activeSelf} componentGO.activeInHierarchy={gameObject.activeInHierarchy} " +
                      $"panel={showPanelInfo} panel.activeSelf={(_containerPanel != null ? _containerPanel.activeSelf.ToString() : "null")} " +
                      $"panel.activeInHierarchy={(_containerPanel != null ? _containerPanel.activeInHierarchy.ToString() : "null")} " +
                      $"canvasGroup={showCgInfo} parentChain='{DescribeParentChain(_containerPanel != null ? _containerPanel.transform : transform)}'");
            BuildRows(storage);
        }

        private static string DescribeParentChain(Transform start)
        {
            if (start == null)
                return "null";

            var parts = new List<string>();
            Transform t = start;
            while (t != null && parts.Count < 8)
            {
                parts.Add($"{t.name}[self={t.gameObject.activeSelf},hier={t.gameObject.activeInHierarchy}]");
                t = t.parent;
            }
            return string.Join(" <- ", parts);
        }

        /// <summary>Called when the open lootable's SyncList changes â€” refreshes the item rows.</summary>
        private static string FormatStorage(IReadOnlyList<ItemInstanceData> storage)
        {
            if (storage == null) return "null";
            if (storage.Count == 0) return "[]";

            var parts = new List<string>(storage.Count);
            for (int i = 0; i < storage.Count; i++)
            {
                var item = storage[i];
                parts.Add($"{i}:{item.DefinitionID}x{item.Quantity}#{ShortId(item.InstanceID)}");
            }
            return "[" + string.Join(", ", parts) + "]";
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "null";
            return id.Length <= 8 ? id : id.Substring(0, 8);
        }

        private void RebuildRows()
        {
            IReadOnlyList<ItemInstanceData> src = null;
            if (_openContainer != null) src = _openContainer.GetStorage();
            else if (_openCorpse != null) src = _openCorpse.GetStorage();
            else if (_openWorldItems.Count > 0)
            {
                var groundStorage = new List<ItemInstanceData>();
                for (int i = 0; i < _openWorldItems.Count; i++)
                {
                    var item = _openWorldItems[i];
                    if (item != null && !item.IsPickedUp && !item.IsPickupPending)
                        groundStorage.Add(item.ItemData);
                }
                src = groundStorage;
            }
            if (src == null) return;
            _currentStorage = src;
            BuildRows(src);
        }

        private void BuildRows(IReadOnlyList<ItemInstanceData> storage)
        {
            ClearRows();

            if (storage == null) { Debug.LogWarning("[LootContainerUI] BuildRows: storage NULL"); return; }

            string prefabInfo = _itemSlotPrefab != null ? "assigned" : "NULL";
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] BuildRows: {storage.Count} item(s) storage={FormatStorage(storage)} slotsParent={(_slotsParent != null ? _slotsParent.name : "NULL")} slotPrefab={prefabInfo}");

            for (int i = 0; i < storage.Count; i++)
            {
                var item   = storage[i];
                var rowGo  = SpawnSlot(item, i);
                if (rowGo != null) _spawnedRows.Add(rowGo);
            }
        }

        private GameObject SpawnSlot(ItemInstanceData itemData, int storageIndex)
        {
            if (_itemSlotPrefab == null || _slotsParent == null)
            {
                Debug.LogError("[LootContainerUI] SpawnSlot failed: _itemSlotPrefab or _slotsParent is NULL. Ensure Inspector is setup correctly!");
                return null;
            }

            var slotGo = Instantiate(_itemSlotPrefab, _slotsParent);
            slotGo.SetActive(true);

            var slotView = slotGo.GetComponent<ItemSlotView>();
            var slotInput = slotGo.GetComponent<ItemSlotInput>();

            if (slotView == null)
            {
                Debug.LogError("[LootContainerUI] SpawnSlot: Prefab is missing ItemSlotView component!");
                return slotGo;
            }

            // Determine container ID for UISlotId
            string containerId = _openContainer != null ? _openContainer.NetworkObject.ObjectId.ToString() :
                                (_openCorpse != null ? _openCorpse.NetworkObject.ObjectId.ToString() : "nearby_world_items");

            var slotId = UISlotId.Loot(containerId, storageIndex);

            // Initialize slot UI component
            slotView.Initialize(slotId);
            DragDropController.Instance?.RegisterSlotView(slotView);

            // Populate the slot state from item data
            var def = ItemDatabase.GetDefinition(itemData.DefinitionID);
            if (def != null)
            {
                var state = new UISlotState
                {
                    Item = itemData.ToInstance(),
                    Icon = def.Icon,
                    Background = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance != null
                        ? NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance.GetRarityBackground(def.Rarity)
                        : null,
                    StackCount = itemData.Quantity,
                    IsValidDropTarget = false,
                    IsHighlight = false,
                    IsLocked = false
                };
                slotView.SetState(state);
            }
            else
            {
                slotView.SetEmptyState();
            }

            // Setup interactions
            if (slotInput != null)
            {
                // Unsubscribe first just in case
                slotInput.OnSlotDoubleClicked -= HandleSlotDoubleClicked;
                slotInput.OnSlotDoubleClicked += HandleSlotDoubleClicked;

                slotInput.OnSlotHoverEnter -= HandleSlotHoverEnter;
                slotInput.OnSlotHoverEnter += HandleSlotHoverEnter;

                slotInput.OnSlotHoverExit -= HandleSlotHoverExit;
                slotInput.OnSlotHoverExit += HandleSlotHoverExit;
            }

            return slotGo;
        }

        private void HandleSlotDoubleClicked(ItemSlotView view)
        {
            if (view.State == null || view.State.Item == null) return;

            int storageIndex = view.SlotId.Index;
            int quantity = view.State.StackCount;
            TakeItem(storageIndex, quantity);
        }

        private void HandleSlotHoverEnter(ItemSlotView view)
        {
            if (view.State != null && view.State.Item != null)
            {
                _itemTooltip?.Show(view.State.Item, view.RectTransform.position, view.transform as RectTransform, "Take");
            }
        }

        private void HandleSlotHoverExit(ItemSlotView view)
        {
            _itemTooltip?.Hide();
        }

        public void TakeItem(int storageIndex, int quantity)
        {
            string nobInfo = _localNob != null ? _localNob.ObjectId.ToString() : "NULL";
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] â–¶ TakeItem CALLED: idx={storageIndex} qty={quantity} localNob={nobInfo} action={_takeItemAction != null}");
            if (_localNob == null) { Debug.LogWarning("[LootContainerUI] TakeItem: localNob not set"); return; }
            if (_takeItemAction == null) { Debug.LogWarning("[LootContainerUI] TakeItem: _takeItemAction null"); return; }
            _takeItemAction.Invoke(storageIndex, quantity);
        }

        private void OnTakeAll()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] â–¶ OnTakeAll CALLED: action={_takeItemAction != null} storage={_currentStorage != null} nob={_localNob != null}");
            if (_takeItemAction == null || _currentStorage == null || _localNob == null)
            {
                Debug.LogWarning($"[LootContainerUI] OnTakeAll: SKIP â€” action={_takeItemAction != null} storage={_currentStorage != null} nob={_localNob != null}");
                return;
            }

            StopTakeAllCoroutine();
            _takeAllCoroutine = StartCoroutine(TakeAllRoutine());
        }

        private IEnumerator TakeAllRoutine()
        {
            int count = _currentStorage?.Count ?? 0;
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                Debug.Log($"[LootContainerUI] TakeAllRoutine: starting with {count} item(s)");

            const float maxWaitPerItem = 0.35f;
            while (_takeItemAction != null && _currentStorage != null && _currentStorage.Count > 0 && _localNob != null)
            {
                int beforeCount = _currentStorage.Count;
                int idx = beforeCount - 1;
                int qty = Mathf.Max(1, _currentStorage[idx].Quantity);

                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableInventoryDebugLogs)
                    Debug.Log($"[LootContainerUI] TakeAllRoutine: taking idx={idx} qty={qty} count={beforeCount}");

                _takeItemAction.Invoke(idx, qty);

                float elapsed = 0f;
                while (_currentStorage != null && _currentStorage.Count >= beforeCount && elapsed < maxWaitPerItem)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (_currentStorage != null && _currentStorage.Count >= beforeCount)
                {
                    Debug.LogWarning($"[LootContainerUI] TakeAllRoutine: storage did not shrink after idx={idx}; stopping to avoid request spam.");
                    break;
                }
            }

            bool shouldHide = _currentStorage == null || _currentStorage.Count == 0;
            _takeAllCoroutine = null;

            if (shouldHide)
                Hide();
        }

        private void StopTakeAllCoroutine()
        {
            if (_takeAllCoroutine == null) return;
            StopCoroutine(_takeAllCoroutine);
            _takeAllCoroutine = null;
        }

        private void ClearRows()
        {
            foreach (var row in _spawnedRows)
            {
                if (row != null)
                {
                    // Clean up from DragDropController
                    var view = row.GetComponent<ItemSlotView>();
                    if (view != null)
                        DragDropController.Instance?.UnregisterSlotView(view);

                    Destroy(row);
                }
            }
            _spawnedRows.Clear();

            // Need to hide tooltip in case we were hovering over a slot when it was rebuilt/cleared
            _itemTooltip?.Hide();
        }


    }
}
