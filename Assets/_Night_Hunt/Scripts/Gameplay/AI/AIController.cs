using UnityEngine;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.Gameplay.AI
{
    /// <summary>
    /// Base AI controller
    /// </summary>
    public abstract class AIController : MonoBehaviour
    {
        [Header("AI Settings")]
        [SerializeField] protected float detectionRadius = 20f;
        [SerializeField] protected float attackRange = 10f;
        [SerializeField] protected float moveSpeed = 3f;

        protected CharacterStats characterStats;
        protected CharacterPredictedMovement CharacterPredictedMovement;
        protected CharacterCombat characterCombat;
        protected List<NetworkPlayer> detectedPlayers = new List<NetworkPlayer>();

        protected virtual void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            CharacterPredictedMovement = GetComponent<CharacterPredictedMovement>();
            characterCombat = GetComponent<CharacterCombat>();
        }

        protected virtual void Update()
        {
            DetectPlayers();
            UpdateAI();
        }

        /// <summary>
        /// Detect nearby players
        /// </summary>
        protected virtual void DetectPlayers()
        {
            detectedPlayers.Clear();
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= detectionRadius)
                {
                    detectedPlayers.Add(player);
                }
            }
        }

        /// <summary>
        /// Update AI behavior
        /// </summary>
        protected abstract void UpdateAI();

        /// <summary>
        /// Move towards target
        /// </summary>
        protected virtual void MoveTowards(Vector3 target)
        {
            if (CharacterPredictedMovement == null) return;

            Vector3 direction = (target - transform.position).normalized;
            CharacterPredictedMovement.SetMoveInput(new Vector2(direction.x, direction.z));
        }

        /// <summary>
        /// Attack target
        /// </summary>
        protected virtual void AttackTarget(NetworkPlayer target)
        {
            if (characterCombat == null || target == null) return;

            Vector3 direction = (target.transform.position - transform.position).normalized;
            characterCombat.SetAimDirection(direction);
            characterCombat.SetAttacking(true);
        }
    }
}

