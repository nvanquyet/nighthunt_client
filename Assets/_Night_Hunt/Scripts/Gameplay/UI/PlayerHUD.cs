using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Networking;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Main HUD controller for player
    /// Displays health, stamina, prompt text, quick slots, weapon slots
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health & Stamina")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Slider staminaBar;
        [SerializeField] private TextMeshProUGUI staminaText;

        [Header("Prompt Text")]
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private TextMeshProUGUI promptText;

        [Header("Quick Slots")]
        [SerializeField] private QuickSlotUI[] quickSlots = new QuickSlotUI[4]; // Assign manually in inspector

        [Header("Weapon Slots")]
        [SerializeField] private WeaponSlotUI[] weaponSlots = new WeaponSlotUI[2]; // Assign manually in inspector

        [Header("Weight Display")]
        [SerializeField] private Slider weightBar;
        [SerializeField] private TextMeshProUGUI weightText;

        private NetworkPlayer localPlayer;
        private InventorySystem inventorySystem;
        private CharacterStats characterStats;
        private InventoryPanel inventoryPanel;
        private bool isInitialized = false;

        /// <summary>
        /// Initialize HUD with player references
        /// </summary>
        public void Initialize(NetworkPlayer player, InventorySystem inventory, InventoryPanel panel = null)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                Debug.LogWarning("[PlayerHUD] Cannot initialize: Not local player!");
                return;
            }

            localPlayer = player;
            inventorySystem = inventory;
            inventoryPanel = panel;
            characterStats = player.GetComponent<CharacterStats>();

            SetupQuickSlots();
            SetupWeaponSlots();

            isInitialized = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!isInitialized || localPlayer == null || !localPlayer.IsLocalPlayer)
                return;

            UpdateHealthBar();
            UpdateStaminaBar();
            UpdateWeightDisplay();
            UpdateQuickSlots();
            UpdateWeaponSlots();
        }

        /// <summary>
        /// Setup quick slots UI - initialize manually assigned slots
        /// </summary>
        private void SetupQuickSlots()
        {
            // Initialize manually assigned quick slots
            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] != null)
                {
                    quickSlots[i].Initialize(i, this, inventoryPanel);
                }
            }
        }

        /// <summary>
        /// Setup weapon slots UI - initialize manually assigned slots
        /// </summary>
        private void SetupWeaponSlots()
        {
            // Initialize manually assigned weapon slots
            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (weaponSlots[i] != null)
                {
                    weaponSlots[i].Initialize(i, this, inventoryPanel);
                }
            }
        }

        /// <summary>
        /// Update health bar
        /// </summary>
        private void UpdateHealthBar()
        {
            if (characterStats == null || healthBar == null) return;

            float currentHP = characterStats.GetHP();
            float maxHP = characterStats.GetMaxHP();
            float hpPercentage = maxHP > 0 ? currentHP / maxHP : 0f;

            healthBar.value = hpPercentage;

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(currentHP)}/{Mathf.CeilToInt(maxHP)}";
            }
        }

        /// <summary>
        /// Update stamina bar
        /// </summary>
        private void UpdateStaminaBar()
        {
            if (characterStats == null || staminaBar == null) return;

            float currentStamina = characterStats.GetStamina();
            float maxStamina = characterStats.GetMaxStamina();
            float staminaPercentage = maxStamina > 0 ? currentStamina / maxStamina : 0f;

            staminaBar.value = staminaPercentage;

            if (staminaText != null)
            {
                staminaText.text = $"{Mathf.CeilToInt(currentStamina)}/{Mathf.CeilToInt(maxStamina)}";
            }
        }

        /// <summary>
        /// Update weight display
        /// </summary>
        private void UpdateWeightDisplay()
        {
            if (inventorySystem == null || weightBar == null) return;

            float currentWeight = inventorySystem.GetCurrentWeight();
            float maxWeight = inventorySystem.GetWeightCapacity();
            float weightPercentage = maxWeight > 0 ? currentWeight / maxWeight : 0f;

            weightBar.value = weightPercentage;

            if (weightText != null)
            {
                weightText.text = $"{currentWeight:F1}/{maxWeight:F1} kg";
            }
        }

        /// <summary>
        /// Update quick slots display
        /// </summary>
        private void UpdateQuickSlots()
        {
            if (inventorySystem == null) return;

            var quickSlotData = inventorySystem.GetQuickSlots();
            for (int i = 0; i < quickSlots.Length && i < quickSlotData.Length; i++)
            {
                if (quickSlots[i] != null)
                {
                    quickSlots[i].UpdateSlot(quickSlotData[i]);
                }
            }
        }

        /// <summary>
        /// Update weapon slots display
        /// </summary>
        private void UpdateWeaponSlots()
        {
            // TODO: Get equipped weapons from combat system
            // For now, slots will be empty
        }

        /// <summary>
        /// Show interaction prompt
        /// </summary>
        public void ShowPrompt(string text)
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(true);
            }

            if (promptText != null)
            {
                promptText.text = text;
            }
        }

        /// <summary>
        /// Hide interaction prompt
        /// </summary>
        public void HidePrompt()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Get quick slot by index
        /// </summary>
        public QuickSlotUI GetQuickSlot(int index)
        {
            if (index >= 0 && index < quickSlots.Length)
                return quickSlots[index];
            return null;
        }

        /// <summary>
        /// Get weapon slot by index
        /// </summary>
        public WeaponSlotUI GetWeaponSlot(int index)
        {
            if (index >= 0 && index < weaponSlots.Length)
                return weaponSlots[index];
            return null;
        }

        /// <summary>
        /// Get inventory system reference
        /// </summary>
        public InventorySystem GetInventorySystem() => inventorySystem;
    }
}
