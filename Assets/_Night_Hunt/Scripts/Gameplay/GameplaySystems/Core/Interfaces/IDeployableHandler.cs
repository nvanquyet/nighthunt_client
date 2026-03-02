using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Implemented by any component that handles placing/deploying an item
    /// (e.g. BeaconPlaceable).  QuickSlotSystem routes Deployable items through
    /// this interface via TargetRpc so the server never tries to direct-consume
    /// a deployable while leaving the client-side placement preview running.
    /// </summary>
    public interface IDeployableHandler
    {
        /// <summary>
        /// Start the deployment flow for <paramref name="item"/>.
        /// Runs on the owning client only.
        /// </summary>
        /// <returns>True if the handler accepted the item.</returns>
        bool BeginDeploy(ItemInstance item, ItemDefinition def);

        /// <summary>Cancel any in-progress deployment.</summary>
        void CancelDeploy();
    }
}
