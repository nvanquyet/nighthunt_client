using UnityEngine;
using NightHunt.Data;
using FishNet.Object;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Spawns projectiles locally for the owner and broadcasts to remote clients via FishNet RPCs.
    ///
    /// Flow:
    ///   1. Owner calls SpawnLocal() → Instantiate owner copy, mark as authoritative for damage.
    ///   2. SpawnLocal() → SendToServerRpc → server validates → BroadcastToClientsRpc → non-owners Instantiate visual copy.
    ///   Only the owner copy has _isOwnerShot = true, so only it sends damage RPCs to the server.
    /// </summary>
    public class ProjectileSpawner : NetworkBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab;

        public void SpawnLocal(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[ProjectileSpawner] projectilePrefab is not assigned.");
                return;
            }

            // Owner copy — marked as authoritative so it can send damage RPCs.
            SpawnInstance(position, direction, weaponConfig, isOwnerCopy: true);

            if (IsOwner)
                SendToServerRpc(position, direction, weaponConfig);
        }

        private void SpawnInstance(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig,
                                   bool isOwnerCopy = false)
        {
            var go   = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
            var comp = go.GetComponent<ProjectileComponent>();
            if (comp == null) return;

            comp.Initialize(weaponConfig, direction, false);

            if (isOwnerCopy)
                comp.SetOwnerData((int)ObjectId, weaponConfig.WeaponId);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendToServerRpc(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            // TODO: validate position plausibility against server-tracked player position.
            BroadcastToClientsRpc(position, direction, weaponConfig);
        }

        [ObserversRpc]
        private void BroadcastToClientsRpc(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            // Non-owners spawn a visual-only copy; isOwnerCopy = false so no damage RPCs are sent.
            if (!IsOwner)
                SpawnInstance(position, direction, weaponConfig, isOwnerCopy: false);
        }
    }
}