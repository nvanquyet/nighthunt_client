using UnityEngine;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Input prediction for network
    /// Stores input commands for prediction and reconciliation
    /// </summary>
    public class InputPrediction
    {
        /// <summary>
        /// Input command for prediction
        /// </summary>
        public struct InputCommand
        {
            public int Tick;
            public Vector2 MoveInput;
            public bool IsSprinting;
            public bool IsCrouching;
            public bool IsAttacking;
            public Vector3 AimDirection;
            public float Timestamp;
        }

        private readonly System.Collections.Generic.Queue<InputCommand> commandBuffer = new System.Collections.Generic.Queue<InputCommand>();
        private int currentTick = 0;
        private const int MAX_BUFFER_SIZE = 60;

        /// <summary>
        /// Add input command
        /// </summary>
        public void AddCommand(Vector2 moveInput, bool isSprinting, bool isCrouching, bool isAttacking, Vector3 aimDirection)
        {
            currentTick++;

            var command = new InputCommand
            {
                Tick = currentTick,
                MoveInput = moveInput,
                IsSprinting = isSprinting,
                IsCrouching = isCrouching,
                IsAttacking = isAttacking,
                AimDirection = aimDirection,
                Timestamp = Time.time
            };

            commandBuffer.Enqueue(command);

            // Remove old commands
            while (commandBuffer.Count > MAX_BUFFER_SIZE)
            {
                commandBuffer.Dequeue();
            }
        }

        /// <summary>
        /// Get command at specific tick
        /// </summary>
        public bool TryGetCommand(int tick, out InputCommand command)
        {
            foreach (var cmd in commandBuffer)
            {
                if (cmd.Tick == tick)
                {
                    command = cmd;
                    return true;
                }
            }

            command = default;
            return false;
        }

        /// <summary>
        /// Remove commands up to and including tick
        /// </summary>
        public void RemoveCommandsUpTo(int tick)
        {
            while (commandBuffer.Count > 0 && commandBuffer.Peek().Tick <= tick)
            {
                commandBuffer.Dequeue();
            }
        }

        /// <summary>
        /// Clear all commands
        /// </summary>
        public void Clear()
        {
            commandBuffer.Clear();
            currentTick = 0;
        }

        /// <summary>
        /// Get current tick
        /// </summary>
        public int CurrentTick => currentTick;
    }
}

