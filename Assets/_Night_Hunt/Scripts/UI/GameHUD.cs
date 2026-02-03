using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;

namespace NightHunt.UI
{
    /// <summary>
    /// Main HUD for gameplay
    /// Displays health, stamina, ammo, minimap, etc.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Health Bar")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Image healthBarFill;

        [Header("Stamina Bar")]
        [SerializeField] private Slider staminaBar;
        [SerializeField] private TextMeshProUGUI staminaText;

        [Header("Ammo Display")]
        [SerializeField] private TextMeshProUGUI ammoText;
        [SerializeField] private TextMeshProUGUI reserveAmmoText;
        [SerializeField] private GameObject reloadIndicator;

        [Header("Weapon Display")]
        [SerializeField] private TextMeshProUGUI weaponNameText;
        [SerializeField] private Image weaponIcon;

        [Header("Weight Display")]
        [SerializeField] private Slider weightBar;
        [SerializeField] private TextMeshProUGUI weightText;
        [SerializeField] private Color normalWeightColor = Color.green;
        [SerializeField] private Color heavyWeightColor = Color.red;

        [Header("Minimap")]
        [SerializeField] private RectTransform minimapContainer;
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RawImage minimapImage;

        [Header("Crosshair")]
        [SerializeField] private Image crosshair;
        [SerializeField] private float crosshairSpread = 10f;

        private NetworkPlayer localPlayer;
        private CharacterStats characterStats;
        private CharacterCombat characterCombat;
        private CharacterPredictedMovement _characterPredictedMovement;

        private void Start()
        {
            // Find local player
            FindLocalPlayer();
        }

        private void Update()
        {
            if (localPlayer == null)
            {
                FindLocalPlayer();
                return;
            }

            UpdateHealthBar();
            UpdateStaminaBar();
            UpdateAmmoDisplay();
            UpdateWeightDisplay();
            UpdateCrosshair();
        }

        /// <summary>
        /// Find local player
        /// </summary>
        private void FindLocalPlayer()
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.IsLocalPlayer)
                {
                    localPlayer = player;
                    characterStats = player.GetComponent<CharacterStats>();
                    characterCombat = player.GetComponent<CharacterCombat>();
                    _characterPredictedMovement = player.GetComponent<CharacterPredictedMovement>();
                    break;
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

            // Color based on health
            if (healthBarFill != null)
            {
                if (hpPercentage > 0.6f)
                    healthBarFill.color = Color.green;
                else if (hpPercentage > 0.3f)
                    healthBarFill.color = Color.yellow;
                else
                    healthBarFill.color = Color.red;
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
        /// Update ammo display
        /// </summary>
        private void UpdateAmmoDisplay()
        {
            if (characterCombat == null) return;

            int currentAmmo = characterCombat.GetCurrentAmmo();
            int reserveAmmo = characterCombat.GetReserveAmmo();
            bool isReloading = characterCombat.IsReloading();

            if (ammoText != null)
            {
                ammoText.text = currentAmmo.ToString();
            }

            if (reserveAmmoText != null)
            {
                reserveAmmoText.text = reserveAmmo.ToString();
            }

            if (reloadIndicator != null)
            {
                reloadIndicator.SetActive(isReloading);
            }

            // Update weapon name
            var weapon = characterCombat.GetCurrentWeapon();
            if (weapon != null && weaponNameText != null)
            {
                weaponNameText.text = weapon.DisplayName;
            }
        }

        /// <summary>
        /// Update weight display
        /// </summary>
        private void UpdateWeightDisplay()
        {
            // // if (inventorySystem == null || weightBar == null) return;
            // //
            // // float currentWeight = inventorySystem.GetCurrentWeight();
            // // float maxWeight = inventorySystem.GetWeightCapacity();
            // float weightPercentage = maxWeight > 0 ? currentWeight / maxWeight : 0f;
            //
            // weightBar.value = weightPercentage;
            //
            // if (weightText != null)
            // {
            //     weightText.text = $"{currentWeight:F1}/{maxWeight:F1} kg";
            // }
            //
            // // Color based on weight
            // if (weightBar != null)
            // {
            //     var fillImage = weightBar.fillRect?.GetComponent<Image>();
            //     if (fillImage != null)
            //     {
            //         if (weightPercentage < 0.8f)
            //             fillImage.color = normalWeightColor;
            //         else
            //             fillImage.color = Color.Lerp(normalWeightColor, heavyWeightColor, (weightPercentage - 0.8f) / 0.2f);
            //     }
            // }
        }

        /// <summary>
        /// Update crosshair
        /// </summary>
        private void UpdateCrosshair()
        {
            if (crosshair == null) return;

            // Update crosshair spread based on weapon spread
            if (characterCombat != null)
            {
                // Would need to get spread from combat system
                // For now, just show crosshair
            }
        }

        /// <summary>
        /// Show/Hide HUD
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}

