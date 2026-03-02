# WEAPON TYPES COMPARISON & ADVANCED SETUP

---

## Hitscan vs Projectile Weapons

### 📊 Comparison Table

| Feature | Hitscan | Projectile |
|---------|---------|-----------|
| **Visual** | Instant ray (laser) | Visible bullet travel |
| **Feel** | Arcade/shooter | Realistic/ballistic |
| **Network** | Instant RPC | Spawn sync required |
| **Complexity** | ⭐ Simple | ⭐⭐⭐ Complex |
| **Accuracy** | 100% ray trace | Physics simulation |
| **Good For** | Rifles, instant weapons | Grenades, slow bullets |
| **Damage Apply** | Instant on server | On impact/timeout |

---

## Setup Comparison

### ProjectileWeapon (Current)

**When to use:**
- Grenades with throw arc
- Slow rockets
- Visible bullet effects
- Server-side validation needed

**Setup:**
```
Weapon GameObject
├─ ProjectileWeapon script
├─ FirePoint (at muzzle)
├─ visualProjectilePrefab (Bullet prefab)
└─ ProjectileSpawner (auto-added)

Bullet prefab
├─ MeshRenderer (visible)
├─ Collider (isTrigger)
└─ ProjectileComponent
```

**Fire sequence:**
```
ProjectileWeapon.Fire()
  ├─ Instantiate visual at FirePoint
  ├─ ProjectileComponent.Initialize()
  ├─ Send to server via ServerRpc
  └─ Server broadcasts to all clients
```

---

### HitscanWeapon (Alternative)

**When to use:**
- Instant weapons (rifles, pistols)
- Perfect accuracy needed
- No travel time
- Instant feedback

**Script location:** `Assets/_Night_Hunt/Scripts/Gameplay/Character/Combat/Weapons/HitscanWeapon.cs`

**Setup:**
```
Weapon GameObject
├─ HitscanWeapon script
├─ FirePoint (at muzzle)
├─ hitLayers (what to hit)
└─ (NO ProjectileSpawner needed)

No prefab needed for visual
```

**Fire sequence:**
```
HitscanWeapon.Fire()
  ├─ Raycast from FirePoint to max distance
  ├─ Check for hits on hitLayers
  ├─ Apply damage immediately
  ├─ Show hit effect at impact point
  └─ Send damage RPC to server
```

---

## Code Comparison

### ProjectileWeapon Code

```csharp
public class ProjectileWeapon : WeaponBase
{
    [SerializeField] private GameObject visualProjectilePrefab;
    [SerializeField] private bool useHitscanForLogic = false;
    [SerializeField] private LayerMask hitLayers = -1;

    public override void Fire(Vector3 direction)
    {
        if (!CanFire()) return;

        lastFireTime = Time.time;
        currentAmmo--;

        Vector3 startPos = firePoint.position;

        // 1. Spawn visual projectile
        if (visualProjectilePrefab != null)
        {
            SpawnVisualProjectile(startPos, direction);
        }

        // 2. Send to server for validation
        if (IsOwner)
        {
            SendProjectileToServer(startPos, direction);
        }

        // 3. Optional: hitscan for logic
        if (useHitscanForLogic)
        {
            ProcessHitscanLogic(startPos, direction);
        }
    }

    private void SpawnVisualProjectile(Vector3 position, Vector3 direction)
    {
        GameObject projectile = Instantiate(
            visualProjectilePrefab,
            position,
            Quaternion.LookRotation(direction)
        );

        var projectileComponent = projectile.GetComponent<ProjectileComponent>();
        if (projectileComponent != null)
        {
            projectileComponent.Initialize(weaponConfig, direction, useHitscanForLogic);
        }
    }
}
```

---

### HitscanWeapon Code (Reference)

