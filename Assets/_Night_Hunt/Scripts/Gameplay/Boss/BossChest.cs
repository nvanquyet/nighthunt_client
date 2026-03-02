using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using UnityEngine;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// Boss loot chest — spawned at boss death position.
    /// Players can interact to open it and see rolled loot.
    /// Implements <see cref="ILootable"/> so it works with the
    /// existing <see cref="NightHunt.GameplaySystems.UI.LootContainerUI"/>.
    /// </summary>
    public sealed class BossChest : NetworkBehaviour, ILootable
    {
        // ── Sync ──────────────────────────────────────────────────────────────
        private readonly SyncList<ItemInstanceData> _syncStorage = new SyncList<ItemInstanceData>();
        private readonly SyncVar<bool> _isOpen   = new SyncVar<bool>();
        private readonly SyncVar<bool> _isLooted = new SyncVar<bool>();

        // ── Config injected at spawn ──────────────────────────────────────────
        private IList<BossDropEntryData> _dropTable;

        // ── ILootable ─────────────────────────────────────────────────────────
        public bool IsLooted     => _isLooted.Value;
        public bool IsOpen       => _isOpen.Value;
        public string InteractLabel => IsLooted ? "[E] Empty" : (IsOpen ? "[E] Loot Chest" : "[E] Open Boss Chest");

        /// <summary>Raised server-side when any player opens this chest.</summary>
        public static event Action<BossChest, NetworkConnection> OnChestOpened;

        // ──────────────────────────────────────────────────────────────────────
        #region FishNet Lifecycle

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Roll loot immediately on spawn so storage is always ready
            if (_dropTable != null)
                RollLoot();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Initialisation

        /// <summary>Called by <see cref="BossController"/> after spawn.</summary>
        [Server]
        public void Initialize(IList<BossDropEntryData> dropTable)
        {
            _dropTable = dropTable;
            if (IsServerStarted)
                RollLoot();
        }

        [Server]
        private void RollLoot()
        {
            if (_dropTable == null) return;

            _syncStorage.Clear();
            var drops = ItemDropTable.Roll(_dropTable);

            foreach (var drop in drops)
            {
                _syncStorage.Add(new ItemInstanceData
                {
                    InstanceID   = System.Guid.NewGuid().ToString(),
                    DefinitionID = drop.ItemId,
                    Quantity     = drop.Quantity
                });
            }

            Debug.Log($"[BossChest] Rolled {_syncStorage.Count} drop(s).");
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region ILootable / IInteractable

        public IReadOnlyList<ItemInstanceData> GetStorage() => _syncStorage;

        public void OnHoverEnter(GameObject interactor)
        {
            // Outline / highlight effect wired up when highlight system is ready.
        }

        public void OnHoverExit(GameObject interactor)
        {
            // Outline / highlight effect wired up when highlight system is ready.
        }

        public bool CanInteract(GameObject interactor)
        {
            if (IsLooted) return false;
            return Vector3.Distance(transform.position, interactor.transform.position) <= 3f;
        }

        public void Interact(GameObject interactor)
        {
            if (!IsServerStarted) return; // should never happen; ServerRpc enforces ownership
            Open(interactor.GetComponent<FishNet.Object.NetworkObject>()?.Owner);
        }

        /// <summary>ServerRpc — client asks to open the chest.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestOpen(NetworkObject playerNob)
        {
            if (playerNob == null || IsLooted) return;
            Open(playerNob.Owner);
        }

        [Server]
        private void Open(NetworkConnection conn)
        {
            _isOpen.Value = true;
            OnChestOpened?.Invoke(this, conn);
            RpcNotifyOpen(conn);
        }

        /// <summary>Remove an item once the player takes it.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void TakeItem(int storageIndex)
        {
            if (storageIndex < 0 || storageIndex >= _syncStorage.Count) return;
            _syncStorage.RemoveAt(storageIndex);

            if (_syncStorage.Count == 0)
                _isLooted.Value = true;
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region RPCs

        [TargetRpc]
        private void RpcNotifyOpen(NetworkConnection conn)
        {
            // Client shows loot UI via LootContainerUI (which listens for ILootable.IsOpen changes)
            Debug.Log("[BossChest] Chest opened for client.");
        }

        #endregion
    }
}
