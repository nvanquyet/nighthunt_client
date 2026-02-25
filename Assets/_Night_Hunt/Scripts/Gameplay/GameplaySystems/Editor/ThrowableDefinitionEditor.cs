#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Configs;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Editor;

namespace NightHunt.GameplaySystems.Editor
{
    [CustomEditor(typeof(ThrowableDefinition))]
    public class ThrowableDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Sample Setup", EditorStyles.boldLabel);
            
            var throwable = (ThrowableDefinition)target;
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Frag Grenade")) SetupFragGrenade(throwable);
            if (GUILayout.Button("Smoke Grenade")) SetupSmokeGrenade(throwable);
            if (GUILayout.Button("Flashbang")) SetupFlashbang(throwable);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Incendiary")) SetupIncendiary(throwable);
            if (GUILayout.Button("Gas Grenade")) SetupGasGrenade(throwable);
            if (GUILayout.Button("Impact Grenade")) SetupImpactGrenade(throwable);
            EditorGUILayout.EndHorizontal();
        }
        
        private void SetupFragGrenade(ThrowableDefinition throwable)
        {
            // Stats-only config (Weight)
            var config = ItemStatConfigSetup.GetOrCreateConfig<ThrowableStatConfig>("ThrowableStatConfig_FragGrenade");
            ItemStatConfigSetup.SetupFragGrenade(config);
            throwable.StatConfig = config;

            // Base fields
            throwable.DisplayName = "Frag Grenade";
            throwable.Description = "Explosive grenade – 3 s fuse. Deals AoE damage.";
            throwable.IsStackable = true;
            throwable.MaxStackSize = 3;
            throwable.Weight = config.GetStatValue(ItemStatType.Weight);
            
            // Usage System fields (QUAN TRỌNG cho QuickSlot)
            throwable.ValidSlots = new SlotLocationType[] { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            throwable.CanUseWhileMoving = true;
            throwable.CanCancelUsage = true;
            throwable.UsageDuration = 0f; // Không có progress bar, chờ Fire button
            
            // Throw Settings
            throwable.ThrowForce = 15f;
            throwable.MaxRange = 30f;
            throwable.PrepareTime = 0.5f;
            
            // Projectile Behaviour
            throwable.ThrowableType = ThrowableType.Grenade;
            throwable.Damage = 100f;
            throwable.ExplosionRadius = 5f;
            throwable.FuseTime = 3f;
            throwable.CanBounce = true;
            
            EditorUtility.SetDirty(throwable);
            Debug.Log("[ThrowableDefinitionEditor] Frag Grenade setup complete.");
        }
        
        private void SetupSmokeGrenade(ThrowableDefinition throwable)
        {
            // Stats-only config (Weight)
            var config = ItemStatConfigSetup.GetOrCreateConfig<ThrowableStatConfig>("ThrowableStatConfig_SmokeGrenade");
            ItemStatConfigSetup.SetupSmokeGrenade(config);
            throwable.StatConfig = config;

            // Base fields
            throwable.DisplayName = "Smoke Grenade";
            throwable.Description = "Deploys smoke screen for 15 s. No damage.";
            throwable.IsStackable = true;
            throwable.MaxStackSize = 3;
            throwable.Weight = config.GetStatValue(ItemStatType.Weight);
            
            // Usage System fields
            throwable.ValidSlots = new SlotLocationType[] { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            throwable.CanUseWhileMoving = true;
            throwable.CanCancelUsage = true;
            throwable.UsageDuration = 0f;
            
            // Throw Settings
            throwable.ThrowForce = 12f;
            throwable.MaxRange = 25f;
            throwable.PrepareTime = 0.5f;
            
            // Projectile Behaviour
            throwable.ThrowableType = ThrowableType.Smoke;
            throwable.Damage = 0f;
            throwable.ExplosionRadius = 8f;
            throwable.FuseTime = 2f;
            throwable.CanBounce = false;
            
            EditorUtility.SetDirty(throwable);
            Debug.Log("[ThrowableDefinitionEditor] Smoke Grenade setup complete.");
        }
        
        private void SetupFlashbang(ThrowableDefinition throwable)
        {
            // Weight can be configured later via ThrowableStatConfig if needed.

            // Base fields
            throwable.DisplayName = "Flashbang";
            throwable.Description = "Blinds and stuns enemies in radius. Low damage.";
            throwable.IsStackable = true;
            throwable.MaxStackSize = 2;
            throwable.Weight = 0.25f;
            
            // Usage System fields
            throwable.ValidSlots = new SlotLocationType[] { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            throwable.CanUseWhileMoving = true;
            throwable.CanCancelUsage = true;
            throwable.UsageDuration = 0f;
            
            // Throw Settings
            throwable.ThrowForce = 18f;
            throwable.MaxRange = 25f;
            throwable.PrepareTime = 0.3f;
            
            // Projectile Behaviour
            throwable.ThrowableType = ThrowableType.Flashbang;
            throwable.Damage = 10f;
            throwable.ExplosionRadius = 6f;
            throwable.FuseTime = 1.5f;
            throwable.CanBounce = true;
            
            EditorUtility.SetDirty(throwable);
            Debug.Log("[ThrowableDefinitionEditor] Flashbang setup complete.");
        }
        
        private void SetupIncendiary(ThrowableDefinition throwable)
        {
            // Weight can be configured later via ThrowableStatConfig if needed.

            // Base fields
            throwable.DisplayName = "Incendiary Grenade";
            throwable.Description = "Creates fire area with damage over time.";
            throwable.IsStackable = true;
            throwable.MaxStackSize = 2;
            throwable.Weight = 0.5f;
            
            // Usage System fields
            throwable.ValidSlots = new SlotLocationType[] { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            throwable.CanUseWhileMoving = true;
            throwable.CanCancelUsage = true;
            throwable.UsageDuration = 0f;
            
            // Throw Settings
            throwable.ThrowForce = 14f;
            throwable.MaxRange = 28f;
            throwable.PrepareTime = 0.5f;
            
            // Projectile Behaviour
            throwable.ThrowableType = ThrowableType.Incendiary;
            throwable.Damage = 50f;
            throwable.ExplosionRadius = 4f;
            throwable.FuseTime = 2.5f;
            throwable.CanBounce = false;
            
            EditorUtility.SetDirty(throwable);
            Debug.Log("[ThrowableDefinitionEditor] Incendiary Grenade setup complete.");
        }
        
        private void SetupGasGrenade(ThrowableDefinition throwable)
        {
            // Weight can be configured later via ThrowableStatConfig if needed.

            // Base fields
            throwable.DisplayName = "Gas Grenade";
            throwable.Description = "Releases poison cloud. Damage over time in area.";
            throwable.IsStackable = true;
            throwable.MaxStackSize = 2;
            throwable.Weight = 0.35f;
            
            // Usage System fields
            throwable.ValidSlots = new SlotLocationType[] { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            throwable.CanUseWhileMoving = true;
            throwable.CanCancelUsage = true;
            throwable.UsageDuration = 0f;
            
            // Throw Settings
            throwable.ThrowForce = 13f;
            throwable.MaxRange = 25f;
            throwable.PrepareTime = 0.5f;
            
            // Projectile Behaviour
            throwable.ThrowableType = ThrowableType.Gas;
            throwable.Damage = 30f;
            throwable.ExplosionRadius = 7f;
            throwable.FuseTime = 2f;
            throwable.CanBounce = false;
            
            EditorUtility.SetDirty(throwable);
            Debug.Log("[ThrowableDefinitionEditor] Gas Grenade setup complete.");
        }
        
        private void SetupImpactGrenade(ThrowableDefinition throwable)
        {
            // Weight can be configured later via ThrowableStatConfig if needed.

            // Base fields
            throwable.DisplayName = "Impact Grenade";
            throwable.Description = "Explodes immediately on impact. No fuse time.";
            throwable.IsStackable = true;
            throwable.MaxStackSize = 2;
            throwable.Weight = 0.4f;
            
            // Usage System fields
            throwable.ValidSlots = new SlotLocationType[] { 
                SlotLocationType.Inventory, 
                SlotLocationType.QuickSlot 
            };
            throwable.CanUseWhileMoving = true;
            throwable.CanCancelUsage = true;
            throwable.UsageDuration = 0f;
            
            // Throw Settings
            throwable.ThrowForce = 20f;
            throwable.MaxRange = 30f;
            throwable.PrepareTime = 0.3f;
            
            // Projectile Behaviour
            throwable.ThrowableType = ThrowableType.Impact;
            throwable.Damage = 80f;
            throwable.ExplosionRadius = 4f;
            throwable.FuseTime = 0f; // Impact-only, no fuse
            throwable.CanBounce = false;
            
            EditorUtility.SetDirty(throwable);
            Debug.Log("[ThrowableDefinitionEditor] Impact Grenade setup complete.");
        }
        
        public static class SetupHelpers
        {
            public static void SetupDefaultFragGrenade(ThrowableDefinition throwable)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<ThrowableStatConfig>("ThrowableStatConfig_FragGrenade");
                ItemStatConfigSetup.SetupFragGrenade(config);
                throwable.StatConfig = config;

                throwable.DisplayName = "Frag Grenade";
                throwable.Description = "Explosive grenade – 3 s fuse. Deals AoE damage.";
                throwable.IsStackable = true;
                throwable.MaxStackSize = 3;
                throwable.Weight = config.GetStatValue(ItemStatType.Weight);
                throwable.ValidSlots = new SlotLocationType[] { 
                    SlotLocationType.Inventory, 
                    SlotLocationType.QuickSlot 
                };
                throwable.CanUseWhileMoving = true;
                throwable.CanCancelUsage = true;
                throwable.UsageDuration = 0f;
                throwable.ThrowForce = 15f;
                throwable.MaxRange = 30f;
                throwable.PrepareTime = 0.5f;
                throwable.ThrowableType = ThrowableType.Grenade;
                throwable.Damage = 100f;
                throwable.ExplosionRadius = 5f;
                throwable.FuseTime = 3f;
                throwable.CanBounce = true;
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(throwable);
            }
            
            public static void SetupDefaultSmokeGrenade(ThrowableDefinition throwable)
            {
                var config = ItemStatConfigSetup.GetOrCreateConfig<ThrowableStatConfig>("ThrowableStatConfig_SmokeGrenade");
                ItemStatConfigSetup.SetupSmokeGrenade(config);
                throwable.StatConfig = config;

                throwable.DisplayName = "Smoke Grenade";
                throwable.Description = "Deploys smoke screen for 15 s. No damage.";
                throwable.IsStackable = true;
                throwable.MaxStackSize = 3;
                throwable.Weight = config.GetStatValue(ItemStatType.Weight);
                throwable.ValidSlots = new SlotLocationType[] { 
                    SlotLocationType.Inventory, 
                    SlotLocationType.QuickSlot 
                };
                throwable.CanUseWhileMoving = true;
                throwable.CanCancelUsage = true;
                throwable.UsageDuration = 0f;
                throwable.ThrowForce = 12f;
                throwable.MaxRange = 25f;
                throwable.PrepareTime = 0.5f;
                throwable.ThrowableType = ThrowableType.Smoke;
                throwable.Damage = 0f;
                throwable.ExplosionRadius = 8f;
                throwable.FuseTime = 2f;
                throwable.CanBounce = false;
                EditorUtility.SetDirty(config);
                EditorUtility.SetDirty(throwable);
            }
        }
    }
}
#endif