```csharp
public class HitscanWeapon : WeaponBase
{
    [SerializeField] private LayerMask hitLayers = -1;
    [SerializeField] private float maxRange = 100f;

    public override void Fire(Vector3 direction)
    {
        if (!CanFire()) return;

        lastFireTime = Time.time;
        currentAmmo--;

        Vector3 startPos = firePoint != null ? firePoint.position : transform.position;

        // Raycast from fire point
        RaycastHit hit;
        if (Physics.Raycast(startPos, direction, out hit, maxRange, hitLayers))
        {
            // Hit something
            ProcessHit(hit, weaponConfig.DamageBody);

            // Send to server
            SendHitToServer(hit.point, hit.normal, hit.collider.name);
        }
        else
        {
            // Miss - just send direction
            SendMissToServer(startPos, direction);
        }

        // Show muzzle flash here
        ShowMuzzleFlash();
    }

    private void ProcessHit(RaycastHit hit, float damage)
    {
        // Apply damage, show effect
        var targetHealth = hit.collider.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }

        // Show hit particle
        Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
    }

    [ServerRpc]
    private void SendHitToServer(Vector3 hitPoint, Vector3 hitNormal, string hitObject)
    {
        // Server validates and broadcasts to other clients
        BroadcastHitToClients(hitPoint, hitNormal);
    }

    [ObserversRpc]
    private void BroadcastHitToClients(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!IsOwner)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
        }
    }
}
```

---

## Advanced ProjectileWeapon Variations

### Variation 1: Hitscan-based Logic

For bullets that should NOT have visual travel delay:

```csharp
// In ProjectileWeapon inspector:
_useHitscanForLogic = true  // ← Enable hitscan damage

// In Fire():
// 1. Visual spawns instantly
// 2. Damage calculated via raycast
// 3. Projectile despawns when hit (not timeout)
```

**Use case:** Sniper rifle with bullet-time view

---

### Variation 2: Collision-based Logic

For projectiles that use collider detection:

```csharp
// In ProjectileWeapon inspector:
_useHitscanForLogic = false  // ← Disable hitscan

// In ProjectileComponent.OnTriggerEnter():
private void OnTriggerEnter(Collider other)
{
    if (useHitscanLogic)
    {
        Destroy(gameObject);
        return;
    }

    // Collider-based collision
    var character = other.GetComponent<Health>();
    if (character != null)
    {
        character.TakeDamage(weaponConfig.DamageBody);
    }

    Destroy(gameObject);
}
```

**Use case:** Grenade with physics bounce

---

### Variation 3: Ballistic (Gravity) Projectile

For grenades that arc:

```csharp
// In WeaponConfigData:
public enum BallisticType { Hitscan, Projectile, Ballistic }
public BallisticType BallisticType = BallisticType.Ballistic;
public float GravityScale = 1.0f;  // 0 = no gravity
public float LaunchAngle = 45f;    // Throw angle

// In ProjectileComponent.Update():
private void Update()
{
    Vector3 movement = direction * speed * Time.deltaTime;
    
    // Apply gravity if ballistic
    if (weaponConfig.BallisticType == "Ballistic" || 
        weaponConfig.BallisticType == "Projectile")
    {
        movement.y -= weaponConfig.GravityScale * 9.81f * Time.deltaTime;
    }

    transform.position += movement;
    
    // Update direction for visual
    if (movement.magnitude > 0.001f)
    {
        direction = movement.normalized;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    lifetime -= Time.deltaTime;
    if (lifetime <= 0)
    {
        // Explode grenade
        ExplodeGrenade();
        Destroy(gameObject);
    }
}

private void ExplodeGrenade()
{
    // Area damage
    Collider[] colliders = Physics.OverlapSphere(
        transform.position,
        weaponConfig.ExplosionRadius
    );
    foreach (var collider in colliders)
    {
        var health = collider.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(weaponConfig.ExplosionDamage);
        }
    }

    // Explode effect
    Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
}
```

---

## Hybrid Weapon System

To support BOTH weapon types on the same player:

```csharp
public abstract class WeaponBase : MonoBehaviour
{
    public enum WeaponType { Hitscan, Projectile }
    
    [SerializeField] protected WeaponType weaponType;
    
    public static WeaponBase CreateWeapon(WeaponConfigData config, GameObject weaponGO)
    {
        switch (config.WeaponType)
        {
            case WeaponType.Hitscan:
                return weaponGO.AddComponent<HitscanWeapon>();
            
            case WeaponType.Projectile:
                return weaponGO.AddComponent<ProjectileWeapon>();
            
            default:
                return weaponGO.AddComponent<ProjectileWeapon>();
        }
    }
}
```

---

## Inspector Presets (Copy-Paste Values)

