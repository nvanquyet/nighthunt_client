using System.Collections.Generic;
using FishNet.Connection;

namespace NightHunt.InteractionSystem.Core
{
    public interface ILootContainer
    {
        IReadOnlyList<ItemInstance> ContainerItems { get; }
        int MaxSlots { get; }
        bool IsLocked { get; }
        bool CanLoot(NetworkConnection player);
        bool TryAddItem(ItemInstance item);
        bool TryRemoveItem(string itemId);
    }
}