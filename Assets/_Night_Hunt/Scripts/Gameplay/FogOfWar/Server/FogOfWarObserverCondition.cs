using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// FishNet observer condition for server-side Fog of War visibility.
    ///
    /// Do not attach this to live player prefabs. FishNet observer culling stops the
    /// entire NetworkObject stream for that client, including prediction/reconcile,
    /// SyncVars, RPCs, and spawn/despawn lifecycle. Player FoW must be implemented
    /// as visual/gameplay affordance logic while the player NetworkObject remains
    /// scene-observed.
    ///
    /// Use this only for non-player objects that can be safely despawned from a
    /// client while hidden.
    /// </summary>
    [CreateAssetMenu(
        menuName = "NightHunt/Observers/FogOfWar Condition",
        fileName = "FogOfWarObserverCondition")]
    public class FogOfWarObserverCondition : ObserverCondition
    {
        /// <summary>
        /// Timed conditions are rechecked by FishNet at regular observer intervals.
        /// </summary>
        public override ObserverConditionType GetConditionType()
            => ObserverConditionType.Timed;

        /// <summary>
        /// Returns whether <paramref name="connection"/> should currently observe this object.
        /// Called on the server by FishNet's observer system.
        /// </summary>
        public override bool ConditionMet(
            NetworkConnection connection,
            bool currentlyAdded,
            out bool notProcessed)
        {
            notProcessed = false;

            // Owner always sees their own object.
            if (NetworkObject != null && NetworkObject.Owner == connection)
                return true;

            // Player NetworkObjects must never be observer-culled by FoW.
            // Culling them cuts movement, SyncVars, RPCs, and spawn lifecycle.
            if (NetworkObject != null && NetworkObject.GetComponent<NetworkPlayer>() != null)
                return true;

            // Fail open if the service is not running, such as offline/client-only scenes.
            ServerFogVisibilityService svc = ServerFogVisibilityService.Instance;
            if (svc == null)
                return true;

            int observerNetObjId = -1;
            foreach (NetworkObject nob in connection.Objects)
            {
                if (nob.GetComponent<NetworkPlayer>() != null)
                {
                    observerNetObjId = nob.ObjectId;
                    break;
                }
            }

            // Connecting clients or spectators without a body should not be culled.
            if (observerNetObjId < 0 || NetworkObject == null)
                return true;

            return svc.IsVisible(observerNetObjId, NetworkObject.ObjectId);
        }
    }
}
