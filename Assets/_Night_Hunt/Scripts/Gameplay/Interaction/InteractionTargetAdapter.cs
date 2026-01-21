using UnityEngine;
using FishNet.Object;
using NightHunt.Gameplay.Loot;
using NightHunt.Data;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Adapter to make NetworkLootItem implement IInteractionTarget
    /// Follows Adapter pattern for SOLID compliance
    /// </summary>
    public class InteractionTargetAdapter : MonoBehaviour, IInteractionTarget
    {
        private NetworkLootItem lootItem;
        private NetworkObject networkObject;

        private void Awake()
        {
            lootItem = GetComponent<NetworkLootItem>();
            networkObject = GetComponent<NetworkObject>();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (lootItem == null || lootItem.IsLooted) return false;
            if (networkObject == null || !networkObject.IsSpawned) return false;

            // Check distance
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            return distance <= 5f; // Max interaction range
        }

        public string GetInteractionText()
        {
            if (lootItem == null) return "";
            
            var config = GameConfigLoader.Instance?.GetItemConfig(lootItem.ItemId);
            string itemName = config != null ? config.DisplayName : lootItem.ItemId;
            
            return $"Press E to pick up {itemName}";
        }

        public uint GetNetworkObjectId()
        {
            if (networkObject == null) return 0;
            return unchecked((uint)networkObject.ObjectId);
        }

        public string GetInteractionType()
        {
            return "Pickup";
        }
    }
}

