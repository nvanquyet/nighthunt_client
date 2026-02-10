using UnityEngine;

namespace NightHunt.Gameplay.Spawn
{
    /// <summary>
    /// SPAWN POINT - Marks valid spawn locations
    /// 
    /// Features:
    /// - Team-specific or neutral spawns
    /// - Random position offset within radius
    /// - Optional random rotation
    /// - Visual gizmos for level design
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        #region Serialized Fields
        
        [Header("Team Settings")]
        [Tooltip("Team ID (-1 = neutral, available to all teams)")]
        [SerializeField] private int _teamId = 0;
        
        [Header("Randomization")]
        [Tooltip("Random offset radius from spawn point center")]
        [SerializeField] private float _spawnRadius = 1f;
        
        [Tooltip("Randomize player rotation on spawn")]
        [SerializeField] private bool _randomizeRotation = false;
        
        [Header("Visualization")]
        [SerializeField] private Color _gizmoColor = Color.green;
        [SerializeField] private float _gizmoSize = 1f;
        [SerializeField] private bool _showTeamLabel = true;
        
        #endregion
        
        #region Properties
        
        public int TeamId => _teamId;
        public float SpawnRadius => _spawnRadius;
        
        #endregion
        
        #region Spawn Position/Rotation
        
        /// <summary>
        /// Get randomized spawn position within radius
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            if (_spawnRadius <= 0f)
            {
                return transform.position;
            }
            
            // Random position within circle on XZ plane
            Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
            Vector3 offset = new Vector3(randomCircle.x, 0f, randomCircle.y);
            
            return transform.position + offset;
        }
        
        /// <summary>
        /// Get spawn rotation (randomized if enabled)
        /// </summary>
        public Quaternion GetSpawnRotation()
        {
            if (_randomizeRotation)
            {
                return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            
            return transform.rotation;
        }
        
        #endregion
        
        #region Team Validation
        
        /// <summary>
        /// Check if this spawn point is valid for a specific team
        /// </summary>
        public bool IsForTeam(int teamId)
        {
            // Neutral spawns (-1) are valid for all teams
            return _teamId == -1 || _teamId == teamId;
        }
        
        /// <summary>
        /// Set team ID at runtime
        /// </summary>
        public void SetTeamId(int teamId)
        {
            _teamId = teamId;
        }
        
        #endregion
        
        #region Gizmos (Editor Visualization)
        
        private void OnDrawGizmos()
        {
            // Draw spawn point marker
            Gizmos.color = _gizmoColor;
            
            // Draw position sphere
            Gizmos.DrawWireSphere(transform.position, _gizmoSize);
            
            // Draw spawn radius area
            if (_spawnRadius > 0f)
            {
                Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.3f);
                Gizmos.DrawWireSphere(transform.position, _spawnRadius);
            }
            
            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw detailed gizmo when selected
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _gizmoSize * 1.5f);
            
            // Draw spawn radius with more detail
            if (_spawnRadius > 0f)
            {
                Gizmos.color = Color.cyan;
                
                // Draw circle outline
                int segments = 32;
                float angleStep = 360f / segments;
                
                for (int i = 0; i < segments; i++)
                {
                    float angle1 = i * angleStep * Mathf.Deg2Rad;
                    float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                    
                    Vector3 p1 = transform.position + new Vector3(
                        Mathf.Cos(angle1) * _spawnRadius,
                        0f,
                        Mathf.Sin(angle1) * _spawnRadius
                    );
                    
                    Vector3 p2 = transform.position + new Vector3(
                        Mathf.Cos(angle2) * _spawnRadius,
                        0f,
                        Mathf.Sin(angle2) * _spawnRadius
                    );
                    
                    Gizmos.DrawLine(p1, p2);
                }
            }
            
            // Draw team label
#if UNITY_EDITOR
            if (_showTeamLabel)
            {
                string label = _teamId == -1 ? "Neutral Spawn" : $"Team {_teamId} Spawn";
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    label,
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.white },
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    }
                );
            }
#endif
        }
        
        #endregion
        
        #region Validation
        
        private void OnValidate()
        {
            // Clamp values
            _spawnRadius = Mathf.Max(0f, _spawnRadius);
            _gizmoSize = Mathf.Max(0.1f, _gizmoSize);
            
            // Update gizmo color based on team
            UpdateGizmoColor();
        }
        
        private void UpdateGizmoColor()
        {
            // Auto-assign colors based on team ID
            switch (_teamId)
            {
                case -1: // Neutral
                    _gizmoColor = Color.white;
                    break;
                case 0: // Team 0
                    _gizmoColor = Color.blue;
                    break;
                case 1: // Team 1
                    _gizmoColor = Color.red;
                    break;
                case 2: // Team 2
                    _gizmoColor = Color.green;
                    break;
                case 3: // Team 3
                    _gizmoColor = Color.yellow;
                    break;
                default:
                    _gizmoColor = Color.magenta;
                    break;
            }
        }
        
        #endregion
    }
}