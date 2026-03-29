using UnityEngine;
using NightHunt.GameplaySystems.World;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// Đặt Component này tại các vị trí mọc Boss trên bản đồ.
    /// Cho phép Map Designer tuỳ biến phần thưởng theo từng KHU VỰC,
    /// chứ không phải bị cố định theo từng CON BOSS (Prefab).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossSpawnPoint : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Phần thưởng sẽ rớt ra (đồ Fixed + Random) khi có bất kỳ Boss nào chết tại điểm này.")]
        public WorldSpawnConfig RewardConfig;

        /// <summary>Vị trí Boss mọc lên, lấy luôn transform của object này</summary>
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}
