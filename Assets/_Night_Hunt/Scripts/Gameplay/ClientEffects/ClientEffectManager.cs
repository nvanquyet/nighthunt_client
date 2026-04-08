using UnityEngine;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Gameplay.ClientEffects
{
    /// <summary>
    /// Client-side VFX dispatcher for hit sparks, heal bursts, muzzle flashes, and trails.
    ///
    /// POOLING:
    ///   All VFX are served by SimpleEffectPool — zero Instantiate/Destroy in the hot path.
    ///   GameObjects are activated, played, then returned to the pool automatically after
    ///   their configured lifetime.
    ///
    /// SETUP (scene):
    ///   1. Place this component on a persistent scene GameObject ("VFX" or "Systems").
    ///   2. Ensure SimpleEffectPool is on the SAME or a sibling GameObject.
    ///   3. Assign the four effect prefabs in the Inspector.
    ///   4. Do NOT rely on the auto-create singleton path — always place in scene.
    ///
    /// EVENT FLOW:
    ///   Shooter/damage system publishes DamageEffectEvent or ProjectileSpawnEvent
    ///   via GameplayEventBus → this manager subscribes and requests VFX from the pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientEffectManager : MonoBehaviour
    {
        public static ClientEffectManager Instance { get; private set; }

        [Header("Effect Prefabs")]
        [Tooltip("Spark / blood / hit-impact VFX at the damage point. " +
                 "Assign VFX_HitSpark_Template (NightHunt/Tools/Build Template Prefabs). " +
                 "Must be registered with SimpleEffectPool — add a ParticleSystem, set loops=false, " +
                 "configure particle lifetime ≤ damageEffectLifetime below.")]
        [SerializeField] private GameObject damageEffectPrefab;

        [Tooltip("Heal VFX played on the character when health is restored. " +
                 "Assign VFX_HealBurst_Template. Same SimpleEffectPool rules as damageEffectPrefab.")]
        [SerializeField] private GameObject healEffectPrefab;

        [Tooltip("Visual effect spawned STATIONARY at the projectile's launch position when ProjectileSpawnEvent fires. " +
                 "Assign VFX_BulletTrail_Template or any muzzle-origin tracer ParticleSystem. " +
                 "NOTE: for a trail that FOLLOWS the projectile, embed it in the projectile prefab's [MainVisual] child instead. " +
                 "This effect does NOT move — it plays at the barrel tip and fades. " +
                 "Leave null if projectile VFX are fully handled by ProjectileComponent.")]
        [SerializeField] private GameObject projectileTrailPrefab;

        [Tooltip("Standalone muzzle flash at the weapon fire point. " +
                 "Assign VFX_MuzzleFlash_Template. " +
                 "This is spawned by SpawnMuzzleFlash() called from WeaponSystem — separate from the " +
                 "[MuzzleFlash] child embedded inside the projectile prefab (which fires with the projectile). " +
                 "Use this field when a lighter/quicker flash is needed independently of projectile spawning.")]
        [SerializeField] private GameObject muzzleFlashPrefab;

        [Header("Effect Lifetimes (seconds)")]
        [SerializeField, Min(0.01f)] private float damageEffectLifetime    = 2f;
        [SerializeField, Min(0.01f)] private float healEffectLifetime      = 2f;
        [SerializeField, Min(0.01f)] private float projectileTrailLifetime = 1f;
        [SerializeField, Min(0.01f)] private float muzzleFlashLifetime     = 0.1f;

        // -----------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (Instance == this) Instance = null;
        }

        // -----------------------------------------------------------------
        // Event bus wiring
        // -----------------------------------------------------------------

        private void SubscribeToEvents()
        {
            var bus = GameplayEventBus.Instance;
            if (bus == null) return;
            bus.Subscribe<DamageEffectEvent>(OnDamageEffect);
            bus.Subscribe<ProjectileSpawnEvent>(OnProjectileSpawn);
        }

        private void UnsubscribeFromEvents()
        {
            var bus = GameplayEventBus.Instance;
            if (bus == null) return;
            bus.Unsubscribe<DamageEffectEvent>(OnDamageEffect);
            bus.Unsubscribe<ProjectileSpawnEvent>(OnProjectileSpawn);
        }

        // -----------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------

        private void OnDamageEffect(DamageEffectEvent evt)
        {
            if (damageEffectPrefab == null) return;
            var rot = evt.HitDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(evt.HitDirection)
                : Quaternion.identity;
            PlayEffect(damageEffectPrefab, evt.HitPoint, rot, damageEffectLifetime);
        }

        private void OnProjectileSpawn(ProjectileSpawnEvent evt)
        {
            if (projectileTrailPrefab == null) return;
            var rot = evt.Direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(evt.Direction)
                : Quaternion.identity;
            PlayEffect(projectileTrailPrefab, evt.Position, rot, projectileTrailLifetime);
        }

        // -----------------------------------------------------------------
        // Public API (called directly for effects without a bus event)
        // -----------------------------------------------------------------

        public void SpawnMuzzleFlash(Vector3 position, Quaternion rotation)
            => PlayEffect(muzzleFlashPrefab, position, rotation, muzzleFlashLifetime);

        public void SpawnHealEffect(Vector3 position)
            => PlayEffect(healEffectPrefab, position, Quaternion.identity, healEffectLifetime);

        public void SpawnDamageEffect(Vector3 hitPoint, Vector3 hitDirection)
        {
            var rot = hitDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(hitDirection)
                : Quaternion.identity;
            PlayEffect(damageEffectPrefab, hitPoint, rot, damageEffectLifetime);
        }

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------

        private void PlayEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (prefab == null) return;

            var pool = SimpleEffectPool.Instance;
            if (pool != null)
            {
                pool.Play(prefab, position, rotation, lifetime);
                return;
            }

            // Fallback if pool is missing from scene (editor / test mode).
            Debug.LogWarning("[ClientEffectManager] SimpleEffectPool not found — falling back to Instantiate.", this);
            Destroy(Instantiate(prefab, position, rotation), lifetime);
        }

