using UnityEngine;

namespace NightHunt.Gameplay.Spawn
{
    /// <summary>
    /// Spawn point for players
    /// Can be team-specific or neutral
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private int teamId = 0; // -1 = neutral spawn for all teams
        [SerializeField] private float spawnRadius = 1f; // Random offset within radius
        [SerializeField] private bool randomizeRotation = false;

        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = Color.green;
        [SerializeField] private float gizmoSize = 1f;

        public int TeamId => teamId;

        /// <summary>
        /// Get spawn position (with optional random offset)
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            if (spawnRadius <= 0f)
            {
                return transform.position;
            }

            // Random position within circle
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 offset = new Vector3(randomCircle.x, 0f, randomCircle.y);
            
            return transform.position + offset;
        }

        /// <summary>
        /// Get spawn rotation
        /// </summary>
        public Quaternion GetSpawnRotation()
        {
            if (randomizeRotation)
            {
                return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            return transform.rotation;
        }

        /// <summary>
        /// Set team ID
        /// </summary>
        public void SetTeamId(int team)
        {
            teamId = team;
        }

        /// <summary>
        /// Check if this spawn point is for a specific team
        /// </summary>
        public bool IsForTeam(int team)
        {
            return teamId == -1 || teamId == team;
        }

        private void OnDrawGizmos()
        {
            // Draw spawn point visualization
            Gizmos.color = gizmoColor;
            
            // Draw position sphere
            Gizmos.DrawWireSphere(transform.position, gizmoSize);
            
            // Draw spawn radius
            if (spawnRadius > 0f)
            {
                Gizmos.DrawWireSphere(transform.position, spawnRadius);
            }
            
            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw detailed gizmo when selected
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, gizmoSize * 1.5f);
            
            // Draw team info
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f, 
                $"Spawn Point\nTeam: {(teamId == -1 ? "Neutral" : teamId.ToString())}"
            );
            #endif
        }
    }
}