### Rifle (Hitscan-style, instant hit)
```
ProjectileWeapon
├─ weaponConfig: RifleConfig
├─ firePoint: [muzzle]
├─ visualProjectilePrefab: null (or flash prefab)
├─ useHitscanForLogic: true  ← Hitscan damage
└─ hitLayers: Enemy | Environment
```

### Grenade (Projectile with gravity)
```
ProjectileWeapon
├─ weaponConfig: GrenadeConfig
├─ firePoint: [hand]
├─ visualProjectilePrefab: GrenadePrefab
├─ useHitscanForLogic: false  ← Physics collision
└─ hitLayers: Everything
```

### Rocket (Projectile no gravity)
```
ProjectileWeapon
├─ weaponConfig: RocketConfig
├─ firePoint: [launcher]
├─ visualProjectilePrefab: RocketPrefab
├─ useHitscanForLogic: false  ← Physics collision
└─ hitLayers: Everything
```

---

## Network Optimization

### For Projectile Weapons

**Current:** Every projectile sends ServerRpc (heavy)

**Optimized Solution:**

```csharp
// ProjectileSpawner
[ServerRpc]
private void SendProjectileToServer(Vector3 position, Vector3 direction, uint weaponConfigId)
{
    // Server validation
    if (!IsValidShot(position, direction)) return;
    
    // Broadcast compact data instead of complex object
    BroadcastProjectileToClients(position, direction, weaponConfigId);
}

[ObserversRpc]
private void BroadcastProjectileToClients(Vector3 pos, Vector3 dir, uint configId)
{
    // Other clients spawn with minimal data
    if (!IsOwner)
    {
        GameObject projectile = Instantiate(projectilePrefab, pos, Quaternion.LookRotation(dir));
        var component = projectile.GetComponent<ProjectileComponent>();
        component.Initialize(GetConfigFromId(configId), dir, false);
    }
}
```

---

## Testing Different Weapon Types

### Test Scene Setup

```csharp
public class WeaponTestBootstrap : MonoBehaviour
{
    [SerializeField] private Transform rifleSpawnPoint;
    [SerializeField] private Transform grenadeSpawnPoint;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            SpawnTestRifle();
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            SpawnTestGrenade();
        }
    }

    private void SpawnTestRifle()
    {
        var rifle = Instantiate(Resources.Load("Weapons/Rifle"), rifleSpawnPoint);
        var fire = rifle.GetComponent<ProjectileWeapon>();
        fire.Fire(Vector3.forward);
    }

    private void SpawnTestGrenade()
    {
        var grenade = Instantiate(Resources.Load("Weapons/Grenade"), grenadeSpawnPoint);
        var fire = grenade.GetComponent<ProjectileWeapon>();
        fire.Fire((Vector3.forward + Vector3.up).normalized);
    }
}
```

---

## Recommended Project Structure

```
Assets/_Night_Hunt/Scripts/Gameplay/Character/Combat/Weapons/
├─ WeaponBase.cs              (Abstract)
├─ ProjectileWeapon.cs        (Visible bullet)
├─ HitscanWeapon.cs           (Raycast instant)
├─ ProjectileComponent.cs     (Bullet behavior)
├─ ProjectileSpawner.cs       (Network sync)
├─ ProjectileSync.cs          (Data struct)
└─ Effects/
   ├─ MuzzleFlash.cs
   ├─ HitEffect.cs
   └─ ExplosionEffect.cs
```

---

## Next Steps (Advanced)

1. **Server Authoritative Damage**
   - Server validates all hits
   - Client sends only direction/position
   - Server applies damage and broadcasts

2. **Bullet Pooling**
   - Pre-allocate projectile objects
   - Reuse instead of destroy/instantiate
   - Improves performance on high volume

3. **Lag Compensation**
   - Server-side position extrapolation
   - Client prediction for local view
   - Network latency smoothing

4. **Audio/Visual Effects**
   - Muzzle flash
   - Bullet trails
   - Impact effects
   - Shell casings

5. **Weapon Attachments**
   - Silencers (damage modifier)
   - Scopes (crosshair change)
   - Extended mags (capacity increase)

---

**Version:** 1.1 (Advanced)  
**Last Updated:** March 2026
