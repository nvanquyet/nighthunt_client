using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// Static world door — đặt sẵn trên Scene, không spawn dynamic.
    ///
    /// DESIGN:
    ///   - Implements IHoldInteractable: HoldDuration > 0 nếu config = Hold, else 0 (Instant).
    ///   - PlayerInteractionSystem xử lý cả hai mode tự động qua interface.
    ///   - State (IsOpen) sync qua SyncVar cho tất cả client.
    ///
    /// SETUP:
    ///   1. Thêm WorldDoor component vào door GameObject trên Scene.
    ///   2. Gán InteractableConfig asset (Create → GameplaySystems → Config → Interactable Config).
    ///   3. Gán Animator (nếu có) → WorldDoor tự gọi SetBool("IsOpen", ...) khi toggle.
    /// </summary>
    public class WorldDoor : NetworkBehaviour, IHoldInteractable
    {
        [Header("Config")]
        [Tooltip("InteractableConfig xác định type, interaction mode, hold duration, prompt...")]
        [SerializeField]
        private InteractableConfig _config;

        [Header("References")] [Tooltip("Animator để play open/close animation (optional).")] [SerializeField]
        private Animator _animator;

        [Tooltip("Tên bool parameter trong Animator điều khiển trạng thái cửa.")] [SerializeField]
        private string _animatorBoolName = "IsOpen";

        [Header("Fallback Visual (khi không có Animator)")]
        [Tooltip("Renderer dùng để đổi màu khi mở/đóng cửa. Tự động tìm trên GameObject nếu để trống.")]
        [SerializeField] private Renderer _fallbackRenderer;
        [Tooltip("Màu fallback khi cửa đóng (chỉ dùng khi không có Animator).")]
        [SerializeField] private Color _closedColor = Color.white;
        [Tooltip("Màu fallback khi cửa mở (chỉ dùng khi không có Animator).")]
        [SerializeField] private Color _openColor   = new Color(0.35f, 0.85f, 0.35f);

        [Header("State")] [Tooltip("Trạng thái ban đầu khi scene load.")] [SerializeField]
        private bool startOpen = false;

        // SYNC: all clients see the same door state
        private readonly SyncVar<bool> syncIsOpen = new SyncVar<bool>();
        private readonly SyncVar<bool> syncIsLocked = new SyncVar<bool>();

        // Client-cache
        private bool _isOpen;
        private bool _isLocked;
        private Coroutine _autoCloseCoroutine;

        public bool IsOpen => syncIsOpen.Value;
        public bool IsLocked => syncIsLocked.Value;

        // ── IHoldInteractable ────────────────────────────────────────────────────

        /// <summary>
        /// 0 = Instant (InteractionMode != Hold hoặc không có config).
        /// > 0 = giây player phải giữ nút trước khi Interact() fire.
        /// </summary>
        public float HoldDuration
            => _config?.InteractionMode == LootInteractionMode.Hold ? _config.HoldDuration : 0f;

        // ── IInteractable ────────────────────────────────────────────────────────

        public string InteractLabel
        {
            get
            {
                if (_isLocked)
                    return _config?.PromptLocked ?? "[E] Locked";

                return _config?.InteractionMode == LootInteractionMode.Hold
                    ? (_config.PromptHolding ?? "[Hold E] Open Door")
                    : (_config?.PromptDefault ?? (_isOpen ? "[E] Close Door" : "[E] Open Door"));
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_isLocked) return false;
            float maxDist = _config?.MaxInteractDistance ?? 3f;
            return Vector3.Distance(transform.position, interactor.transform.position) <= maxDist;
        }

        /// <summary>Client calls this → fires ToggleDoor ServerRpc.</summary>
        public void Interact(GameObject interactor)
        {
            var playerNob = ComponentResolver.Find<FishNet.Object.NetworkObject>(interactor)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] FishNet.Object.NetworkObject not found")
                .Resolve();
            ToggleDoor(playerNob);
        }

        public void OnHoverEnter(GameObject interactor)
        {
            /* outline effect wired up when highlight system is ready */
        }

        public void OnHoverExit(GameObject interactor)
        {
            /* outline effect wired up when highlight system is ready */
        }

        // ── Network lifecycle ────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            syncIsOpen.Value = startOpen;
            syncIsLocked.Value = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Auto-discover fallback renderer if not assigned in Inspector.
            if (_fallbackRenderer == null)
                _fallbackRenderer = GetComponentInChildren<Renderer>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncIsOpen.OnChange += OnOpenChanged;
            syncIsLocked.OnChange += OnLockedChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            syncIsOpen.OnChange -= OnOpenChanged;
            syncIsLocked.OnChange -= OnLockedChanged;
        }

        // ── Server-side toggle ───────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        private void ToggleDoor(FishNet.Object.NetworkObject playerNob,
            FishNet.Connection.NetworkConnection conn = null)
        {
            if (!IsServerInitialized) return;

            // Host fallback: conn not injected for local ServerRpc calls.
            if (conn == null) conn = playerNob?.Owner;

            // Ownership check — prevent remote clients triggering door for someone else.
            if (playerNob != null && conn != null && playerNob.Owner != conn)
            {
                Debug.LogWarning($"[WorldDoor] ToggleDoor: ownership mismatch (ClientId={conn.ClientId}).");
                return;
            }

            // Distance check — server-side re-validation.
            if (playerNob != null)
            {
                var player = ComponentResolver.Find<NightHunt.Networking.NetworkPlayer>(playerNob)
                    .OnSelf()
                    .InChildren()
                    .OrLogWarning("[Auto] NightHunt.Networking.NetworkPlayer not found")
                    .Resolve();
                if (player != null)
                {
                    float dist = Vector3.Distance(transform.position, player.transform.position);
                    float maxDist = _config?.MaxInteractDistance ?? 3f;
                    if (dist > maxDist)
                    {
                        Debug.LogWarning($"[WorldDoor] ToggleDoor: Quá xa ({dist:F2}m > {maxDist}m).");
                        return;
                    }
                }
            }

            if (_isLocked)
            {
                Debug.LogWarning("[WorldDoor] ToggleDoor: door is locked.");
                return;
            }

            if (_config?.OneTimeUse == true && _isOpen)
            {
                Debug.Log("[WorldDoor] OneTimeUse door: already opened.");
                return;
            }

            syncIsOpen.Value = !syncIsOpen.Value;
            Debug.Log($"[WorldDoor] Toggled → IsOpen={syncIsOpen.Value}");

            // Auto-close: nếu vừa mở và config có AutoReset
            if (syncIsOpen.Value && _config?.AutoReset == true)
            {
                if (_autoCloseCoroutine != null) StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = StartCoroutine(AutoCloseCoroutine());
            }
        }

        /// <summary>Lock / Unlock door server-side (called by logic e.g. QuestSystem).</summary>
        [Server]
        public void SetLocked(bool locked)
        {
            syncIsLocked.Value = locked;
        }

        // ── Auto Close ────────────────────────────────────────────────────

        /// <summary>Cửa tự đóng lại sau AutoResetDelay giây.</summary>
        private IEnumerator AutoCloseCoroutine()
        {
            float delay = _config?.AutoResetDelay ?? 10f;
            Debug.Log($"[WorldDoor] Tự đóng sau {delay}s...");
            yield return new WaitForSeconds(delay);
            if (syncIsOpen.Value) // vẫn đang mở thì mới đóng
            {
                syncIsOpen.Value = false;
                Debug.Log("[WorldDoor] đã tự đóng.");
            }

            _autoCloseCoroutine = null;
        }

        // ── Client sync handlers ─────────────────────────────────────────────────

        private void OnOpenChanged(bool oldVal, bool newVal, bool asServer)
        {
            _isOpen = newVal;
            if (_animator != null)
            {
                _animator.SetBool(_animatorBoolName, _isOpen);
            }
            else
            {
                // Fallback: tint the first renderer on this door so state is always visible.
                ApplyFallbackColor();
            }
        }

        private void ApplyFallbackColor()
        {
            if (_fallbackRenderer == null) return;
            // Use MaterialPropertyBlock to avoid creating extra material instances.
            var block = new MaterialPropertyBlock();
            _fallbackRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", _isOpen ? _openColor : _closedColor);
            _fallbackRenderer.SetPropertyBlock(block);
        }

        private void OnLockedChanged(bool oldVal, bool newVal, bool asServer)
        {
            _isLocked = newVal;
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = _isLocked ? Color.red : Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f,
                $"Door [{(_isOpen ? "OPEN" : "CLOSED")}]");
#endif
        }
    }
}