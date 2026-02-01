using System.Linq;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Loot.Definitions;

namespace NightHunt.InteractionSystem.Items.Runtime
{
    /// <summary>
    /// Network loot item that implements IPickupable (separate from IInteractable).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NetworkLootItem : NetworkBehaviour, IPickupable
    {
        [Header("TEST ONLY (for prefab testing)")]
        [SerializeField] private LootItemDefinition testDefinition;

        [Header("Runtime (set by spawner)")]
        private readonly SyncVar<string> syncDefinitionId = new SyncVar<string>();
        [SerializeField] private ItemDataBase itemData;
        private readonly SyncVar<int> syncQuantity = new SyncVar<int>(1);
        [SerializeField] private float pickupRange = 3f;

        // Properties for easy access
        private string definitionId => syncDefinitionId.Value;
        private int quantity => syncQuantity.Value;

        [Header("Item State (preserved when dropped)")]
        [SerializeField] private float durability = -1f; // -1 = not applicable
        [SerializeField] private string customData = string.Empty; // JSON string for ammo, attachments, etc.
        [SerializeField] private string instanceId = string.Empty; // Original instance ID

        [Header("Visual")]
        [SerializeField] private Transform modelRoot;
        
        [Header("VFX (for future implementation)")]
        [SerializeField] private GameObject highlightVFX;
        [SerializeField] private GameObject targetVFX;

        private bool isPickedUp = false;
        private LootItemDefinition _definition;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (modelRoot == null)
                modelRoot = transform;

            // Subscribe to SyncVar changes
            syncDefinitionId.OnChange += OnDefinitionIdChanged;
            syncQuantity.OnChange += OnQuantityChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (syncDefinitionId != null)
                syncDefinitionId.OnChange -= OnDefinitionIdChanged;
            if (syncQuantity != null)
                syncQuantity.OnChange -= OnQuantityChanged;
        }

        private void OnDefinitionIdChanged(string oldValue, string newValue, bool asServer)
        {
            Debug.Log($"[NetworkLootItem] OnDefinitionIdChanged called. Old: '{oldValue}', New: '{newValue}', asServer: {asServer}");
            
            if (!asServer && !string.IsNullOrEmpty(newValue) && _definition == null)
            {
                Debug.Log($"[NetworkLootItem] ✅ definitionId synced to client: '{newValue}'. Resolving visual now...");
                ResolveAndApplyVisual();
            }
            else if (asServer)
            {
                Debug.Log($"[NetworkLootItem] Server side change (ignored on client)");
            }
            else if (string.IsNullOrEmpty(newValue))
            {
                Debug.LogWarning($"[NetworkLootItem] definitionId changed to empty string!");
            }
            else if (_definition != null)
            {
                Debug.Log($"[NetworkLootItem] Definition already resolved, skipping visual resolution.");
            }
        }

