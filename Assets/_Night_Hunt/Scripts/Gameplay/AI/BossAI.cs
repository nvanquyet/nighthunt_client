using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.Gameplay.AI
{
    /// <summary>
    /// AI Boss behavior
    /// Aggressive AI that hunts players
    /// Server-authoritative AI (works with host and dedicated server)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class BossAI : NetworkBehaviour
    {
        [Header("AI Settings")]
        [SerializeField] private float detectionRange = 30f;
        [SerializeField] private float attackRange = 15f;
        [SerializeField] private float attackCooldown = 2f;
        [SerializeField] private float patrolRadius = 50f;

        [Header("Combat")]
        [SerializeField] private int bossHP = 1000;
        [SerializeField] private int bossDamage = 50;
        [SerializeField] private float attackDelay = 1f;

        [Header("Visual")]
        [SerializeField] private GameObject bossModel;
        [SerializeField] private ParticleSystem deathEffect;

        private NavMeshAgent navAgent;
        private CharacterStats characterStats;
        private CharacterCombat characterCombat;
        private Vector3 spawnPosition;
        private NetworkPlayer currentTarget;
        private float lastAttackTime;
        private AIState currentState = AIState.Patrol;

        private enum AIState
        {
            Patrol,
            Chase,
            Attack,
            Dead
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            navAgent = GetComponent<NavMeshAgent>();
            characterStats = GetComponent<CharacterStats>();
            characterCombat = GetComponent<CharacterCombat>();

            if (characterStats == null)
            {
                characterStats = gameObject.AddComponent<CharacterStats>();
            }

            spawnPosition = transform.position;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize boss
            if (characterStats != null)
            {
                characterStats.SetHP(bossHP);
            }

            currentState = AIState.Patrol;
        }

        private void Update()
        {
            if (!IsServer) return;

            if (characterStats != null && !characterStats.IsAlive())
            {
                currentState = AIState.Dead;
                OnDeath();
                return;
            }

            UpdateAI();
        }

        /// <summary>
        /// Update AI behavior
        /// </summary>
        private void UpdateAI()
        {
            switch (currentState)
            {
                case AIState.Patrol:
                    Patrol();
                    CheckForTargets();
                    break;
                case AIState.Chase:
                    ChaseTarget();
                    CheckAttackRange();
                    break;
                case AIState.Attack:
                    AttackTarget();
                    break;
                case AIState.Dead:
                    // Do nothing
                    break;
            }
        }

        /// <summary>
        /// Patrol behavior
        /// </summary>
        private void Patrol()
        {
            if (navAgent == null) return;

            // If not moving, pick new patrol point
            if (!navAgent.pathPending && navAgent.remainingDistance < 0.5f)
            {
                Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
                randomDirection += spawnPosition;
                randomDirection.y = spawnPosition.y;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
                {
                    navAgent.SetDestination(hit.position);
                }
            }
        }

        /// <summary>
        /// Check for nearby targets
        /// </summary>
        private void CheckForTargets()
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            float closestDistance = float.MaxValue;
            NetworkPlayer closestPlayer = null;

            foreach (var player in players)
            {
                if (player == null || !player.IsSpawned) continue;
                
                var playerStats = player.GetComponent<CharacterStats>();
                if (playerStats == null || !playerStats.IsAlive()) continue;

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < detectionRange && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = player;
                }
            }

            if (closestPlayer != null)
            {
                currentTarget = closestPlayer;
                currentState = AIState.Chase;
            }
        }

        /// <summary>
        /// Chase target
        /// </summary>
        private void ChaseTarget()
        {
            if (currentTarget == null || !currentTarget.IsSpawned)
            {
                currentTarget = null;
                currentState = AIState.Patrol;
                return;
            }

            if (navAgent != null)
            {
                navAgent.SetDestination(currentTarget.transform.position);
            }

            // Check if lost target
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distance > detectionRange * 1.5f)
            {
                currentTarget = null;
                currentState = AIState.Patrol;
            }
        }

        /// <summary>
        /// Check if in attack range
        /// </summary>
        private void CheckAttackRange()
        {
            if (currentTarget == null) return;

            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distance <= attackRange)
            {
                currentState = AIState.Attack;
            }
        }

        /// <summary>
        /// Attack target
        /// </summary>
        private void AttackTarget()
        {
            if (currentTarget == null || !currentTarget.IsSpawned)
            {
                currentTarget = null;
                currentState = AIState.Patrol;
                return;
            }

            // Face target
            Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
            direction.y = 0;
            transform.rotation = Quaternion.LookRotation(direction);

            // Attack
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }

            // Check if target moved out of range
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distance > attackRange * 1.2f)
            {
                currentState = AIState.Chase;
            }
        }

        /// <summary>
        /// Perform attack
        /// </summary>
        private void PerformAttack()
        {
            if (currentTarget == null) return;

            // Deal damage
            var targetStats = currentTarget.GetComponent<CharacterStats>();
            if (targetStats != null)
            {
                targetStats.TakeDamage(bossDamage);
            }

            // Visual effect
            RpcPlayAttackEffect();
        }

        /// <summary>
        /// Client: Play attack effect
        /// </summary>
        [ObserversRpc]
        private void RpcPlayAttackEffect()
        {
            // Play attack animation, sound, etc.
        }

        /// <summary>
        /// Handle boss death
        /// </summary>
        private void OnDeath()
        {
            // Award score to killer
            // This would be handled by scoring system

            // Visual effect
            RpcPlayDeathEffect();

            // Despawn after delay
            Invoke(nameof(DespawnBoss), 3f);
        }

        /// <summary>
        /// Server: Despawn boss
        /// </summary>
        [Server]
        private void DespawnBoss()
        {
            if (IsSpawned)
            {
                Despawn();
            }
        }

        /// <summary>
        /// Client: Play death effect
        /// </summary>
        [ObserversRpc]
        private void RpcPlayDeathEffect()
        {
            if (deathEffect != null)
            {
                deathEffect.Play();
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // Draw attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}

