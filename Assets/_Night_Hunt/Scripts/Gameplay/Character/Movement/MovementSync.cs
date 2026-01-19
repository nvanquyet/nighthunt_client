using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Gameplay.Core.Networking;

namespace NightHunt.Gameplay.Character.Movement
{
    /// <summary>
    /// Network sync component for movement
    /// Syncs position, rotation, and stamina from server to clients
    /// </summary>
    public class MovementSync : NetworkBehaviour
    {
        private readonly SyncVar<Vector3> networkPosition = new SyncVar<Vector3>();
        private readonly SyncVar<Quaternion> networkRotation = new SyncVar<Quaternion>();
        private readonly SyncVar<float> networkStamina = new SyncVar<float>();

        private CharacterMovement characterMovement;
        private MovementPrediction movementPrediction;

        // ✅ Track initialization state
        private bool isInitialized = false;

        private void Awake()
        {
            characterMovement = GetComponent<CharacterMovement>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Subscribe to SyncVar changes
            networkPosition.OnChange += OnPositionChanged;
            networkRotation.OnChange += OnRotationChanged;
            networkStamina.OnChange += OnStaminaChanged;

            // ✅ Mark as initialized
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

            // ✅ Mark as not initialized
            isInitialized = false;
        }

        private void Update()
        {
            // ✅ CRITICAL FIX: Check initialization BEFORE accessing NetworkBehaviour properties
            if (!isInitialized) return;
            
            // ✅ SAFE: Now we can check IsServerStarted
            if (!IsServerStarted) return;

            // Server: Sync position, rotation, and stamina
            if (characterMovement != null)
            {
                networkPosition.Value = transform.position;
                networkRotation.Value = transform.rotation;
                networkStamina.Value = characterMovement.GetCurrentStamina();
            }
        }

        /// <summary>
        /// Client: Receive server position for reconciliation
        /// </summary>
        private void OnPositionChanged(Vector3 oldPos, Vector3 newPos, bool asServer)
        {
            // Only clients reconcile (server is authoritative)
            if (!asServer && characterMovement != null && movementPrediction != null)
            {
                // Create server state for reconciliation
                var serverState = new MovementState
                {
                    Position = newPos,
                    Rotation = networkRotation.Value,
                    Stamina = networkStamina.Value
                };
                
                // Reconcile client prediction with server state
                // TODO: Get actual server tick from FishNet's TimeManager
                movementPrediction.Reconcile(serverState, 0);
            }
        }

        /// <summary>
        /// Client: Receive server rotation
        /// </summary>
        private void OnRotationChanged(Quaternion oldRot, Quaternion newRot, bool asServer)
        {
            if (!asServer && characterMovement != null)
            {
                // Apply server rotation directly
                transform.rotation = newRot;
            }
        }

        /// <summary>
        /// Client: Receive server stamina
        /// </summary>
        private void OnStaminaChanged(float oldStamina, float newStamina, bool asServer)
        {
            if (!asServer && characterMovement != null)
            {
                // Update client stamina from server
                characterMovement.SetStamina(newStamina);
            }
        }

        /// <summary>
        /// Set movement prediction component
        /// </summary>
        public void SetMovementPrediction(MovementPrediction prediction)
        {
            movementPrediction = prediction;
        }
    }
}