using UnityEngine;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Base class cho tất cả projectile (đạn, grenade, smoke…).
    ///
    /// Cấu trúc prefab (2 child):
    ///   MainVisual     — mesh + trail, active mặc định, tắt khi va chạm nếu hideTrailOnImpact = true.
    ///   DetonationVFX  — particle duy nhất, inactive mặc định, bật khi nổ/va chạm.
    ///                    Nội dung thay đổi tuỳ loại: spark (bullet), explosion (grenade), smoke cloud (smoke).
    ///
    /// Weapon prefab (KHÔNG phải projectile) có thêm child MuzzleFlash riêng.
    /// </summary>
    public class ProjectileBase : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Inspector — VFX children (kéo thả trong prefab)
        // -----------------------------------------------------------------
        [Header("VFX Children")]
        [Tooltip("Child chứa mesh/trail — active mặc định khi bay.")]
        public GameObject mainVisualChild;

        [Tooltip("Child particle duy nhất cho hiệu ứng va chạm/nổ/smoke — inactive mặc định.")]
        public GameObject detonationVFXChild;

        // -----------------------------------------------------------------
        // Inspector — Config
        // -----------------------------------------------------------------
        [Header("Detonation Config")]
        [Tooltip("Nổ/tương tác ngay khi va chạm. False = chỉ nổ khi hết fuseTime.")]
        public bool isImpact = true;

        [Tooltip("Giây chờ trước khi tự nổ. 0 = không dùng (chỉ impact).")]
        public float fuseTime = 0f;

        [Tooltip("Giây tồn tại sau khi nổ để chạy hết VFX, rồi deactivate/pool.")]
        public float lifetimeAfterImpact = 3f;

        [Tooltip("Tắt MainVisual ngay khi nổ để chỉ thấy DetonationVFX.")]
        public bool hideTrailOnImpact = true;

        // -----------------------------------------------------------------
        // Internal reset — gọi trong OnEnable để pool reuse hoạt động đúng
        // -----------------------------------------------------------------
        protected virtual void OnEnable()
        {
            if (mainVisualChild != null)
                mainVisualChild.SetActive(true);

            if (detonationVFXChild != null)
                detonationVFXChild.SetActive(false);
        }

        // -----------------------------------------------------------------
        // API công khai
        // -----------------------------------------------------------------

        /// <summary>Restart main visual (trail/mesh). Gọi khi spawn/reuse từ pool.</summary>
        public virtual void PlayMainVisual()
        {
            if (mainVisualChild == null) return;
            mainVisualChild.SetActive(true);
            RestartParticleSystems(mainVisualChild);
        }

        /// <summary>
        /// Kích hoạt DetonationVFX tại vị trí va chạm.
        /// Tắt MainVisual nếu hideTrailOnImpact = true.
        /// Không Instantiate gì thêm — chỉ bật/tắt child.
        /// </summary>
        public virtual void TriggerDetonation(Vector3 position, Quaternion rotation)
        {
            if (hideTrailOnImpact && mainVisualChild != null)
                mainVisualChild.SetActive(false);

            if (detonationVFXChild != null)
            {
                detonationVFXChild.transform.position = position;
                detonationVFXChild.transform.rotation = rotation;
                detonationVFXChild.SetActive(true);
                RestartParticleSystems(detonationVFXChild);
            }
        }

        // -----------------------------------------------------------------
        // Helper
        // -----------------------------------------------------------------
        protected void RestartParticleSystems(GameObject root)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
    }
}
