using System;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core.Structs
{
    /// <summary>
    /// Progress tracking for hold-type interactions.
    /// </summary>
    [Serializable]
    public struct InteractionProgress
    {
        /// <summary>
        /// Current hold time (0 to requiredHoldTime).
        /// </summary>
        public float currentHoldTime;

        /// <summary>
        /// Required hold time to complete interaction.
        /// </summary>
        public float requiredHoldTime;

        /// <summary>
        /// Whether the interaction is currently being held.
        /// </summary>
        public bool isHolding;

        /// <summary>
        /// Whether the interaction has been completed.
        /// </summary>
        public bool isCompleted;

        /// <summary>
        /// Create a new interaction progress tracker.
        /// </summary>
        public InteractionProgress(float requiredHoldTime)
        {
            this.currentHoldTime = 0f;
            this.requiredHoldTime = requiredHoldTime;
            this.isHolding = false;
            this.isCompleted = false;
        }

        /// <summary>
        /// Get the progress as a percentage (0-1).
        /// </summary>
        public float GetProgress()
        {
            if (requiredHoldTime <= 0f)
                return isCompleted ? 1f : 0f;

            return Mathf.Clamp01(currentHoldTime / requiredHoldTime);
        }

        /// <summary>
        /// Update the progress with delta time.
        /// </summary>
        public InteractionProgress Update(float deltaTime)
        {
            var progress = this;

            if (progress.isHolding && !progress.isCompleted)
            {
                progress.currentHoldTime += deltaTime;

                if (progress.currentHoldTime >= progress.requiredHoldTime)
                {
                    progress.currentHoldTime = progress.requiredHoldTime;
                    progress.isCompleted = true;
                }
            }

            return progress;
        }

        /// <summary>
        /// Start holding the interaction.
        /// </summary>
        public InteractionProgress StartHolding()
        {
            var progress = this;
            progress.isHolding = true;
            return progress;
        }

        /// <summary>
        /// Stop holding the interaction (cancel if not completed).
        /// </summary>
        public InteractionProgress StopHolding()
        {
            var progress = this;
            progress.isHolding = false;

            if (!progress.isCompleted)
            {
                progress.currentHoldTime = 0f;
            }

            return progress;
        }

        /// <summary>
        /// Reset the progress.
        /// </summary>
        public InteractionProgress Reset()
        {
            var progress = this;
            progress.currentHoldTime = 0f;
            progress.isHolding = false;
            progress.isCompleted = false;
            return progress;
        }
    }
}
