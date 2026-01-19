using UnityEngine;
using NightHunt.Gameplay.Loot;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Crate drop objective
    /// </summary>
    public class CrateDropObjective : MonoBehaviour, IObjective
    {
        [Header("Crate Drop Settings")]
        [SerializeField] private string objectiveId = "CRATE_DROP";
        [SerializeField] private string objectiveName = "Secure Crate";
        [SerializeField] private LootItem crateLoot;

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted { get; private set; }
        public float Progress { get; private set; }

        public void OnStart()
        {
            IsCompleted = false;
            Progress = 0f;

            if (crateLoot != null)
            {
                crateLoot.OnLooted += OnCrateLooted;
            }
        }

        public void OnUpdate()
        {
            // Crate drop is completed when looted
            if (crateLoot != null && crateLoot.IsLooted)
            {
                Progress = 1f;
                if (!IsCompleted)
                {
                    OnComplete();
                }
            }
        }

        public void OnComplete()
        {
            IsCompleted = true;
            Progress = 1f;
            Debug.Log($"[CrateDropObjective] Crate secured: {objectiveName}");
        }

        public void OnFail()
        {
            // Crate drop doesn't fail
        }

        private void OnCrateLooted()
        {
            OnComplete();
        }

        private void OnDestroy()
        {
            if (crateLoot != null)
            {
                crateLoot.OnLooted -= OnCrateLooted;
            }
        }
    }
}

