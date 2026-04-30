using System;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for item use system
    /// 
    /// RESPONSIBILITIES:
    /// - Handle consumable item usage (medkits, food, etc.)
    /// - Handle throwable item usage (grenades, etc.)
    /// - Manage item use state and progress
    /// - Coordinate with WeaponSystem for holster/restore
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All use operations on server
    /// - Events sync to clients for UI updates
    /// </summary>
    public interface IItemUseSystem
    {
        /// <summary>
        /// Whether an item is currently being used
        /// </summary>
        bool IsUsingItem { get; }
        
        /// <summary>
        /// Currently active item being used
        /// Returns null if no item in use
        /// </summary>
        ItemInstance CurrentItem { get; }

        /// <summary>
        /// True while a deployable placement preview is active on the owning client.
        /// </summary>
        bool IsDeploying { get; }
        
        /// <summary>
        /// Start using an item
        /// 
        /// PARAMETERS:
        /// - item: Item instance to use
        /// 
        /// RETURNS:
        /// - True if use started successfully, false otherwise
        /// 
        /// NETWORK:
        /// - Server-only operation
        /// </summary>
        bool UseItem(ItemInstance item);
        
        /// <summary>
        /// Cancel in-progress item use
        /// 
        /// NETWORK:
        /// - Server-only operation
        /// </summary>
        void CancelUse();
        
        /// <summary>
        /// Execute throw (for throwable items)
        /// Called when Fire pressed during throw-mode
        /// 
        /// NETWORK:
        /// - Server-only operation
        /// </summary>
        void ExecuteThrow(Vector3 aimTarget);

        /// <summary>
        /// Request throw execution from the owning client.
        /// Routes to server via ServerRpc — use this from client-side code
        /// (e.g. CombatInputHandler, ItemAimController mobile path).
        /// FishNet guarantees ServerRpcs from the same client are ordered, so
        /// selecting an item (ServerRpc path) then RequestExecuteThrow(ServerRpc)
        /// always processes BeginThrowable before ExecuteThrow on the server.
        /// </summary>
        void RequestExecuteThrow(Vector3 aimTarget);

        /// <summary>
        /// Confirm the active deployable preview from the owning client.
        /// Returns true when placement confirmation was accepted locally.
        /// </summary>
        bool TryConfirmDeploy();

        /// <summary>
        /// Request cancel of the in-progress item use from the owning client.
        /// Routes to <see cref="CancelUse"/> on the server via ServerRpc.
        /// Safe to call when no item is in use (no-op).
        /// </summary>
        void RequestCancelUse();
        
        #region Events
        
        /// <summary>
        /// Event fired when item use started
        /// Parameters: (item)
        /// </summary>
        event Action<ItemInstance> OnItemUseStarted;
        
        /// <summary>
        /// Event fired when item use completed
        /// Parameters: (item)
        /// </summary>
        event Action<ItemInstance> OnItemUseCompleted;
        
        /// <summary>
        /// Event fired when item use cancelled
        /// Parameters: (item)
        /// </summary>
        event Action<ItemInstance> OnItemUseCancelled;

        /// <summary>
        /// Event fired the instant a throw is executed (projectile spawned).
        /// Subscribe in CharacterAnimationController to trigger the Throw animator parameter.
        /// </summary>
        event Action OnThrowExecuted;
        
        /// <summary>
        /// Event fired during item use progress
        /// Parameters: (item, progress) where progress is 0.0-1.0
        /// </summary>
        event Action<ItemInstance, float> OnItemUseProgress;
        
        #endregion
    }
}
