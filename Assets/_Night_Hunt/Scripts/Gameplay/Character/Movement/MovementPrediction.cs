using UnityEngine;
using NightHunt.Gameplay.Core.Prediction;
using NightHunt.Gameplay.Input;

namespace NightHunt.Gameplay.Character.Movement
{
    /// <summary>
    /// Client-side movement prediction logic
    /// </summary>
    public class MovementPrediction
    {
        private readonly PredictionManager<MovementState> predictionManager;
        private readonly CharacterMovement characterMovement;
        private readonly InputPrediction inputPrediction;

        public MovementPrediction(CharacterMovement movement, InputPrediction input)
        {
            characterMovement = movement;
            inputPrediction = input;
            
            // Create prediction manager if movement implements IPredictable
            if (movement is IPredictable<MovementState> predictable)
            {
                predictionManager = new PredictionManager<MovementState>(predictable);
            }
        }

        /// <summary>
        /// Predict movement based on input
        /// </summary>
        public void Predict(Vector2 moveInput, bool isSprinting, bool isCrouching)
        {
            if (predictionManager != null)
            {
                predictionManager.Predict();
            }
        }

        /// <summary>
        /// Reconcile with server state
        /// </summary>
        public void Reconcile(MovementState serverState, int serverTick)
        {
            if (predictionManager != null)
            {
                predictionManager.Reconcile(serverState, serverTick);
            }
        }

        /// <summary>
        /// Get prediction manager
        /// </summary>
        public PredictionManager<MovementState> GetPredictionManager() => predictionManager;
    }
}

