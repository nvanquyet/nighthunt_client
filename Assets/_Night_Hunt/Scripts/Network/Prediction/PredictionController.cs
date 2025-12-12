using UnityEngine;

namespace _Night_Hunt.Scripts.Network.Prediction
{
    /// <summary>
    /// Client-Side Prediction Controller
    /// Implements prediction + reconciliation for smooth gameplay
    /// </summary>
    public class PredictionController : MonoBehaviour
    {
        private const int BUFFER_SIZE = 1024;
        
        // Circular buffers for prediction
        private InputState[] inputBuffer = new InputState[BUFFER_SIZE];
        private TransformState[] stateBuffer = new TransformState[BUFFER_SIZE];
        
        private uint currentTick = 0;
        private uint lastServerTick = 0;
        
        [SerializeField] private float reconciliationThreshold = 0.5f;

        public struct InputState
        {
            public uint tick;
            public Vector3 moveInput;
            public Vector3 lookDirection;
            public bool isSprinting;
            public bool isCrouching;
            public bool isFiring;
        }

        public struct TransformState
        {
            public uint tick;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
        }

        /// <summary>
        /// Record input for prediction
        /// </summary>
        public void RecordInput(InputState input)
        {
            input.tick = currentTick;
            inputBuffer[currentTick % BUFFER_SIZE] = input;
            currentTick++;
        }

        /// <summary>
        /// Record predicted state
        /// </summary>
        public void RecordState(TransformState state)
        {
            state.tick = currentTick - 1;
            stateBuffer[state.tick % BUFFER_SIZE] = state;
        }

        /// <summary>
        /// Server sends authoritative state - reconcile if needed
        /// </summary>
        public bool ReconcileState(TransformState serverState)
        {
            lastServerTick = serverState.tick;
            
            // Get our predicted state at that tick
            TransformState predictedState = stateBuffer[serverState.tick % BUFFER_SIZE];
            
            // Calculate error
            float posError = Vector3.Distance(serverState.position, predictedState.position);
            float rotError = Quaternion.Angle(serverState.rotation, predictedState.rotation);
            
            // If error is too large, reconcile
            if (posError > reconciliationThreshold || rotError > 5f)
            {
                Debug.LogWarning($"[Prediction] Reconciling - Pos error: {posError:F3}m, Rot error: {rotError:F1}°");
                
                // Snap to server state
                transform.position = serverState.position;
                transform.rotation = serverState.rotation;
                
                // Re-simulate from server tick to current tick
                ReplayInputs(serverState.tick, currentTick);
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Replay inputs from oldTick to newTick for reconciliation
        /// </summary>
        private void ReplayInputs(uint fromTick, uint toTick)
        {
            for (uint tick = fromTick + 1; tick < toTick; tick++)
            {
                InputState input = inputBuffer[tick % BUFFER_SIZE];
                
                // Re-apply physics simulation with this input
                SimulateMovement(input);
                
                // Record new predicted state
                TransformState newState = new TransformState
                {
                    tick = tick,
                    position = transform.position,
                    rotation = transform.rotation,
                    velocity = Vector3.zero // TODO: get from rigidbody
                };
                
                stateBuffer[tick % BUFFER_SIZE] = newState;
            }
        }

        private void SimulateMovement(InputState input)
        {
            // TODO: Apply movement logic here
            // This should match server-side movement exactly
        }

        public uint GetCurrentTick() => currentTick;
        public uint GetLastServerTick() => lastServerTick;
    }
}