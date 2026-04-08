using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Deployables;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Kế thừa BaseDeployable. Dành riêng cho chức năng Hồi Sinh (Respawn). 
    /// Không cho tầm nhìn, bị giới hạn theo Phase của Match.
    /// Thông số HP, PlaceTime, RespawnDelay được load từ config trung tâm tùy loại beacon.
    ///
    /// Bug #28 fix: Exposes static <see cref="All"/> registry so RespawnSystem can
    /// iterate beacons in O(k) instead of calling FindObjectsByType every frame.
    /// </summary>
    public class RespawnBeacon : BaseDeployable
    {
        // ── Static O(1) registry — Bug #28 fix ───────────────────────────────────
        /// <summary>
        /// All currently active (server-spawned) RespawnBeacons.
        /// RespawnSystem iterates this instead of FindObjectsByType every frame.
        /// </summary>
        public static readonly HashSet<RespawnBeacon> All = new HashSet<RespawnBeacon>();
        private MatchPhaseManager _phaseManager;

        public override void OnStartNetwork()
        {
            // HP được Initialize từ Server trước khi Spawn.
            // Client chỉ cần quan tâm sync vars.
            base.OnStartNetwork();

            _phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            All.Add(this); // Bug #28 fix: register so RespawnSystem avoids FindObjectsByType
            // Check Phase hiện tại có cho phép xài Beacon hồi sinh hay không
            // Beacons chỉ được phép ở Phase 1 và Phase 2.
            if (_phaseManager != null && _phaseManager.CurrentPhase == MatchPhaseState.Lockdown)
            {
                DespawnDeployable();
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            All.Remove(this); // Bug #28 fix: unregister on despawn
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            base.OnDeployablePlaced();
            Debug.Log($"[RespawnBeacon] Kích hoạt Trạm Hồi Sinh cho Team {_ownerTeamId.Value} thành công!");
            // Gọi ra effect khói/âm thanh ở đây nếu muốn
        }

        /// <summary>
        /// Server: Hỏi xem người chơi Team này có quyền hồi sinh ở đây không?
        /// </summary>
        [Server]
        public bool CanRespawnHere(int teamId)
        {
            if (!_isActive.Value || !_isPlaced.Value) return false;
            
            if (teamId != _ownerTeamId.Value) return false; // Không chơi chung với địch
            
            // Ở Phase 3 (Lockdown) mọi Beacon bị vô hiệu hoá
            if (_phaseManager != null && _phaseManager.CurrentPhase == MatchPhaseState.Lockdown) return false;
            
            return true;
        }

        // Custom giao diện (UI) nếu máu thay đổi
        protected override void OnHPChanged(int oldHP, int newHP, bool asServer)
        {
            base.OnHPChanged(oldHP, newHP, asServer);
            // Update UI Cột Máu của Beacon (tuỳ ý)
        }
    }
}