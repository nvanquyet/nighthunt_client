using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;

namespace NightHunt.Networking.Prediction.FishNet
{
    /// <summary>
    /// Base FishNet predicted behaviour using TickNetworkBehaviour pattern.
    /// 
    /// ARCHITECTURE:
    /// - Generic base class cho CSP (Client-Side Prediction)
    /// - Kế thừa từ TickNetworkBehaviour
    /// - Enforce [Replicate]/[Reconcile] pattern
    /// 
    /// RESPONSIBILITIES:
    /// - Setup tick callbacks
    /// - Provide utility methods (TickDelta, IsReplaying)
    /// - Define abstract methods cho derived classes
    /// 
    /// USAGE:
    /// Derived classes MUST implement:
    /// - TimeManager_OnTick(): Build và send replicate data
    /// - CreateReconcileData(): Tạo reconcile state
    /// - [Replicate] method: Simulate movement
    /// - [Reconcile] method: Apply server state
    /// </summary>
    public abstract class FishNetPredictedBehaviour<TReplicate, TReconcile> : TickNetworkBehaviour
        where TReplicate : struct, IReplicateData
        where TReconcile : struct, IReconcileData
    {
        /// <summary>
        /// Delta time per tick, dùng cho tất cả physics calculations
        /// </summary>
        public float TickDelta => (float)base.TimeManager.TickDelta;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            SetTickCallbacks(TickCallback.Tick);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            SetTickCallbacks(TickCallback.None);
        }

        /// <summary>
        /// Create reconcile data for server to send to client.
        /// Called by both server and client mỗi tick.
        /// </summary>
        protected abstract TReconcile CreateReconcileData();

        /// <summary>
        /// Helper to check if currently replaying during reconcile.
        /// Replayed flag chỉ có ở client khi đang reconcile.
        /// </summary>
        protected bool IsReplaying(ReplicateState state)
        {
            return (state & ReplicateState.Replayed) == ReplicateState.Replayed;
        }

        /// <summary>
        /// Check if state contains Ticked flag.
        /// Ticked = Data được chạy từ OnTick (không phải từ reconcile).
        /// </summary>
        protected bool IsTicked(ReplicateState state)
        {
            return (state & ReplicateState.Ticked) == ReplicateState.Ticked;
        }

        /// <summary>
        /// Check if state contains Created flag.
        /// Created = Data được tạo bởi server/client (có input thật).
        /// </summary>
        protected bool IsCreated(ReplicateState state)
        {
            return (state & ReplicateState.Created) == ReplicateState.Created;
        }
    }
}