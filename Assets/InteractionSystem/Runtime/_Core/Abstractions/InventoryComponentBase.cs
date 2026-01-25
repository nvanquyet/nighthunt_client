using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace NightHunt.InteractionSystem.Core
{
    public abstract class InventoryComponentBase : NetworkBehaviour
    {
        protected readonly SyncList<ItemInstance> items = new SyncList<ItemInstance>();
    
        public IReadOnlyList<ItemInstance> Items => items;
        public abstract int Capacity { get; }
    
        [Server]
        public virtual bool TryAddItem(ItemInstance item)
        {
            if (items.Count >= Capacity) return false;
            items.Add(item);
            OnItemAdded(item);
            return true;
        }
    
        [Server]
        public virtual bool TryRemoveItem(string instanceId)
        {
            int index = items.FindIndex(i => i.instanceId == instanceId);
            if (index < 0) return false;
        
            ItemInstance removed = items[index];
            items.RemoveAt(index);
            OnItemRemoved(removed);
            return true;
        }
    
        protected virtual void OnItemAdded(ItemInstance item) { }
        protected virtual void OnItemRemoved(ItemInstance item) { }
    }
}