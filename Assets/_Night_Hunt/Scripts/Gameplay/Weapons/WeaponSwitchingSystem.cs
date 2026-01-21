using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Input;
using UnityEngine.InputSystem;

namespace NightHunt.Gameplay.Weapons
{
    /// <summary>
    /// Handles weapon switching between multiple weapons
    /// Supports weapon slots, quick switch, drop/swap
    /// </summary>
    public class WeaponSwitchingSystem : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private int maxWeaponSlots = 2;
        [SerializeField] private float switchTime = 1f;

        [Header("Visual")]
        [SerializeField] private Transform weaponParent;
        [SerializeField] private GameObject[] weaponModels = new GameObject[2];

        private CharacterCombat characterCombat;
        private InventorySystem inventorySystem;
        private PlayerInputHandler inputHandler;
        private WeaponConfigData[] weaponSlots;
        private int currentWeaponIndex = 0;
        private bool isSwitching = false;
        
        // Input state tracking
        private bool wasWeaponSwitch1Pressed = false;
        private bool wasWeaponSwitch2Pressed = false;
        private float lastScrollValue = 0f;

        private void Awake()
        {
            characterCombat = GetComponent<CharacterCombat>();
            inventorySystem = GetComponent<InventorySystem>();
            inputHandler = GetComponent<PlayerInputHandler>();
            weaponSlots = new WeaponConfigData[maxWeaponSlots];
        }

        private void Start()
        {
            // Find input handler if not found
            if (inputHandler == null)
            {
                inputHandler = GetComponentInParent<PlayerInputHandler>();
            }
        }

