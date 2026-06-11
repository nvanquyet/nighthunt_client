using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Scoring
{
    /// <summary>
    /// Network sync for scores
    /// </summary>
    public class ScoreSync : NetworkBehaviour
    {
        private readonly SyncVar<string> networkScoreData = new SyncVar<string>();

        private ScoringSystem scoringSystem;
        private string _lastPublishedScoreData;

        private void Awake()
        {
            scoringSystem = ComponentResolver.Find<ScoringSystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] ScoringSystem not found")
        .Resolve();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkScoreData.OnChange += OnScoreDataChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            networkScoreData.OnChange -= OnScoreDataChanged;
        }

        /// <summary>
        /// Server: Sync score data
        /// </summary>
        public void SyncScoreData(string scoreData)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(scoreData))
                return;

            networkScoreData.Value = scoreData;
            RpcScoreDataSynced(scoreData);
        }

        private void OnScoreDataChanged(string oldData, string newData, bool asServer)
        {
            // Host callbacks arrive asServer=true, but the host still has a local client HUD.
            // Dedicated servers have IsClientInitialized=false and must not publish UI events.
            if (!asServer || IsClientInitialized)
                PublishScoreData(newData);
        }

        [ObserversRpc]
        private void RpcScoreDataSynced(string scoreData)
        {
            PublishScoreData(scoreData);
        }

        private void PublishScoreData(string scoreData)
        {
            if (string.IsNullOrEmpty(scoreData) || scoreData == _lastPublishedScoreData)
                return;

            _lastPublishedScoreData = scoreData;
            GameplayEventBus.Instance?.Publish(new ScoreDataSyncedEvent { ScoreDataJson = scoreData });
        }
    }
}
