using System;
using UnityEngine;
using Unity.Cinemachine;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Spectator;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Manages the three gameplay camera states:
    ///   Free       – default, CinemachineInputAxisController active (free rotation).
    ///   Locked     – X-key toggle; camera rotation frozen at current orientation.
    ///   WeaponAim  – entered explicitly when the player aims (e.g. RMB via EnterWeaponAim);
    ///                exits back to the previous state (Free or Locked) when holstered.
    ///
    /// WIRING:
    ///   1. Assign _virtualCamera + _inputAxisController (the CinemachineCamera child component).
    ///   2. Assign _movementInput (MovementInputHandler on this player).
    ///   3. Assign _weaponSystemMB (WeaponSystem MonoBehaviour on this player).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CameraStateManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────
        [Header("Cinemachine")]
        [Tooltip("The player CinemachineCamera (virtual camera).")]
        [SerializeField] private CinemachineCamera _virtualCamera;

        [Tooltip("CinemachineInputAxisController on the virtual camera – disabling it freezes rotation.")]
        [SerializeField] private CinemachineInputAxisController _inputAxisController;

        [Header("Input")]
        [SerializeField] private MovementInputHandler _movementInput;

        [Header("Weapon System")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (typically WeaponSystem on this GameObject).")]
        [SerializeField] private WeaponSystem _weaponSystemMB;

        // ─────────────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────────────

        private IWeaponSystem _weaponSystem;
        private NetworkPlayer _ownerPlayer;

        private CameraState _currentState   = CameraState.Free;
        private CameraState _previousState  = CameraState.Free; // restored when WeaponAim exits

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public CameraState CurrentState => _currentState;

        /// <summary>Fired on every state transition. (from, to)</summary>
        public event Action<CameraState, CameraState> OnStateChanged;

        /// <summary>
        /// Called directly by NetworkPlayer.SetupOwnerSide() on the owning client to
        /// activate or deactivate this player prefab's virtual camera.
        /// This bypasses the NetworkPlayer.OnOwnerReady event subscription timing gap:
        /// CameraStateManager.OnEnable() (where the subscription lives) is called by Unity
        /// AFTER FishNet's OnStartClient, so the handler may not be registered yet when the
        /// event fires on a freshly-spawned prefab.  Direct invocation has no timing issue.
        /// </summary>
        public void SetOwnerCamera(bool active)
        {
            if (_virtualCamera == null)
            {
                Debug.LogError("[CameraStateManager] SetOwnerCamera: _virtualCamera is not assigned! " +
                               "Assign the CinemachineCamera in the Inspector on the player prefab.", this);
                return;
            }
            SetVirtualCameraActive(active);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemMB as IWeaponSystem;
            _ownerPlayer = GetComponentInParent<NetworkPlayer>(includeInactive: true);

            if (_weaponSystemMB != null && _weaponSystem == null)
                Debug.LogError("[CameraStateManager] _weaponSystemMB does not implement IWeaponSystem!", this);

            SetVirtualCameraActive(false);
        }

        private void OnEnable()
        {
            // Auto-discover from InputManager if not assigned in Inspector.
            // This is required for network-spawned player prefabs which cannot
            // reference the scene-level InputManager singleton via SerializeField.
            if (_movementInput == null)
                _movementInput = NightHunt.Gameplay.Input.Core.InputManager.Instance?.MovementHandler;

            if (_movementInput != null)
                _movementInput.OnCameraLockToggled += HandleLockToggled;

            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            
            NetworkPlayer.OnOwnerReady += HandleOwnerReady;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnCurrentPlayerChanged += HandleCurrentPlayerChanged;
        }

        private void Start()
        {
            HandleCurrentPlayerChanged(SpectateManager.Instance?.GetCurrentPlayer());
        }
        

        private void OnDisable()
        {
            if (_movementInput != null)
                _movementInput.OnCameraLockToggled -= HandleLockToggled;

            if (_weaponSystem != null)
                _weaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            NetworkPlayer.OnOwnerReady -= HandleOwnerReady;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnCurrentPlayerChanged -= HandleCurrentPlayerChanged;
        }

        
        private void HandleOwnerReady(NetworkPlayer player)
        {
            if (_virtualCamera == null)
            {
                // _virtualCamera is not assigned — this component is on the player prefab itself.
                // CameraStateManager.OnEnable() subscribes AFTER OnOwnerReady fires because
                // FishNet initialises NetworkBehaviours before Unity's OnEnable/Start sequence
                // completes on the spawned prefab. By the time this handler fires the field
                // must be wired in the Inspector; log an actionable error instead of crashing.
                Debug.LogError("[CameraStateManager] HandleOwnerReady: _virtualCamera is not assigned! " +
                               "Assign it in the Inspector on the player prefab.", this);
                return;
            }
            HandleCurrentPlayerChanged(SpectateManager.Instance != null
                ? SpectateManager.Instance.GetCurrentPlayer()
                : player);
        }

        private void HandleCurrentPlayerChanged(NetworkPlayer player)
        {
            SetVirtualCameraActive(player != null && _ownerPlayer != null && player == _ownerPlayer);
        }

        private void SetVirtualCameraActive(bool active)
        {
            if (_virtualCamera == null) return;
            if (_virtualCamera.gameObject.activeSelf != active)
                _virtualCamera.gameObject.SetActive(active);
        }
        
        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called when the player presses the ToggleCameraLock key (default X).
        /// Ignored while in WeaponAim — the weapon controls the camera mode.
        /// </summary>
        private void HandleLockToggled(bool isLocked)
        {
            // WeaponAim takes priority; ignore manual lock while aiming
            if (_currentState == CameraState.WeaponAim)
                return;

            TransitionTo(isLocked ? CameraState.Locked : CameraState.Free);
        }

        /// <summary>
        /// Called when the active weapon slot changes.
        /// Holstering (newSlot == null) exits WeaponAim if active, restoring the prior state.
        /// Equipping a weapon does NOT enter WeaponAim — camera stays Free/Locked.
        /// WeaponAim is entered only by explicit aim input (e.g. RMB via EnterWeaponAim).
        /// </summary>
        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            if (!newSlot.HasValue)
            {
                ExitWeaponAim();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  State Transitions
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Enter weapon-aim mode (called by aim input handler, e.g. RMB pressed).
        /// Saves the current state (Free/Locked) to restore when aim is released.
        /// No-op if already in WeaponAim.
        /// </summary>
        public void EnterWeaponAim()
        {
            if (_currentState == CameraState.WeaponAim)
                return;

            // Remember where we came from so we can restore on holster
            _previousState = _currentState;
            TransitionTo(CameraState.WeaponAim);
        }

        /// <summary>
        /// Exit weapon-aim mode explicitly (called when aim button released).
        /// No-op if not currently in WeaponAim state.
        /// </summary>
        public void ExitWeaponAim()
        {
            if (_currentState != CameraState.WeaponAim)
                return;

            TransitionTo(_previousState);
        }

        private void TransitionTo(CameraState newState)
        {
            if (newState == _currentState)
                return;

            CameraState from = _currentState;
            _currentState    = newState;

            ApplyStateSettings(newState);
            OnStateChanged?.Invoke(from, newState);
        }

        /// <summary>
        /// Applies visual / input side-effects for each state.
        /// </summary>
        private void ApplyStateSettings(CameraState state)
        {
            switch (state)
            {
                case CameraState.Free:
                    SetCinemachineInput(enabled: true);
                    break;

                case CameraState.Locked:
                    // Freeze the rotation at its current value by disabling the axis controller
                    SetCinemachineInput(enabled: false);
                    break;

                case CameraState.WeaponAim:
                    // Keep rotation locked while weapon is drawn; character facing drives aim
                    SetCinemachineInput(enabled: false);
                    break;
            }
        }

        /// <summary>
        /// Enables or disables the Cinemachine axis input controller so the
        /// camera rotation either tracks mouse input or stays frozen.
        /// </summary>
        private void SetCinemachineInput(bool enabled)
        {
            if (_inputAxisController != null)
                _inputAxisController.enabled = enabled;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns true while the camera is locked (Locked or WeaponAim).</summary>
        public bool IsRotationLocked() => _currentState != CameraState.Free;

        /// <summary>Force-transition to a state (for external systems, e.g. cutscenes).</summary>
        public void ForceState(CameraState state) => TransitionTo(state);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Enum
    // ─────────────────────────────────────────────────────────────────────────

    public enum CameraState
    {
        /// <summary>Free camera rotation via mouse / controller.</summary>
        Free,

        /// <summary>Camera rotation frozen; character strafes relative to locked angle.</summary>
        Locked,

        /// <summary>Camera frozen while a weapon is drawn; exits automatically on holster.</summary>
        WeaponAim
    }
}
