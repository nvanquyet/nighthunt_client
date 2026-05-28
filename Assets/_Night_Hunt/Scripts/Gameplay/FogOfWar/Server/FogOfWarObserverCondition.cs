using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// FishNet <see cref="ObserverCondition"/> that gates network visibility of a spawned object
    /// behind server-side Fog of War line-of-sight, computed by <see cref="ServerFogVisibilityService"/>.
    ///
    /// ══════════════════════════════════════════════════════
    ///  HOW IT WORKS
    /// ══════════════════════════════════════════════════════
    ///  FishNet calls ConditionMet(connection, ...) on the SERVER for every connection
    ///  that might observe the object this condition is attached to.
    ///
    ///  This condition asks: "Can the player owned by <paramref name='connection'/> currently
    ///  see this NetworkObject?"
    ///
    ///  If false → FishNet stops sending position/state updates for this object to that client.
    ///  If true  → FishNet includes this object in the client's observer set (normal updates).
    ///
    ///  Because it is a Timed condition, FishNet re-evaluates it at regular observer update
    ///  intervals (configurable on ObserverManager — default 0.5 s, recommend 0.1–0.2 s for FPS).
    ///
    /// ══════════════════════════════════════════════════════
    ///  ALWAYS-VISIBLE CASES (short-circuit, never culled)
    /// ══════════════════════════════════════════════════════
    ///  • connection == NetworkObject.Owner    (player always sees their own object)
    ///  • ServerFogVisibilityService not found (fail open — avoids startup cull)
    ///  • Observer has no spawned NetworkPlayer (connecting / pure spectator)
    ///
    /// ══════════════════════════════════════════════════════
    ///  SETUP
    /// ══════════════════════════════════════════════════════
    ///  1. Right-click in Project → Create → NightHunt → Observers → FogOfWar Condition
    ///     to create the ScriptableObject asset (one instance shared by all prefabs).
    ///  2. On the player prefab: add a NetworkObserver component.
    ///     Drag the asset into NetworkObserver → Observer Conditions list.
    ///  3. Ensure <see cref="ServerFogVisibilityService"/> MonoBehaviour is in the server scene.
    ///  4. Tune ObserverManager "Observer Update Interval" on the NetworkManager
    ///     (recommend 0.1–0.2 s for an FPS game).
    ///
    /// ══════════════════════════════════════════════════════
    ///  RUNTIME WIRING (alternative to Inspector setup)
    /// ══════════════════════════════════════════════════════
    ///  NetworkPlayer.OnStartServer can programmatically add this condition via:
    ///    var cond = ScriptableObject.CreateInstance&lt;FogOfWarObserverCondition&gt;();
    ///    NetworkObject.NetworkObserver.ObserverConditionsInternal.Add(cond);
    ///    cond.Initialize(NetworkObject);
    ///  See NetworkPlayer.OnStartServer for the actual wiring.
    /// </summary>
    [CreateAssetMenu(
        menuName  = "NightHunt/Observers/FogOfWar Condition",
        fileName  = "FogOfWarObserverCondition")]
    public class FogOfWarObserverCondition : ObserverCondition
    {
        /// <summary>
        /// Timed = FishNet re-checks this condition at regular intervals rather than
        /// only when something changes.  Required for a dynamic LoS system.
        /// </summary>
        public override ObserverConditionType GetConditionType()
            => ObserverConditionType.Timed;

        /// <summary>
        /// Returns whether <paramref name="connection"/> should currently observe this object.
        /// Called on the SERVER by FishNet's observer system.
        /// </summary>
        public override bool ConditionMet(
            NetworkConnection connection,
            bool              currentlyAdded,
            out bool          notProcessed)
        {
            notProcessed = false;

            // 1. Owner always sees their own object.
            if (NetworkObject != null && NetworkObject.Owner == connection)
                return true;

            // 2. Fail open if the service is not running (e.g. offline / client-only scenes).
            var svc = ServerFogVisibilityService.Instance;
            if (svc == null)
            {
                notProcessed = true;
                return currentlyAdded;
            }

            // 3. Locate the observer's NetworkPlayer object.
            //    connection.Objects contains all NetworkObjects owned by that client.
            int observerNetObjId = -1;
            foreach (NetworkObject nob in connection.Objects)
            {
                if (nob.GetComponent<NetworkPlayer>() != null)
                {
                    observerNetObjId = nob.ObjectId;
                    break;
                }
            }

            // 4. No player object found (still connecting, spectator without body, etc.) → keep current.
            if (observerNetObjId < 0 || NetworkObject == null)
            {
                notProcessed = true;
                return currentlyAdded;
            }

            // 5. Delegate to the server visibility map.
            return svc.IsVisible(observerNetObjId, NetworkObject.ObjectId);
        }
    }
}
