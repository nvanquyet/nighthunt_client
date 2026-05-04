using FishNet.Object;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Utilities;
using UnityEngine;

namespace NightHunt.GameplaySystems.Loot
{
    public static class LootTransferUtility
    {
        private const float WeightEpsilon = 0.0001f;

        public static int CalculateCarryableQuantity(
            NetworkObject playerNob,
            IInventorySystem inventory,
            ItemInstanceData itemData,
            int requestedQuantity)
        {
            if (inventory == null || requestedQuantity <= 0 || string.IsNullOrEmpty(itemData.DefinitionID))
                return 0;

            var itemDef = ItemDatabase.GetDefinition(itemData.DefinitionID);
            if (itemDef == null)
                return 0;

            var statSystem = ResolveStatSystem(playerNob);
            if (statSystem == null)
                return requestedQuantity;

            float capacity = statSystem.GetWeightCapacity();
            if (capacity <= WeightEpsilon)
                return 0;

            float currentWeight = inventory.CalculateTotalWeight();
            float remainingWeight = capacity - currentWeight;
            if (remainingWeight <= WeightEpsilon)
                return 0;

            float unitWeight = Mathf.Max(0f, itemDef.GetTotalWeight(1));
            if (unitWeight <= WeightEpsilon)
                return requestedQuantity;

            int maxByWeight = Mathf.FloorToInt((remainingWeight + WeightEpsilon) / unitWeight);
            return Mathf.Clamp(maxByWeight, 0, requestedQuantity);
        }

        public static bool TryAddItemWithinWeight(
            NetworkObject playerNob,
            IInventorySystem inventory,
            ItemInstanceData itemData,
            int requestedQuantity,
            bool newInstanceId,
            out int acceptedQuantity)
        {
            acceptedQuantity = CalculateCarryableQuantity(playerNob, inventory, itemData, requestedQuantity);
            if (acceptedQuantity <= 0)
            {
                LogWeightReject(playerNob, inventory, itemData, requestedQuantity);
                return false;
            }

            var acceptedData = ItemInstanceFactory.CopyDataForQuantity(itemData, acceptedQuantity, newInstanceId);
            inventory.AddItemFromData(acceptedData);
            return true;
        }

        private static IPlayerStatSystem ResolveStatSystem(NetworkObject playerNob)
        {
            if (playerNob == null)
                return null;

            return ComponentResolver.Find<IPlayerStatSystem>(playerNob)
                .OnSelf()
                .InChildren()
                .InParent()
                .OrDefault(null)
                .Resolve();
        }

        private static void LogWeightReject(
            NetworkObject playerNob,
            IInventorySystem inventory,
            ItemInstanceData itemData,
            int requestedQuantity)
        {
            var cfg = NightHuntDebugConfig.Instance;
            if (cfg == null || !cfg.EnableInventoryDebugLogs)
                return;

            var statSystem = ResolveStatSystem(playerNob);
            float current = inventory?.CalculateTotalWeight() ?? 0f;
            float capacity = statSystem?.GetWeightCapacity() ?? 0f;
            var itemDef = ItemDatabase.GetDefinition(itemData.DefinitionID);
            float unitWeight = itemDef != null ? itemDef.GetTotalWeight(1) : 0f;

            Debug.Log(
                $"[LOOT_WEIGHT] Reject pickup def={itemData.DefinitionID} qty={requestedQuantity} " +
                $"current={current:F2} capacity={capacity:F2} unit={unitWeight:F2}");
        }
    }
}
