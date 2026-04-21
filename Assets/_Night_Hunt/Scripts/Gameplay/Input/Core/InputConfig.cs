using UnityEngine;
using UnityEngine.InputSystem;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Centralized configuration for input system.
    /// Create via: Assets &gt; Create &gt; Night Hunt &gt; Input &gt; Input Config
    /// </summary>
    [CreateAssetMenu(fileName = "InputConfig", menuName = "NightHunt/Config/Input Config")]
    public class InputConfig : ScriptableObject
    {
        [Header("Input Action Asset")]
        [Tooltip("The main InputActionAsset for the entire game")]
        [SerializeField] private InputActionAsset inputActionAsset;

        [Header("Action Map Names – must match names in InputSystem_Actions.inputactions")]
        [SerializeField] private string playerMapName     = "Player";
        [SerializeField] private string combatMapName     = "Combat";
        [SerializeField] private string inventoryMapName  = "Inventory";
        [SerializeField] private string cameraMapName     = "Camera";
        [SerializeField] private string uiMapName         = "UI";
        [SerializeField] private string spectatorMapName  = "Spectator";
        [SerializeField] private string teamMapName       = "Team";
        [SerializeField] private string objectivesMapName = "Objectives";
        [SerializeField] private string devicesMapName    = "Devices";
        [SerializeField] private string debugMapName      = "Debug";

        [Header("Input Settings")]
        [SerializeField] private float mouseSensitivity = 1f;
        [SerializeField] private bool  invertY          = false;

        // ── Public properties ──────────────────────────────────────────────────────
        public InputActionAsset InputActionAsset  => inputActionAsset;
        public string PlayerMapName               => playerMapName;
        public string CombatMapName               => combatMapName;
        public string InventoryMapName            => inventoryMapName;
        public string CameraMapName               => cameraMapName;
        public string UIMapName                   => uiMapName;
        public string SpectatorMapName            => spectatorMapName;
        public string TeamMapName                 => teamMapName;
        public string ObjectivesMapName           => objectivesMapName;
        public string DevicesMapName              => devicesMapName;
        public string DebugMapName                => debugMapName;
        public float  MouseSensitivity            => mouseSensitivity;
        public bool   InvertY                     => invertY;

        private void OnValidate()
        {
            if (inputActionAsset == null)
                Debug.LogWarning("[InputConfig] InputActionAsset not yet assign!");
        }
    }
}