        private void Update()
        {
            if (inputHandler == null) return;

            // Weapon switch input using New Input System
            bool ws1 = inputHandler.IsQuickSlot1Pressed(); // Reuse QuickSlot1 for Weapon1
            bool ws2 = inputHandler.IsQuickSlot2Pressed(); // Reuse QuickSlot2 for Weapon2

            if (ws1 && !wasWeaponSwitch1Pressed && weaponSlots[0] != null)
            {
                SwitchToWeapon(0);
            }
            wasWeaponSwitch1Pressed = ws1;

            if (ws2 && !wasWeaponSwitch2Pressed && weaponSlots[1] != null)
            {
                SwitchToWeapon(1);
            }
            wasWeaponSwitch2Pressed = ws2;

            // Scroll wheel switching - need to get from Input System
            // Check if Mouse.current is available
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0f && lastScrollValue <= 0f)
                {
                    SwitchToNextWeapon();
                }
                else if (scroll < 0f && lastScrollValue >= 0f)
                {
                    SwitchToPreviousWeapon();
                }
                lastScrollValue = scroll;
            }
        }

        /// <summary>
        /// Equip weapon to slot
        /// </summary>
        public bool EquipWeapon(string weaponId, int slotIndex = -1)
        {
            if (slotIndex < 0)
            {
                // Find empty slot
                slotIndex = FindEmptySlot();
                if (slotIndex < 0)
                {
                    // No empty slot, replace current
                    slotIndex = currentWeaponIndex;
                }
            }

            if (slotIndex < 0 || slotIndex >= maxWeaponSlots)
                return false;

            var weaponConfig = GameConfigLoader.Instance?.GetWeaponConfig(weaponId);
            if (weaponConfig == null)
                return false;

            // Drop current weapon in slot if exists
            if (weaponSlots[slotIndex] != null)
            {
                DropWeapon(slotIndex);
            }

            // Equip new weapon
            weaponSlots[slotIndex] = weaponConfig;
            
            // If switching to this slot, equip it
            if (slotIndex == currentWeaponIndex)
            {
                characterCombat?.EquipWeapon(weaponId);
                UpdateWeaponModel(slotIndex);
            }

            return true;
        }

        /// <summary>
        /// Switch to weapon slot
        /// </summary>
        public void SwitchToWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxWeaponSlots)
                return;

            if (weaponSlots[slotIndex] == null)
                return;

            if (isSwitching)
                return;

            if (slotIndex == currentWeaponIndex)
                return; // Already using this weapon

            StartCoroutine(SwitchWeaponCoroutine(slotIndex));
        }

        /// <summary>
        /// Switch to next weapon
        /// </summary>
        public void SwitchToNextWeapon()
        {
            int nextIndex = (currentWeaponIndex + 1) % maxWeaponSlots;
            int attempts = 0;

            while (weaponSlots[nextIndex] == null && attempts < maxWeaponSlots)
            {
                nextIndex = (nextIndex + 1) % maxWeaponSlots;
                attempts++;
            }

            if (weaponSlots[nextIndex] != null)
            {
                SwitchToWeapon(nextIndex);
            }
        }

        /// <summary>
        /// Switch to previous weapon
        /// </summary>
        public void SwitchToPreviousWeapon()
        {
            int prevIndex = (currentWeaponIndex - 1 + maxWeaponSlots) % maxWeaponSlots;
            int attempts = 0;

            while (weaponSlots[prevIndex] == null && attempts < maxWeaponSlots)
            {
                prevIndex = (prevIndex - 1 + maxWeaponSlots) % maxWeaponSlots;
                attempts++;
            }

            if (weaponSlots[prevIndex] != null)
            {
                SwitchToWeapon(prevIndex);
            }
        }

        /// <summary>
        /// Switch weapon coroutine
        /// </summary>
        private System.Collections.IEnumerator SwitchWeaponCoroutine(int newIndex)
        {
            isSwitching = true;

            // Hide current weapon
            if (weaponModels[currentWeaponIndex] != null)
            {
                weaponModels[currentWeaponIndex].SetActive(false);
            }

            yield return new WaitForSeconds(switchTime);

            // Switch
            currentWeaponIndex = newIndex;
            characterCombat?.EquipWeapon(weaponSlots[newIndex].WeaponId);
            UpdateWeaponModel(newIndex);

            isSwitching = false;
        }

        /// <summary>
        /// Drop weapon from slot
        /// </summary>
        public void DropWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxWeaponSlots)
                return;

            if (weaponSlots[slotIndex] == null)
                return;

            // Spawn weapon pickup in world
            // Would need weapon pickup prefab

            // Clear slot
            weaponSlots[slotIndex] = null;

            // If dropping current weapon, switch to other
            if (slotIndex == currentWeaponIndex)
            {
                // Find another weapon
                for (int i = 0; i < maxWeaponSlots; i++)
                {
                    if (i != slotIndex && weaponSlots[i] != null)
                    {
                        SwitchToWeapon(i);
                        break;
                    }
                }

                // No other weapon, unequip
                if (weaponSlots[currentWeaponIndex] == null)
                {
                    characterCombat?.EquipWeapon(null);
                }
            }

            UpdateWeaponModel(slotIndex);
        }

        /// <summary>
        /// Find empty weapon slot
        /// </summary>
        private int FindEmptySlot()
        {
            for (int i = 0; i < maxWeaponSlots; i++)
            {
                if (weaponSlots[i] == null)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Update weapon model
        /// </summary>
        private void UpdateWeaponModel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= weaponModels.Length)
                return;

            if (weaponModels[slotIndex] != null)
            {
                weaponModels[slotIndex].SetActive(slotIndex == currentWeaponIndex);
            }
        }

        /// <summary>
        /// Get current weapon
        /// </summary>
        public WeaponConfigData GetCurrentWeapon()
        {
            if (currentWeaponIndex >= 0 && currentWeaponIndex < weaponSlots.Length)
            {
                return weaponSlots[currentWeaponIndex];
            }
            return null;
        }

        /// <summary>
        /// Get weapon in slot
        /// </summary>
        public WeaponConfigData GetWeaponInSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < weaponSlots.Length)
            {
                return weaponSlots[slotIndex];
            }
            return null;
        }
    }
}