#if UNITY_EDITOR
        // -----------------------------------------------------------------
        // Editor — Context Menu: Create VFX Template Prefabs
        // -----------------------------------------------------------------

        [ContextMenu("NightHunt/Create VFX Template Prefabs")]
        private void Editor_CreateVFXTemplatePrefabs()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/VFX";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "VFX");

            bool changed = false;
            if (damageEffectPrefab == null)
            {
                damageEffectPrefab = Editor_CreateParticlePrefab(dir, "VFX_HitSpark_Template", 0.3f);
                changed = true;
            }
            if (healEffectPrefab == null)
            {
                healEffectPrefab = Editor_CreateParticlePrefab(dir, "VFX_HealBurst_Template", 0.6f);
                changed = true;
            }
            if (projectileTrailPrefab == null)
            {
                projectileTrailPrefab = Editor_CreateParticlePrefab(dir, "VFX_BulletTrail_Template", 0.25f);
                changed = true;
            }
            if (muzzleFlashPrefab == null)
            {
                muzzleFlashPrefab = Editor_CreateParticlePrefab(dir, "VFX_MuzzleFlash_Template", 0.08f);
                changed = true;
            }

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            Debug.Log("[ClientEffectManager] VFX template prefabs ready. " +
                      "Customize the ParticleSystem in each prefab (color, shape, rate) " +
                      "then ensure each is registered in SimpleEffectPool.");
        }

        private static GameObject Editor_CreateParticlePrefab(string dir, string prefabName, float lifetime)
        {
            string path = $"{dir}/{prefabName}.prefab";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                Debug.Log($"[ClientEffectManager] Prefab already exists — skipping: {path}");
                return existing;
            }

            var go   = new GameObject(prefabName);
            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop          = false;
            main.duration      = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed    = 3f;

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[ClientEffectManager] Created template prefab: {path}");
            return saved;
        }
#endif
    }
}

