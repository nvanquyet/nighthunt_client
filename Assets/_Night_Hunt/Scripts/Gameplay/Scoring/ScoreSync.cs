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

            UnityEngine.Debug.Log($"[ScoreSync] SyncScoreData (Server): setting SyncVar networkScoreData and calling RpcScoreDataSynced. Json len = {scoreData.Length}");
            networkScoreData.Value = scoreData;
            RpcScoreDataSynced(scoreData);
        }

        private void OnScoreDataChanged(string oldData, string newData, bool asServer)
        {
            UnityEngine.Debug.Log($"[ScoreSync] OnScoreDataChanged (Client/Server): oldLen={oldData?.Length ?? 0}, newLen={newData?.Length ?? 0}, asServer={asServer}, IsClientInitialized={IsClientInitialized}");
            // Host callbacks arrive asServer=true, but the host still has a local client HUD.
            // Dedicated servers have IsClientInitialized=false and must not publish UI events.
            if (!asServer || IsClientInitialized)
                PublishScoreData(newData);
        }

        [ObserversRpc]
        private void RpcScoreDataSynced(string scoreData)
        {
            UnityEngine.Debug.Log($"[ScoreSync] RpcScoreDataSynced (Client ObserversRpc): Json len = {scoreData?.Length ?? 0}");
            PublishScoreData(scoreData);
        }

        private void PublishScoreData(string scoreData)
        {
            if (string.IsNullOrEmpty(scoreData))
            {
                UnityEngine.Debug.LogWarning("[ScoreSync] PublishScoreData: scoreData is null or empty");
                return;
            }

            if (scoreData == _lastPublishedScoreData)
                return;

            UnityEngine.Debug.Log($"[ScoreSync] PublishScoreData: publishing ScoreDataSyncedEvent. Json = {scoreData}");
            _lastPublishedScoreData = scoreData;
            GameplayEventBus.Instance?.Publish(new ScoreDataSyncedEvent { ScoreDataJson = scoreData });
        }
    }
}
