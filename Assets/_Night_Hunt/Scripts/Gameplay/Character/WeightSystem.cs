using UnityEngine;

namespace _Night_Hunt.Scripts.Gameplay.Character
{
   /// <summary>
    /// Weight system - applies penalties based on inventory weight
    /// </summary>
    public class WeightSystem : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats;
        [SerializeField] private Inventory.InventorySystem inventory;
        
        private float lastWeight = 0f;
        private string currentWeightModifierId = null;

        private void Start()
        {
            if (stats == null)
                stats = GetComponent<CharacterStats>();
            
            if (inventory == null)
                inventory = GetComponent<Inventory.InventorySystem>();
            
            if (inventory != null)
            {
                //inventory.OnWeightChanged += OnInventoryWeightChanged;
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                //inventory.OnWeightChanged -= OnInventoryWeightChanged;
            }
        }

        private void OnInventoryWeightChanged(float currentWeight, float maxCapacity)
        {
            if (Mathf.Approximately(currentWeight, lastWeight))
                return;
            
            lastWeight = currentWeight;
            UpdateWeightModifiers(currentWeight, maxCapacity);
        }

        private void UpdateWeightModifiers(float weight, float capacity)
        {
            // Remove old modifier
            if (currentWeightModifierId != null)
            {
                stats.RemoveModifier(currentWeightModifierId);
            }
            
            // Calculate weight percentage
            float weightPercent = weight / capacity;
            
            // Apply penalties based on weight thresholds (from config)
            if (weightPercent <= 0.8f)
            {
                // No penalty
                currentWeightModifierId = null;
            }
            else if (weightPercent <= 1.0f)
            {
                // 80-100%: -10% speed
                currentWeightModifierId = "WEIGHT_HEAVY";
                var mod = new StatModifier(currentWeightModifierId, StatType.MoveSpeed, ModifierOperation.Multiply, 0.9f, false);
                mod.source = "Weight";
                stats.AddModifier(mod);
            }
            else if (weightPercent <= 1.4f)
            {
                // 100-140%: -20% speed, +25% stamina drain
                currentWeightModifierId = "WEIGHT_OVERLOAD";
                var speedMod = new StatModifier(currentWeightModifierId + "_SPEED", StatType.MoveSpeed, ModifierOperation.Multiply, 0.8f, false);
                speedMod.source = "Weight";
                stats.AddModifier(speedMod);
                
                // TODO: Add stamina drain modifier
            }
            else
            {
                // >140%: -30% speed, cannot sprint
                currentWeightModifierId = "WEIGHT_CRITICAL";
                var speedMod = new StatModifier(currentWeightModifierId + "_SPEED", StatType.MoveSpeed, ModifierOperation.Multiply, 0.7f, false);
                speedMod.source = "Weight";
                stats.AddModifier(speedMod);
            }
        }

        public float GetCurrentWeightPercent() => lastWeight / stats.GetWeightCapacity();
        public bool CanSprint() => GetCurrentWeightPercent() <= 1.0f;
    }
}