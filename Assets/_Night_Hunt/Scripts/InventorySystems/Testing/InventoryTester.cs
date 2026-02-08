using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Systems;
using UnityEngine;

namespace _Night_Hunt.Scripts.InventorySystems.Testing
{
    public class InventoryTester : MonoBehaviour
    {
        [SerializeField] private PlayerInventoryController inventory;
        [SerializeField] private ItemDefinition testHelmet;
        [SerializeField] private ItemDefinition testWeapon;
        [SerializeField] private ItemDefinition testGrip;
    
        void Update()
        {
            // Press 1: Add helmet to inventory
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                var helmet = ItemInstanceFactory.CreateInstance(testHelmet);
                inventory.AddItem(helmet, out int slot);
                Debug.Log($"Added helmet to slot {slot}");
            }
        
            // Press 2: Equip helmet from inventory slot 0
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                inventory.EquipFromInventory(0, EquipmentSlotType.Helmet);
                Debug.Log("Equipped helmet");
            }
        
            // Press 3: Add weapon and grip
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                var weapon = ItemInstanceFactory.CreateInstance(testWeapon);
                var grip = ItemInstanceFactory.CreateInstance(testGrip);
            
                inventory.AddItem(weapon, out _);
                inventory.AddItem(grip, out _);
                Debug.Log("Added weapon + grip");
            }
        
            // Press 4: Equip weapon, then attach grip
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                // Get weapon from inventory
                var weapon = inventory.Inventory.GetItemAtSlot(0);
                if (weapon != null)
                {
                    // Equip weapon
                    inventory.EquipWeaponFromInventory(0, WeaponSlotType.Primary);
                
                    // Attach grip (slot 1) to weapon
                    inventory.AttachFromInventory(1, weapon);
                    Debug.Log("Equipped weapon with grip");
                }
            }
        }
    }
}