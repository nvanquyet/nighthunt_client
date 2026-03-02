using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Interfaces;
using UnityEngine.Events;

namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// Static world switch / button — đặt sẵn trên Scene, không spawn dynamic.
    ///
    /// DESIGN:
    ///   - Toggle switch: mỗi lần Interact() đổi state On↔Off → trigger OnActivated / OnDeactivated.
    ///   - Button (OneTimeUse): chỉ trigger 1 lần → OnActivated, sau đó CanInteract = false.
    ///   - State sync qua SyncVar.
    ///   - Dùng UnityEvent để kết nối với bất kỳ logic nào mà không cần code thêm.
    ///
    /// SETUP:
    ///   1. Thêm WorldSwitch vào GameObject trên Scene.
    ///   2. Gán InteractableConfig (Type = Switch hoặc Button).
    ///   3. Wire UnityEvents OnActivated / OnDeactivated trong Inspector.
    /// </summary>
    public class WorldSwitch : NetworkBehaviour, IHoldInteractable
    {
        [Header("Config")]
        [SerializeField] private InteractableConfig _config;

        [Header("Events")]
        [Tooltip("Fired (server + clients) khi switch được bật ON hoặc button được nhấn.")]
        public UnityEvent OnActivated;

        [Tooltip("Fired (server + clients) khi switch được tắt OFF (không fire với Button type).")]
        public UnityEvent OnDeactivated;

        [Header("State")]
        [SerializeField] private bool startActive = false;

        // SYNC
        private readonly SyncVar<bool> syncIsActive = new SyncVar<bool>();
        private readonly SyncVar<bool> syncIsUsed   = new SyncVar<bool>(); // OneTimeUse exhausted

        // Client-cache
        private bool _isActive;
        private bool _isUsed;

        public bool IsActive => syncIsActive.Value;
        public bool IsUsed   => syncIsUsed.Value;

        // ── IHoldInteractable ────────────────────────────────────────────────────

        public float HoldDuration
            => _config?.InteractionMode == LootInteractionMode.Hold ? _config.HoldDuration : 0f;

        // ── IInteractable ────────────────────────────────────────────────────────

        public string InteractLabel
        {
            get
            {
                if (_isUsed) return _config?.PromptLocked ?? "[E] Used";

                bool isButton = _config?.InteractionType == InteractionType.Button;
                if (isButton)
                    return _config?.PromptDefault ?? "[E] Activate";

                return _config?.InteractionMode == LootInteractionMode.Hold
                    ? (_config.PromptHolding ?? $"[Hold E] {(_isActive ? "Deactivate" : "Activate")}")
                    : (_config?.PromptDefault ?? (_isActive ? "[E] Deactivate" : "[E] Activate"));
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_isUsed) return false;
            float maxDist = _config?.MaxInteractDistance ?? 3f;
            return Vector3.Distance(transform.position, interactor.transform.position) <= maxDist;
        }

        public void Interact(GameObject interactor) => Toggle();

        public void OnHoverEnter(GameObject interactor) { /* outline effect wired up when highlight system is ready */ }
        public void OnHoverExit(GameObject interactor)  { /* outline effect wired up when highlight system is ready */ }

        // ── Network lifecycle ────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            syncIsActive.Value = startActive;
            syncIsUsed.Value   = false;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncIsActive.OnChange += OnActiveChanged;
            syncIsUsed.OnChange   += OnUsedChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncIsActive.OnChange -= OnActiveChanged;
            syncIsUsed.OnChange   -= OnUsedChanged;
        }

        // ── Server-side toggle ───────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        private void Toggle()
        {
            if (!IsServerInitialized) return;
            if (_isUsed) return;

            bool willBeActive = !_isActive;
            syncIsActive.Value = willBeActive;

            if (_config?.OneTimeUse == true)
                syncIsUsed.Value = true;

            RpcFireEvents(willBeActive);
            Debug.Log($"[WorldSwitch] Toggled → IsActive={willBeActive}");
        }

        [ObserversRpc]
        private void RpcFireEvents(bool activated)
        {
            if (activated) OnActivated?.Invoke();
            else           OnDeactivated?.Invoke();
        }

        // ── Client sync handlers ─────────────────────────────────────────────────

        private void OnActiveChanged(bool oldVal, bool newVal, bool asServer) => _isActive = newVal;
        private void OnUsedChanged(bool oldVal, bool newVal, bool asServer)   => _isUsed   = newVal;

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = _isActive ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"Switch [{(_isActive ? "ON" : "OFF")}]");
#endif
        }
    }
}
