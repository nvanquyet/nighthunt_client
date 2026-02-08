using UnityEngine;
using NightHunt.Gameplay.AI;
using NightHunt.Gameplay.Character;
using NightHunt.Inventory.Stats;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Boss fight objective
    /// </summary>
    public class BossObjective : MonoBehaviour, IObjective
    {
        [Header("Boss Objective Settings")]
        [SerializeField] private string objectiveId = "BOSS_OBJECTIVE";
        [SerializeField] private string objectiveName = "Defeat Boss";
        [SerializeField] private BossAI bossAI;

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted { get; private set; }
        public float Progress { get; private set; }

        private void Awake()
        {
            if (bossAI == null)
            {
                bossAI = GetComponent<BossAI>();
            }
        }

        public void OnStart()
        {
            IsCompleted = false;
            Progress = 0f;
            
            if (bossAI != null)
            {
                bossAI.OnBossDefeated += OnBossDefeated;
            }
        }

        public void OnUpdate()
        {
            if (bossAI != null && !IsCompleted)
            {
                // Calculate progress based on boss HP
                var bossStats = bossAI.GetComponent<CharacterStats>();
                if (bossStats != null)
                {
                    float maxHP = bossStats.GetMaxHP();
                    float currentHP = bossStats.GetCurrentHP();
                    Progress = 1f - (currentHP / maxHP);
                }
            }
        }

        public void OnComplete()
        {
            IsCompleted = true;
            Progress = 1f;
            Debug.Log($"[BossObjective] Objective completed: {objectiveName}");
        }

        public void OnFail()
        {
            // Boss objective doesn't fail, only completes
        }

        private void OnBossDefeated()
        {
            OnComplete();
        }

        private void OnDestroy()
        {
            if (bossAI != null)
            {
                bossAI.OnBossDefeated -= OnBossDefeated;
            }
        }
    }
}

