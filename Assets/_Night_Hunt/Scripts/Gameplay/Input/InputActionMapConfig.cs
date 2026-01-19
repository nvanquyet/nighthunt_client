using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Configuration for input action maps
    /// </summary>
    [CreateAssetMenu(fileName = "InputActionMapConfig", menuName = "Night Hunt/Input/Input Action Map Config")]
    public class InputActionMapConfig : ScriptableObject
    {
        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset inputActionAsset;

        [Header("Action Map Names")]
        [SerializeField] private string playerMapName = "Player";
        [SerializeField] private string uiMapName = "UI";
        [SerializeField] private string cameraMapName = "Camera";
        [SerializeField] private string spectatorMapName = "Spectator";
        [SerializeField] private string gameplayMapName = "Gameplay";

        public InputActionAsset InputActionAsset => inputActionAsset;
        public string PlayerMapName => playerMapName;
        public string UIMapName => uiMapName;
        public string CameraMapName => cameraMapName;
        public string SpectatorMapName => spectatorMapName;
        public string GameplayMapName => gameplayMapName;
    }
}

