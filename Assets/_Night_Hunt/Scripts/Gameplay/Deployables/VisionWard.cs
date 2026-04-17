using UnityEngine;
using FishNet.Object;
using NightHunt.Gameplay.Deployables;
using FOW;
using NightHunt.Gameplay.FogOfWar;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Mắt Soi Sáng (Vision Ward) - Provides tầm nhìn (Ánh sáng FOW) cho cục bộ teammate,
    /// Tàng hình nếu enemy đi ngan qua. Bị tiêu diệt nếu bắn 3 hit (máu = 30).
    /// Kế thừa toàn bộ Máu, Destroy, Trạng Thái Cắm từ BaseDeployable.
    /// </summary>
    [RequireComponent(typeof(FogOfWarRevealer3D))]
    public class VisionWard : BaseDeployable
    {
        [Header("Vision Config")]
        [Tooltip("Bán kính phát sáng của con mắt")]
        [SerializeField] private float visionRadius = 15f;
        [Tooltip("Thời gian sống tối đa (0 = tồn tại mãi tới khi bị phá)")]
        [SerializeField] private float lifetimeSeconds = 120f;

        private FogOfWarRevealer3D _revealer;
        private FogTeamVisibilityBinder _visibilityBinder;
        
        private void Awake()
        {
            _revealer = GetComponent<FogOfWarRevealer3D>();
            
            // Nếu muốn con mắt VÔ HÌNH TRONG MẮT ĐỊCH, bạn hãy ném FogTeamVisibilityBinder 
            // vào Prefab của VisionWard. Script sẽ tự động lấy thông tin từ BaseDeployable.
            _visibilityBinder = GetComponent<FogTeamVisibilityBinder>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _revealer.ViewRadius = visionRadius;
            
            // Xử lý logic display ngay on spawn (Bật đèn nếu phe ta)
            UpdateVisionState();
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            base.OnDeployablePlaced();
            
            Debug.Log($"[VisionWard] Team {_ownerTeamId.Value} đặt mắt success! Máu: {_currentHP.Value}");
            
            // Nếu có lifetime hẹn giờ chết
            if (lifetimeSeconds > 0)
            {
                Invoke(nameof(ExpireWard), lifetimeSeconds);
            }
        }

        [Server]
        private void ExpireWard()
        {
            if (_isActive.Value)
            {
                OnDeployableDestroyed(); 
            }
        }

        // --- Client Audio / Visual ---
        
        protected override void OnIsPlacedChanged(bool oldVal, bool newVal, bool asServer)
        {
            base.OnIsPlacedChanged(oldVal, newVal, asServer);
            UpdateVisionState();
        }

        private void UpdateVisionState()
        {
            // Mắt chưa cắm success => tắt đèn
            if (!_isPlaced.Value || !_isActive.Value) 
            {
                _revealer.enabled = false;
                return;
            }

            // Nếu cắm success: Ai là Địch => Đèn Tắt (screen tối thui) | Ai là Phải Ta => Đèn Bật (sáng nguyên vùng)
            bool isEnemy = CheckIfEnemyForLocalClient();
            _revealer.enabled = !isEnemy;
        }

        private bool CheckIfEnemyForLocalClient()
        {
            // Tương tự logic check của Player (SpectatorManager / FogTeamVisibilityBinder)
            var localPlayer = NightHunt.Gameplay.Spectator.SpectateManager.Instance?.GetLocalPlayer();
            if (localPlayer == null) return false; // Nếu chưa load xong thì cứ soi sáng đi

            return localPlayer.TeamId != _ownerTeamId.Value;
        }
    }
}
