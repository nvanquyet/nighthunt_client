using UnityEngine;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Utilities;
using System.Collections.Generic;

namespace NightHunt.Inventory.Domain.QuickSlot
{
    /// <summary>
    /// Manages cooldown per quick slot.
    /// Separate from use duration (consumption time).
    /// Cooldown is the time between uses, use duration is the time to consume.
    /// </summary>
    public class QuickSlotCooldownManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float defaultCooldown = 5f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Dictionary<int, float> cooldownEndTimes = new Dictionary<int, float>();
        private Dictionary<int, float> customCooldowns = new Dictionary<int, float>();
        
        #region Lifecycle
        
        void Update()
        {
            // Check and expire cooldowns
            var expiredSlots = new List<int>();
            foreach (var kvp in cooldownEndTimes)
            {
                if (Time.time >= kvp.Value)
                {
                    expiredSlots.Add(kvp.Key);
                }
            }
            
            foreach (var slotIndex in expiredSlots)
            {
                EndCooldown(slotIndex);
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Starts cooldown for a slot.
        /// </summary>
        /// <param name="slotIndex">The slot index (0-3)</param>
        /// <param name="cooldownDuration">Custom cooldown duration. If 0, uses default.</param>
        public void StartCooldown(int slotIndex, float cooldownDuration = 0f)
        {
            float duration = cooldownDuration > 0f ? cooldownDuration : defaultCooldown;
            float endTime = Time.time + duration;
            
            cooldownEndTimes[slotIndex] = endTime;
            customCooldowns[slotIndex] = duration;
            
            QuickSlotEvents.InvokeCooldownStarted(slotIndex);
            
            InventoryLogger.Log("QuickSlotCooldownManager", $"Started cooldown for slot {slotIndex} ({duration}s)", enableDebugLogs);
        }
        
        /// <summary>
        /// Checks if a slot is on cooldown.
        /// </summary>
        /// <param name="slotIndex">The slot index</param>
        /// <returns>True if on cooldown</returns>
        public bool IsOnCooldown(int slotIndex)
        {
            if (!cooldownEndTimes.ContainsKey(slotIndex))
                return false;
            
            return Time.time < cooldownEndTimes[slotIndex];
        }
        
        /// <summary>
        /// Gets remaining cooldown time for a slot.
        /// </summary>
        /// <param name="slotIndex">The slot index</param>
        /// <returns>Remaining time in seconds, or 0 if not on cooldown</returns>
        public float GetRemainingCooldown(int slotIndex)
        {
            if (!cooldownEndTimes.ContainsKey(slotIndex))
                return 0f;
            
            float remaining = cooldownEndTimes[slotIndex] - Time.time;
            return remaining > 0f ? remaining : 0f;
        }
        
        /// <summary>
        /// Gets cooldown progress (0-1) for a slot.
        /// </summary>
        /// <param name="slotIndex">The slot index</param>
        /// <returns>Progress from 0 (just started) to 1 (completed)</returns>
        public float GetCooldownProgress(int slotIndex)
        {
            if (!cooldownEndTimes.ContainsKey(slotIndex) || !customCooldowns.ContainsKey(slotIndex))
                return 1f;
            
            float elapsed = customCooldowns[slotIndex] - GetRemainingCooldown(slotIndex);
            return Mathf.Clamp01(elapsed / customCooldowns[slotIndex]);
        }
        
        /// <summary>
        /// Clears cooldown for a slot.
        /// </summary>
        /// <param name="slotIndex">The slot index</param>
        public void ClearCooldown(int slotIndex)
        {
            if (cooldownEndTimes.ContainsKey(slotIndex))
            {
                cooldownEndTimes.Remove(slotIndex);
                customCooldowns.Remove(slotIndex);
                QuickSlotEvents.InvokeCooldownEnded(slotIndex);
            }
        }
        
        /// <summary>
        /// Clears all cooldowns.
        /// </summary>
        public void ClearAllCooldowns()
        {
            foreach (var slotIndex in cooldownEndTimes.Keys)
            {
                QuickSlotEvents.InvokeCooldownEnded(slotIndex);
            }
            
            cooldownEndTimes.Clear();
            customCooldowns.Clear();
        }
        
        #endregion
        
        #region Private Methods
        
        private void EndCooldown(int slotIndex)
        {
            cooldownEndTimes.Remove(slotIndex);
            customCooldowns.Remove(slotIndex);
            QuickSlotEvents.InvokeCooldownEnded(slotIndex);
            
            InventoryLogger.Log("QuickSlotCooldownManager", $"Cooldown ended for slot {slotIndex}", enableDebugLogs);
        }
        
        #endregion
    }
}
