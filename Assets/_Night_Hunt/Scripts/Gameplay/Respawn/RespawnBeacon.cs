using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using NightHunt.Data;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.Deployables;
using NightHunt.Gameplay.Character.Combat;

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

        public override HittableTargetType TargetType => HittableTargetType.Beacon;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            All.Add(this);
            // Despawn beacon immediately if final zone is active and config disallows beacons
            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            if (isInFinalZone)
                DespawnDeployable();
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
            Debug.Log($"[RespawnBeacon] Kích hoạt Trạm Hồi Sinh cho Team {_ownerTeamId.Value} success!");
            // Call ra effect khói/âm thanh ở đây nếu muốn
        }

        /// <summary>
        /// Server: Hỏi xem player Team này có quyền hồi sinh ở đây không?
        /// </summary>
        [Server]
        public bool CanRespawnHere(int teamId)
        {
            if (!_isActive.Value || !_isPlaced.Value) return false;
            if (teamId != _ownerTeamId.Value) return false;
            // In the final zone, beacons are disallowed (SafeZoneMatchConfig.beaconAllowedInFinalZone = false)
            if (SafeZoneManager.Instance?.IsInFinalZone ?? false) return false;
            return true;
        }

        // Custom UI (UI) nếu máu thay đổi
        protected override void OnHPChanged(int oldHP, int newHP, bool asServer)
        {
            base.OnHPChanged(oldHP, newHP, asServer);
            // Update UI Cột Máu của Beacon (tuỳ ý)
        }
    }
}
