using UnityEngine;
using NightHunt.Data;
using FishNet.Object;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Spawn projectile local + đồng bộ qua network.
    ///
    /// Flow:
    ///   1. ProjectileWeapon gọi SpawnLocal() → tạo instance trên máy owner (owner thấy luôn).
    ///   2. SpawnLocal() gọi ServerRpc → server validate → ObserversRpc → spawn trên các client còn lại.
    ///   Không còn logic ẩn renderer: owner thấy đúng một viên duy nhất.
    /// </summary>
    public class ProjectileSpawner : NetworkBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab;

        // -----------------------------------------------------------------
        public void SpawnLocal(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[ProjectileSpawner] projectilePrefab chưa được gán!");
                return;
            }

            // Tạo instance — owner thấy viên đạn này
            SpawnInstance(position, direction, weaponConfig);

            // Báo server để broadcast sang các client khác
            if (IsOwner)
                SendToServerRpc(position, direction, weaponConfig);
        }

        // -----------------------------------------------------------------
        private void SpawnInstance(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            var go = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
            var comp = go.GetComponent<ProjectileComponent>();
            if (comp != null)
                comp.Initialize(weaponConfig, direction, false);
        }

        // -----------------------------------------------------------------
        [ServerRpc(RequireOwnership = true)]
        private void SendToServerRpc(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            // TODO: server validate (tầm xa, thời gian, v.v.)
            BroadcastToClientsRpc(position, direction, weaponConfig);
        }

        [ObserversRpc]
        private void BroadcastToClientsRpc(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            // Chỉ spawn trên các client KHÔNG phải owner; owner đã có bản từ SpawnLocal()
            if (!IsOwner)
                SpawnInstance(position, direction, weaponConfig);        }
    }
}