using System.Collections.Generic;
using UnityEngine;
using NightHunt.Gameplay.Inventory.Events;

namespace NightHunt.Gameplay.Inventory.Logic.Prediction
{
    /// <summary>
    /// Client-side prediction system for inventory UI
    /// Stores pending operations and provides rollback capability
    /// Ensures UI updates only after server confirmation
    /// </summary>
    public class InventoryUIPrediction : MonoBehaviour
    {
        /// <summary>
        /// Pending operation waiting for server confirmation
        /// </summary>
        public class PendingOperation
        {
            public string OperationId { get; set; }
            public OperationType Type { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
            public System.Action RollbackAction { get; set; }
            public float Timestamp { get; set; }
            public bool IsConfirmed { get; set; }

            public PendingOperation()
            {
                Parameters = new Dictionary<string, object>();
                Timestamp = Time.time;
                IsConfirmed = false;
            }
        }

        public enum OperationType
        {
            MoveItem,
            SwapItems,
            RemoveItem,
            EquipItem,
            UnequipItem,
            AssignQuickSlot,
            ClearQuickSlot,
            UseItem
        }

        private Dictionary<string, PendingOperation> pendingOperations = new Dictionary<string, PendingOperation>();
        private const float OPERATION_TIMEOUT = 5f; // 5 seconds timeout

        private void Update()
        {
            // Cleanup timed-out operations
            CleanupTimedOutOperations();
        }

        /// <summary>
        /// Register a pending operation (client-side prediction)
        /// </summary>
        public string RegisterPendingOperation(OperationType type, Dictionary<string, object> parameters, System.Action rollbackAction)
        {
            string operationId = GenerateOperationId();
            var operation = new PendingOperation
            {
                OperationId = operationId,
                Type = type,
                Parameters = parameters,
                RollbackAction = rollbackAction,
                Timestamp = Time.time
            };

            pendingOperations[operationId] = operation;
            Debug.Log($"[InventoryUIPrediction] Registered pending operation: {operationId}, Type: {type}");

            return operationId;
        }

        /// <summary>
        /// Confirm operation (server accepted)
        /// </summary>
        public void ConfirmOperation(string operationId)
        {
            if (pendingOperations.TryGetValue(operationId, out var operation))
            {
                operation.IsConfirmed = true;
                pendingOperations.Remove(operationId);
                Debug.Log($"[InventoryUIPrediction] Confirmed operation: {operationId}");
            }
        }

        /// <summary>
        /// Reject operation (server rejected) - rollback
        /// </summary>
        public void RejectOperation(string operationId)
        {
            if (pendingOperations.TryGetValue(operationId, out var operation))
            {
                Debug.LogWarning($"[InventoryUIPrediction] Rejecting operation: {operationId}, Type: {operation.Type}");
                
                // Execute rollback
                if (operation.RollbackAction != null)
                {
                    operation.RollbackAction.Invoke();
                }

                pendingOperations.Remove(operationId);
            }
        }

        /// <summary>
        /// Find pending operation by parameters (for matching server response)
        /// </summary>
        public PendingOperation FindPendingOperation(OperationType type, Dictionary<string, object> parameters)
        {
            foreach (var kvp in pendingOperations)
            {
                var op = kvp.Value;
                if (op.Type == type && !op.IsConfirmed)
                {
                    // Check if parameters match
                    bool matches = true;
                    foreach (var param in parameters)
                    {
                        if (!op.Parameters.ContainsKey(param.Key) || 
                            !op.Parameters[param.Key].Equals(param.Value))
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        return op;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Cleanup timed-out operations (assume rejected)
        /// </summary>
        private void CleanupTimedOutOperations()
        {
            var timedOut = new List<string>();

            foreach (var kvp in pendingOperations)
            {
                var operation = kvp.Value;
                if (Time.time - operation.Timestamp > OPERATION_TIMEOUT && !operation.IsConfirmed)
                {
                    timedOut.Add(kvp.Key);
                }
            }

            foreach (var id in timedOut)
            {
                Debug.LogWarning($"[InventoryUIPrediction] Operation timed out: {id}");
                RejectOperation(id);
            }
        }

        /// <summary>
        /// Generate unique operation ID
        /// </summary>
        private string GenerateOperationId()
        {
            return System.Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Clear all pending operations (e.g., on disconnect)
        /// </summary>
        public void ClearAllOperations()
        {
            foreach (var kvp in pendingOperations)
            {
                if (kvp.Value.RollbackAction != null)
                {
                    kvp.Value.RollbackAction.Invoke();
                }
            }

            pendingOperations.Clear();
        }
    }
}
