using UnityEngine;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Character.Stats;
using NightHunt.Inventory.Stats;

namespace NightHunt.Gameplay.PredatorPrey
{
    /// <summary>
    /// Buff/nerf application based on role (Predator/Prey)
    /// </summary>
    public class RoleBuffSystem
    {
        /// <summary>
        /// Apply predator buffs (leading team)
        /// </summary>
        public static void ApplyPredatorBuffs(CharacterStats stats)
        {
            if (stats == null) return;

            // Slower stamina regen
            //var staminaModifier = new StatModifier("Stamina", ModifierType.Multiply, 0.8f, "PredatorRole");
            // TODO: Apply through modifier stack

            // Louder footsteps (noise increase)
            // noiseModifier = new StatModifier("Noise", ModifierType.Multiply, 1.3f, "PredatorRole");
            // TODO: Apply through modifier stack
        }

        /// <summary>
        /// Apply prey buffs (trailing team)
        /// </summary>
        public static void ApplyPreyBuffs(CharacterStats stats)
        {
            if (stats == null) return;

            // Faster revive
            // TODO: Implement revive speed modifier

            // Less noise
            //var noiseModifier = new StatModifier("Noise", ModifierType.Multiply, 0.8f, "PreyRole");
            // TODO: Apply through modifier stack
        }

        /// <summary>
        /// Remove role buffs
        /// </summary>
        public static void RemoveRoleBuffs(CharacterStats stats)
        {
            if (stats == null) return;
            // TODO: Remove modifiers by source ID
        }
    }
}