        private void OnQuantityChanged(int oldValue, int newValue, bool asServer)
        {
            // Quantity changed - can update UI if needed
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Prefab testing path: if testDefinition is assigned, initialize automatically once spawned.
            if (_definition == null && testDefinition != null)
            {
                int q = Random.Range(
                    Mathf.Max(1, testDefinition.DefaultMinQuantity),
                    Mathf.Max(1, testDefinition.DefaultMaxQuantity) + 1
                );
                ServerInitialize(testDefinition, q);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            Debug.Log($"[NetworkLootItem] OnStartClient called. GameObject: {gameObject.name}, NetworkObjectId: {ObjectId}, IsSpawned: {IsSpawned}");
            Debug.Log($"[NetworkLootItem] Current definitionId (from SyncVar): '{definitionId}', _definition: {(_definition != null ? "exists" : "null")}");

            // Client join late: if definitionId is already synced (from server), resolve and apply visual
            // This handles the case where client joins after item was spawned
            if (!string.IsNullOrEmpty(definitionId) && _definition == null)
            {
                Debug.Log($"[NetworkLootItem] ✅ Client late join detected. definitionId already synced: '{definitionId}'. Resolving visual now...");
                ResolveAndApplyVisual();
            }
            else if (string.IsNullOrEmpty(definitionId))
            {
                Debug.Log($"[NetworkLootItem] ⏳ Client started but definitionId is empty. Waiting for SyncVar sync or ObserversSetLoot RPC...");
                Debug.Log($"[NetworkLootItem] SyncVar value: '{syncDefinitionId.Value}', Is null/empty: {string.IsNullOrEmpty(syncDefinitionId.Value)}");
            }
            else if (_definition != null)
            {
                Debug.Log($"[NetworkLootItem] ✅ Definition already resolved. Visual should be applied.");
            }
        }

        /// <summary>
        /// Show VFX effects (highlight, target indicator, etc.)
        /// Can be called when player targets this item or for other visual feedback.
        /// </summary>
        public void ShowVFX(bool showHighlight = true, bool showTarget = false)
        {
            if (highlightVFX != null)
            {
                highlightVFX.SetActive(showHighlight);
            }

            if (targetVFX != null)
            {
                targetVFX.SetActive(showTarget);
            }
        }

        /// <summary>
        /// Hide all VFX effects
        /// </summary>
        public void HideVFX()
        {
            ShowVFX(false, false);
        }

        /// <summary>
        /// Server: initialize this loot item from a definition chosen by a spawner.
        /// </summary>
        [Server]
        public void ServerInitialize(LootItemDefinition def, int qty)
        {
            Debug.Log($"[NetworkLootItem] [SERVER] ServerInitialize called. definitionId: '{def?.DefinitionId}', quantity: {qty}, NetworkObjectId: {ObjectId}");

            if (def == null || def.ItemData == null)
            {
                Debug.LogWarning("[NetworkLootItem] [SERVER] ServerInitialize called with null definition or ItemData.");
                return;
            }

            _definition = def;
            syncDefinitionId.Value = def.DefinitionId;
            itemData = def.ItemData;
            syncQuantity.Value = Mathf.Max(1, qty);
            pickupRange = Mathf.Max(0.1f, def.PickupRange);

            // Reset state (for fresh spawns)
            durability = -1f;
            customData = string.Empty;
            instanceId = string.Empty;

            Debug.Log($"[NetworkLootItem] [SERVER] Setting SyncVar definitionId to: '{def.DefinitionId}'");
            ApplyWorldPresentation(def);
            
            Debug.Log($"[NetworkLootItem] [SERVER] Sending ObserversSetLoot RPC to all clients (including late joiners with BufferLast=true)");
            ObserversSetLoot(syncDefinitionId.Value, syncQuantity.Value, durability, customData, instanceId);
        }

        /// <summary>
        /// Server: initialize this loot item from an ItemInstance (preserves state when dropping).
        /// </summary>
        [Server]
        public void ServerInitializeFromItemInstance(Core.Structs.ItemInstance itemInstance, LootItemDefinition def)
        {
            if (itemInstance.itemDataId == null || def == null || def.ItemData == null)
            {
                Debug.LogWarning("[NetworkLootItem] ServerInitializeFromItemInstance called with invalid data.");
                return;
            }

            _definition = def;
            syncDefinitionId.Value = def.DefinitionId;
            itemData = def.ItemData;
            syncQuantity.Value = itemInstance.quantity;
            pickupRange = Mathf.Max(0.1f, def.PickupRange);

            // Preserve state from ItemInstance
            durability = itemInstance.durability;
            customData = itemInstance.customData ?? string.Empty;
            instanceId = itemInstance.instanceId ?? string.Empty;

            ApplyWorldPresentation(def);
            ObserversSetLoot(syncDefinitionId.Value, syncQuantity.Value, durability, customData, instanceId);
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversSetLoot(string defId, int qty, float dur, string custom, string instId)
        {
            Debug.Log($"[NetworkLootItem] ObserversSetLoot RPC received on client. definitionId: '{defId}', quantity: {qty}");
            
            // SyncVar will be set automatically, but we also set state here
            // Note: SyncVar changes will trigger OnDefinitionIdChanged which calls ResolveAndApplyVisual
            durability = dur;
            customData = custom ?? string.Empty;
            instanceId = instId ?? string.Empty;

            // If SyncVar hasn't triggered yet, resolve manually
            if (!string.IsNullOrEmpty(defId) && _definition == null)
            {
                ResolveAndApplyVisual();
            }
        }

        /// <summary>
        /// Resolve LootItemDefinition and apply visual (called on client).
        /// </summary>
        private void ResolveAndApplyVisual()
        {
            if (string.IsNullOrEmpty(definitionId))
            {
                Debug.LogWarning("[NetworkLootItem] Cannot resolve visual: definitionId is empty.");
                return;
            }

            Debug.Log($"[NetworkLootItem] Resolving visual for definitionId: '{definitionId}'");

            var db = LootItemDefinitionDatabase.Load();
            if (db == null)
            {
                Debug.LogError("[NetworkLootItem] LootItemDefinitionDatabase not found in Resources/LootItemDefinitionDatabase.asset. Cannot resolve loot definition. Please create the database asset and place it in Resources folder.");
                return;
            }

            Debug.Log($"[NetworkLootItem] Database loaded. Looking for definition '{definitionId}'...");

            var def = db.GetById(definitionId);
            if (def == null)
            {
                Debug.LogError($"[NetworkLootItem] Loot definition '{definitionId}' not found in database. Please check that the definition exists in LootItemDefinitionDatabase.");
                
                // List all available definitions for debugging
                var allDefs = db.GetAllDefinitions();
                Debug.Log($"[NetworkLootItem] Available definitions in database: {string.Join(", ", allDefs.Select(d => d.DefinitionId))}");
                return;
            }

            Debug.Log($"[NetworkLootItem] Definition found: '{def.DefinitionId}'. ItemData: {(def.ItemData != null ? def.ItemData.ItemId : "NULL")}");

            if (def.ItemData == null)
            {
                Debug.LogError($"[NetworkLootItem] Loot definition '{definitionId}' has null ItemData. Please assign ItemData to the definition.");
                return;
            }

            _definition = def;
            itemData = def.ItemData;
            pickupRange = Mathf.Max(0.1f, def.PickupRange);

            Debug.Log($"[NetworkLootItem] Definition resolved. WorldPrefab: {(def.WorldPrefab != null ? def.WorldPrefab.name : "NULL")}");

            // Apply visual model
            ApplyWorldPresentation(def);

            if (def.WorldPrefab == null)
            {
                Debug.LogWarning($"[NetworkLootItem] Loot definition '{definitionId}' has null WorldPrefab. Item will spawn but no visual model will be shown. Please assign a WorldPrefab to the definition.");
            }
            else
            {
                Debug.Log($"[NetworkLootItem] Visual model applied successfully for '{definitionId}'.");
            }
        }

        /// <summary>
        /// Get ItemInstance with preserved state (for pickup).
        /// </summary>
        public Core.Structs.ItemInstance GetItemInstance()
        {
            if (itemData == null)
            {
                Debug.LogError("[NetworkLootItem] GetItemInstance called but itemData is NULL!");
                return default;
            }

            Debug.Log($"[NetworkLootItem] GetItemInstance - itemData.ItemId: '{itemData.ItemId}', itemData.name: '{itemData.name}', quantity: {quantity}");

            // Create item instance - constructor will auto-generate GUID if instanceId is empty
            var itemInstance = new Core.Structs.ItemInstance(itemData.ItemId, quantity, durability)
            {
                customData = customData
            };
            
            // Only preserve instanceId if it's not empty (for dropped items with preserved state)
            // If empty, let the constructor's auto-generated GUID remain
            if (!string.IsNullOrEmpty(instanceId))
            {
                itemInstance.instanceId = instanceId;
            }
            
            Debug.Log($"[NetworkLootItem] Created ItemInstance - instanceId: '{itemInstance.instanceId}', itemDataId: '{itemInstance.itemDataId}', quantity: {itemInstance.quantity}");
            
            return itemInstance;
        }

        private void ApplyWorldPresentation(LootItemDefinition def)
        {
            if (def == null)
            {
                Debug.LogWarning("[NetworkLootItem] ApplyWorldPresentation called with null definition.");
                return;
            }

            if (modelRoot == null)
            {
                Debug.LogWarning("[NetworkLootItem] modelRoot is null. Cannot apply world presentation. Setting modelRoot to transform.");
                modelRoot = transform;
            }

            // Clear previous spawned model children (keep modelRoot itself)
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
            {
                var child = modelRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            if (def.WorldPrefab == null)
            {
                Debug.LogWarning($"[NetworkLootItem] WorldPrefab is null for definition '{def.DefinitionId}'. Item will be invisible. Please assign a WorldPrefab to the LootItemDefinition.");
                return;
            }

            var go = Instantiate(def.WorldPrefab, modelRoot);
            go.transform.localPosition = def.WorldPrefabLocalPosition;
            go.transform.localRotation = Quaternion.Euler(def.WorldPrefabLocalEuler);
            go.transform.localScale = def.WorldPrefabLocalScale;

            Debug.Log($"[NetworkLootItem] Applied world presentation for '{def.DefinitionId}' with prefab '{def.WorldPrefab.name}'.");
        }

        // IPickupable implementation
        public bool CanPickup(GameObject player)
        {
            if (isPickedUp)
                return false;

            if (itemData == null)
                return false;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= pickupRange;
        }

        public ItemDataBase GetItemData()
        {
            return itemData;
        }

        public int GetQuantity()
        {
            return quantity;
        }

        public void OnPickedUp(GameObject player)
        {
            isPickedUp = true;

            // Despawn on server
            if (IsServer)
            {
                DespawnAfterDelay(0.5f);
            }
        }

        public string GetDisplayName()
        {
            return itemData != null ? itemData.DisplayName : "Unknown Item";
        }

        public float GetPickupRange()
        {
            return pickupRange;
        }

        /// <summary>
        /// Despawn after delay.
        /// </summary>
        [Server]
        private void DespawnAfterDelay(float delay)
        {
            Invoke(nameof(DespawnItem), delay);
        }

        /// <summary>
        /// Despawn the item.
        /// </summary>
        [Server]
        private void DespawnItem()
        {
            if (IsSpawned)
            {
                Despawn();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
