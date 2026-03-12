using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
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
            if (IsServer)
            {
                networkScoreData.Value = scoreData;
            }
        }

        private void OnScoreDataChanged(string oldData, string newData, bool asServer)
        {
            if (!asServer && !string.IsNullOrEmpty(newData))
            {
                GameplayEventBus.Instance?.Publish(new ScoreDataSyncedEvent { ScoreDataJson = newData });
            }
        }
    }
}
