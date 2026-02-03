using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character;

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
        private WeaponConfigData[] weaponSlots;
        private int currentWeaponIndex = 0;
        private bool isSwitching = false;

        private void Awake()
        {
            characterCombat = GetComponent<CharacterCombat>();
            weaponSlots = new WeaponConfigData[maxWeaponSlots];
        }

        private void Update()
        {
            // Weapon switch input
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) && weaponSlots[0] != null)
            {
                SwitchToWeapon(0);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) && weaponSlots[1] != null)
            {
                SwitchToWeapon(1);
            }

            // Scroll wheel switching
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)
            {
                SwitchToNextWeapon();
            }
            else if (scroll < 0f)
            {
                SwitchToPreviousWeapon();
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

