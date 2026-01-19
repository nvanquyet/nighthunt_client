using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Gameplay.Core.Networking;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Character.Movement
{
    /// <summary>
    /// Network sync component for movement
    /// 
    /// SYNC FLOW:
    /// 1. SERVER: Update SyncVars every frame with current state
    /// 2. ALL CLIENTS: Receive SyncVar changes via OnChange callbacks
    /// 3. CLIENT OWNER: Reconcile prediction with server state
    /// 4. REMOTE CLIENTS: Apply server state directly (with optional interpolation)
    /// </summary>
    public class MovementSync : NetworkBehaviour
    {
        [Header("Sync Settings")]
        [SerializeField] private float reconciliationThreshold = 0.5f; // Distance threshold for reconciliation
        [SerializeField] private bool useInterpolation = true; // Smooth movement for remote clients
        [SerializeField] private float interpolationSpeed = 10f;

        // ✅ SyncVars - Automatically replicate from server to all clients
        private readonly SyncVar<Vector3> networkPosition = new SyncVar<Vector3>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));
        private readonly SyncVar<Quaternion> networkRotation = new SyncVar<Quaternion>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));
        private readonly SyncVar<float> networkStamina = new SyncVar<float>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));

        private CharacterMovement characterMovement;
        private MovementPrediction movementPrediction;
        private NetworkPlayer networkPlayer;

        // Interpolation state for remote clients
        private Vector3 targetPosition;
        private Quaternion targetRotation;

        private bool isInitialized = false;

        private void Awake()
        {
            characterMovement = GetComponent<CharacterMovement>();
            networkPlayer = GetComponent<NetworkPlayer>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Subscribe to SyncVar changes
            networkPosition.OnChange += OnPositionChanged;
            networkRotation.OnChange += OnRotationChanged;
            networkStamina.OnChange += OnStaminaChanged;

            // Initialize interpolation targets
            targetPosition = transform.position;
            targetRotation = transform.rotation;

            isInitialized = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            // Unsubscribe from SyncVar changes
            if (networkPosition != null)
                networkPosition.OnChange -= OnPositionChanged;
            if (networkRotation != null)
                networkRotation.OnChange -= OnRotationChanged;
            if (networkStamina != null)
                networkStamina.OnChange -= OnStaminaChanged;

            isInitialized = false;
        }

        private void Update()
        {
            if (!isInitialized) return;
            
            // ✅ SERVER: Broadcast current state to all clients
            if (IsServerStarted)
            {
                BroadcastState();
            }
            // ✅ REMOTE CLIENTS: Interpolate to server state
            else if (networkPlayer != null && !networkPlayer.IsOwner)
            {
                InterpolateToServerState();
            }
        }

        /// <summary>
        /// SERVER: Broadcast current state to all clients via SyncVars
        /// </summary>
        private void BroadcastState()
        {
            if (characterMovement == null) return;

            // Update SyncVars - FishNet automatically replicates to all clients
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
            networkStamina.Value = characterMovement.GetCurrentStamina();
        }

        /// <summary>
        /// REMOTE CLIENTS: Smooth interpolation to server state
        /// </summary>
        private void InterpolateToServerState()
        {
            if (!useInterpolation)
            {
                // Snap to server state
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                return;
            }

            // Smooth interpolation
            transform.position = Vector3.Lerp(transform.position, targetPosition, interpolationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, interpolationSpeed * Time.deltaTime);
        }

        #region SyncVar Callbacks

        /// <summary>
        /// Called when server updates position
        /// - Client Owner: Reconcile with prediction
        /// - Remote Clients: Update target position for interpolation
        /// </summary>
        private void OnPositionChanged(Vector3 oldPos, Vector3 newPos, bool asServer)
        {
            // Server doesn't need to process its own changes
            if (asServer) return;

            bool isOwner = networkPlayer != null && networkPlayer.IsOwner;

            if (isOwner)
            {
                // ✅ CLIENT OWNER: Reconcile prediction with server state
                ReconcilePosition(newPos);
            }
            else
            {
                // ✅ REMOTE CLIENT: Set target for interpolation
                targetPosition = newPos;
                
                if (!useInterpolation)
                {
                    transform.position = newPos;
                }
            }
        }

        /// <summary>
        /// Called when server updates rotation
        /// </summary>
        private void OnRotationChanged(Quaternion oldRot, Quaternion newRot, bool asServer)
        {
            if (asServer) return;

            bool isOwner = networkPlayer != null && networkPlayer.IsOwner;

            if (isOwner)
            {
                // Owner can reconcile rotation if needed
                float angle = Quaternion.Angle(transform.rotation, newRot);
                if (angle > 10f) // Threshold for rotation reconciliation
                {
                    transform.rotation = newRot;
                }
            }
            else
            {
                // Remote client: Set target rotation
                targetRotation = newRot;
                
                if (!useInterpolation)
                {
                    transform.rotation = newRot;
                }
            }
        }

        /// <summary>
        /// Called when server updates stamina
        /// </summary>
        private void OnStaminaChanged(float oldStamina, float newStamina, bool asServer)
        {
            if (asServer) return;

            // All clients update stamina from server
            if (characterMovement != null)
            {
                characterMovement.SetStamina(newStamina);
            }
        }

        #endregion

        #region Reconciliation

        /// <summary>
        /// CLIENT OWNER: Reconcile predicted position with server state
        /// </summary>
        private void ReconcilePosition(Vector3 serverPosition)
        {
            float distance = Vector3.Distance(transform.position, serverPosition);

            // ✅ Only reconcile if difference exceeds threshold
            if (distance > reconciliationThreshold)
            {
                Debug.LogWarning($"[MovementSync] Reconciliation needed! Client: {transform.position}, Server: {serverPosition}, Distance: {distance:F3}");

                if (movementPrediction != null)
                {
                    // Create server state for prediction reconciliation
                    var serverState = new MovementState
                    {
                        Position = serverPosition,
                        Rotation = networkRotation.Value,
                        Stamina = networkStamina.Value
                    };

                    // Use FishNet's TimeManager for accurate tick
                    int serverTick = (int) (base.TimeManager != null ? base.TimeManager.Tick : 0);
                    movementPrediction.Reconcile(serverState, serverTick);
                }
                else
                {
                    // Fallback: Snap to server position
                    transform.position = serverPosition;
                }
            }
        }

        #endregion

        /// <summary>
        /// Set movement prediction component
        /// </summary>
        public void SetMovementPrediction(MovementPrediction prediction)
        {
            movementPrediction = prediction;
        }

        /// <summary>
        /// Get reconciliation threshold
        /// </summary>
        public float GetReconciliationThreshold() => reconciliationThreshold;

        /// <summary>
        /// Set reconciliation threshold
        /// </summary>
        public void SetReconciliationThreshold(float threshold)
        {
            reconciliationThreshold = Mathf.Max(0.01f, threshold);
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !isInitialized) return;

            // Draw reconciliation threshold
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, reconciliationThreshold);

            // Show server position for client owner
            if (networkPlayer != null && networkPlayer.IsOwner && !networkPlayer.IsServerInitialized)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(networkPosition.Value, 0.3f);
                Gizmos.DrawLine(transform.position, networkPosition.Value);
            }
        }
        #endif
    }
}