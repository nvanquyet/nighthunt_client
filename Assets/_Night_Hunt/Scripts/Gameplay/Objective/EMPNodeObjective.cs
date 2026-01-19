using UnityEngine;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// EMP node objective
    /// </summary>
    public class EMPNodeObjective : MonoBehaviour, IObjective
    {
        [Header("EMP Node Settings")]
        [SerializeField] private string objectiveId = "EMP_NODE";
        [SerializeField] private string objectiveName = "Destroy EMP Node";
        [SerializeField] private float health = 100f;
        [SerializeField] private float maxHealth = 100f;

        public string ObjectiveId => objectiveId;
        public string ObjectiveName => objectiveName;
        public bool IsCompleted { get; private set; }
        public float Progress { get; private set; }

        public void OnStart()
        {
            IsCompleted = false;
            health = maxHealth;
            Progress = 0f;
        }

        public void OnUpdate()
        {
            if (!IsCompleted)
            {
                Progress = 1f - (health / maxHealth);
                
                if (health <= 0f)
                {
                    OnComplete();
                }
            }
        }

        public void OnComplete()
        {
            IsCompleted = true;
            Progress = 1f;
            Debug.Log($"[EMPNodeObjective] EMP node destroyed: {objectiveName}");
        }

        public void OnFail()
        {
            // EMP node doesn't fail
        }

        /// <summary>
        /// Take damage
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (!IsCompleted)
            {
                health = Mathf.Max(0f, health - damage);
            }
        }
    }
}

