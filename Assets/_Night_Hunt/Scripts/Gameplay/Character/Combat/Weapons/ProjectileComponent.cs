using UnityEngine;
using NightHunt.Data;
namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Projectile component for visual projectile
    /// Handles movement and collision
    /// </summary>
    public class ProjectileComponent : MonoBehaviour
    {
        private WeaponConfigData weaponConfig;
        private Vector3 direction;
        private float speed;
        private float lifetime;
        private bool useHitscanLogic;
        private float distanceTraveled;

        public void Initialize(WeaponConfigData config, Vector3 dir, bool useHitscan)
        {
            weaponConfig = config;
            direction = dir.normalized;
            speed = config.ProjectileSpeed;
            lifetime = config.MaxRange / speed;
            useHitscanLogic = useHitscan;
            distanceTraveled = 0f;
        }

        private void Update()
        {
            // Move projectile
            Vector3 movement = direction * speed * Time.deltaTime;
            
            // Apply gravity if projectile type
            if (weaponConfig != null && weaponConfig.BallisticType == "Projectile")
            {
                movement.y -= weaponConfig.GravityScale * 9.81f * Time.deltaTime;
            }

            transform.position += movement;
            distanceTraveled += movement.magnitude;

            // Update direction based on movement
            if (movement.magnitude > 0.001f)
            {
                direction = movement.normalized;
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // Lifetime check
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f || distanceTraveled >= weaponConfig.MaxRange)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (useHitscanLogic)
            {
                // Hitscan logic already processed, just destroy visual
                Destroy(gameObject);
                return;
            }

            // Collider-based collision
            // var character = other.GetComponent<PlayerStats>();
            // if (character != null)
            // {
            //     float damage = weaponConfig.DamageBody;
            //     bool isHeadshot = other.CompareTag("Head");
            //     if (isHeadshot)
            //     {
            //         damage *= weaponConfig.DamageHeadMul;
            //     }
            //
            //     // Send RPC to server for damage application once server-authoritative combat is added.
            //     character.TakeDamage(damage);
            // }

            Destroy(gameObject);
        }
    }
}

