using UnityEngine;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Manages client-side effects and visual feedback
    /// </summary>
    public class ClientEffectManager : MonoBehaviour
    {
        private static ClientEffectManager _instance;
        public static ClientEffectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("ClientEffectManager");
                    _instance = go.AddComponent<ClientEffectManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Effect Prefabs")]
        [SerializeField] private GameObject damageEffectPrefab;
        [SerializeField] private GameObject healEffectPrefab;
        [SerializeField] private GameObject projectileTrailPrefab;
        [SerializeField] private GameObject muzzleFlashPrefab;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SubscribeToEvents();
        }

        /// <summary>
        /// Subscribe to gameplay events
        /// </summary>
        private void SubscribeToEvents()
        {
            var eventBus = Core.Events.GameplayEventBus.Instance;
            if (eventBus != null)
            {
                eventBus.Subscribe<DamageEffectEvent>(OnDamageEffect);
                eventBus.Subscribe<ProjectileSpawnEvent>(OnProjectileSpawn);
            }
        }

        private void OnDestroy()
        {
            var eventBus = Core.Events.GameplayEventBus.Instance;
            if (eventBus != null)
            {
                eventBus.Unsubscribe<DamageEffectEvent>(OnDamageEffect);
                eventBus.Unsubscribe<ProjectileSpawnEvent>(OnProjectileSpawn);
            }
        }

        /// <summary>
        /// Handle damage effect event
        /// </summary>
        private void OnDamageEffect(DamageEffectEvent eventData)
        {
            // Spawn damage effect locally
            if (damageEffectPrefab != null)
            {
                GameObject effect = Instantiate(damageEffectPrefab, eventData.HitPoint, Quaternion.LookRotation(eventData.HitDirection));
                Destroy(effect, 2f);
            }
        }

        /// <summary>
        /// Handle projectile spawn event
        /// </summary>
        private void OnProjectileSpawn(ProjectileSpawnEvent eventData)
        {
            // Spawn visual projectile trail locally
            if (projectileTrailPrefab != null)
            {
                GameObject trail = Instantiate(projectileTrailPrefab, eventData.Position, Quaternion.LookRotation(eventData.Direction));
                Destroy(trail, 1f);
            }
        }

        /// <summary>
        /// Spawn muzzle flash effect
        /// </summary>
        public void SpawnMuzzleFlash(Vector3 position, Quaternion rotation)
        {
            if (muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, position, rotation);
                Destroy(flash, 0.1f);
            }
        }

        /// <summary>
        /// Spawn heal effect
        /// </summary>
        public void SpawnHealEffect(Vector3 position)
        {
            if (healEffectPrefab != null)
            {
                GameObject effect = Instantiate(healEffectPrefab, position, Quaternion.identity);
                Destroy(effect, 2f);
            }
        }
    }
}

