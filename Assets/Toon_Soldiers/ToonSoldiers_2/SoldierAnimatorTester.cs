using UnityEngine;

/// <summary>
/// Standalone test driver for SoldierAnimatorController.
/// Attach to the character GameObject that has an Animator component.
/// NO NightHunt / FishNet dependency.
///
/// ---- CONTROLS ----
/// WASD          : Move (VelocityX/Y)
/// Shift (hold)  : Sprint
/// C (toggle)    : Crouch
/// Z (toggle)    : Prone
/// G (toggle)    : Guard / ADS
/// Space         : Jump (toggle IsGrounded)
/// L (toggle)    : Ladder
/// Q             : Roll (trigger)
/// F             : Shoot (trigger)
/// B             : ShootBurst (trigger)
/// R (hold)      : ShootLoop (bool)
/// T             : Reload (trigger)
/// E             : Draw (trigger)
/// N             : ThrowGrenade (trigger)
/// I             : Interact (cycles A/B)
/// H             : TakeDamage (trigger)
/// X             : Die (trigger + random DeathIndex)
/// Y             : Respawn (trigger)
/// 0 / 7         : Unarmed / Holster (WeaponType=0 — cất súng)
/// 1             : Handgun    (WeaponType=1)
/// 2             : Infantry   (WeaponType=2)
/// 3             : Heavy      (WeaponType=3)
/// 4             : Knife      (WeaponType=4)
/// 5             : Machinegun (WeaponType=5)
/// 6             : RocketLauncher (WeaponType=6)
/// K (Knife)     : Attack (cycles A/B)
/// </summary>
[RequireComponent(typeof(Animator))]
public class SoldierAnimatorTester : MonoBehaviour
{
    [Header("Speed Settings")]
    public float walkSpeed = 1f;
    public float runSpeed  = 2f;

    [Header("Current State (read-only preview)")]
    [SerializeField] private int   weaponType   = 0;
    [SerializeField] private float velocityX    = 0f;
    [SerializeField] private float velocityY    = 0f;
    [SerializeField] private float speed        = 0f;
    [SerializeField] private bool  isCrouching  = false;
    [SerializeField] private bool  isProne      = false;
    [SerializeField] private bool  isGuard      = false;
    [SerializeField] private bool  isSprinting  = false;
    [SerializeField] private bool  isGrounded   = true;
    [SerializeField] private bool  isOnLadder   = false;
    [SerializeField] private bool  shootLoop    = false;

    private Animator _anim;
    private int _interactIndex = 0;
    private int _attackIndex   = 0;
    private int _deathIndex    = 0;

    // ---- Animator param hashes ----
    static readonly int H_WeaponType    = Animator.StringToHash("WeaponType");
    static readonly int H_VelocityX     = Animator.StringToHash("VelocityX");
    static readonly int H_VelocityY     = Animator.StringToHash("VelocityY");
    static readonly int H_Speed         = Animator.StringToHash("Speed");
    static readonly int H_IsCrouching   = Animator.StringToHash("IsCrouching");
    static readonly int H_IsProne       = Animator.StringToHash("IsProne");
    static readonly int H_IsGuard       = Animator.StringToHash("IsGuard");
    static readonly int H_IsSprinting   = Animator.StringToHash("IsSprinting");
    static readonly int H_IsGrounded    = Animator.StringToHash("IsGrounded");
    static readonly int H_IsOnLadder    = Animator.StringToHash("IsOnLadder");
    static readonly int H_ShootLoop     = Animator.StringToHash("ShootLoop");
    static readonly int H_ShootBolt     = Animator.StringToHash("ShootBolt");
    static readonly int H_ShootShotgun  = Animator.StringToHash("ShootShotgun");
    static readonly int H_DeathIndex    = Animator.StringToHash("DeathIndex");
    static readonly int H_InteractIndex = Animator.StringToHash("InteractIndex");
    static readonly int H_AttackIndex   = Animator.StringToHash("AttackIndex");

    static readonly int H_Shoot         = Animator.StringToHash("Shoot");
    static readonly int H_ShootBurst    = Animator.StringToHash("ShootBurst");
    static readonly int H_Reload        = Animator.StringToHash("Reload");
    static readonly int H_Draw          = Animator.StringToHash("Draw");
    static readonly int H_ThrowGrenade  = Animator.StringToHash("ThrowGrenade");
    static readonly int H_Interact      = Animator.StringToHash("Interact");
    static readonly int H_TakeDamage    = Animator.StringToHash("TakeDamage");
    static readonly int H_Attack        = Animator.StringToHash("Attack");
    static readonly int H_Die            = Animator.StringToHash("Die");
    static readonly int H_Roll           = Animator.StringToHash("Roll");
    static readonly int H_Respawn        = Animator.StringToHash("Respawn");
    static readonly int H_WeaponChanged  = Animator.StringToHash("WeaponChanged");

    void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    void Update()
    {
        HandleWeaponSwitch();
        HandleMovement();
        HandleStanceToggles();
        HandleCombatTriggers();
        PushAllToAnimator();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WEAPON SWITCH  0-6  (fires WeaponChanged trigger once per switch)
    // ──────────────────────────────────────────────────────────────────────────
    void HandleWeaponSwitch()
    {
        int next = weaponType;
        if (Input.GetKeyDown(KeyCode.Alpha0)) next = 0; // Unarmed (Holster)
        if (Input.GetKeyDown(KeyCode.Alpha1)) next = 1; // Handgun
        if (Input.GetKeyDown(KeyCode.Alpha2)) next = 2; // Infantry
        if (Input.GetKeyDown(KeyCode.Alpha3)) next = 3; // Heavy
        if (Input.GetKeyDown(KeyCode.Alpha4)) next = 4; // Knife
        if (Input.GetKeyDown(KeyCode.Alpha5)) next = 5; // Machinegun
        if (Input.GetKeyDown(KeyCode.Alpha6)) next = 6; // RocketLauncher
        if (Input.GetKeyDown(KeyCode.Alpha7)) next = 0; // Unarmed (Holster) — alias

        if (next != weaponType)
        {
            weaponType = next;
            // Fire ONCE — AnyState in current sub-machine catches this trigger
            // and exits cleanly. Trigger is consumed immediately, no spam.
            _anim.SetTrigger(H_WeaponChanged);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MOVEMENT  WASD + Shift
    // ──────────────────────────────────────────────────────────────────────────
    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal"); // A/D
        float v = Input.GetAxis("Vertical");   // W/S

        isSprinting = Input.GetKey(KeyCode.LeftShift) && v > 0.1f && !isCrouching && !isProne;

        float targetMult = isSprinting ? runSpeed : (Mathf.Abs(h) + Mathf.Abs(v) > 0.05f ? walkSpeed : 0f);
        // On ladder use Speed only
        if (isOnLadder)
        {
            velocityX = 0f;
            velocityY = Mathf.Lerp(velocityY, v * walkSpeed, Time.deltaTime * 8f);
        }
        else
        {
            velocityX = Mathf.Lerp(velocityX, h * (isSprinting ? runSpeed : walkSpeed), Time.deltaTime * 10f);
            velocityY = Mathf.Lerp(velocityY, v * (isSprinting ? runSpeed : walkSpeed), Time.deltaTime * 10f);
        }

        // Speed = magnitude clamped 0-2  (used by Guard blend tree)
        speed = Mathf.Clamp(new Vector2(velocityX, velocityY).magnitude, 0f, 2f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // STANCE TOGGLES  C Z G Space L
    // ──────────────────────────────────────────────────────────────────────────
    void HandleStanceToggles()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            if (isCrouching) isProne = false;
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            isProne = !isProne;
            if (isProne) isCrouching = false;
        }
        if (Input.GetKeyDown(KeyCode.G))
            isGuard = !isGuard;

        if (Input.GetKeyDown(KeyCode.Space))
            isGrounded = !isGrounded; // simulate jump

        if (Input.GetKeyDown(KeyCode.L))
        {
            isOnLadder = !isOnLadder;
            if (isOnLadder) { isCrouching = false; isProne = false; }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // COMBAT TRIGGERS
    // ──────────────────────────────────────────────────────────────────────────
    void HandleCombatTriggers()
    {
        // Roll
        if (Input.GetKeyDown(KeyCode.Q))
            _anim.SetTrigger(H_Roll);

        // Shoot single — F hoặc Left Mouse Button
        if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
            _anim.SetTrigger(H_Shoot);

        // Shoot burst — B hoặc Middle Mouse Button
        if (Input.GetKeyDown(KeyCode.B) || Input.GetMouseButtonDown(2))
            _anim.SetTrigger(H_ShootBurst);

        // Shoot loop (hold R hoặc hold Right Mouse) — Heavy / Machinegun
        shootLoop = Input.GetKey(KeyCode.R) || Input.GetMouseButton(1);

        // Reload
        if (Input.GetKeyDown(KeyCode.T))
            _anim.SetTrigger(H_Reload);

        // Draw
        if (Input.GetKeyDown(KeyCode.E))
            _anim.SetTrigger(H_Draw);

        // Grenade
        if (Input.GetKeyDown(KeyCode.N))
            _anim.SetTrigger(H_ThrowGrenade);

        // Interact A/B (toggle each press)
        if (Input.GetKeyDown(KeyCode.I))
        {
            _interactIndex = (_interactIndex + 1) % 2;
            _anim.SetInteger(H_InteractIndex, _interactIndex);
            _anim.SetTrigger(H_Interact);
        }

        // TakeDamage
        if (Input.GetKeyDown(KeyCode.H))
            _anim.SetTrigger(H_TakeDamage);

        // Attack (Knife) A/B
        if (Input.GetKeyDown(KeyCode.K))
        {
            _attackIndex = (_attackIndex + 1) % 2;
            _anim.SetInteger(H_AttackIndex, _attackIndex);
            _anim.SetTrigger(H_Attack);
        }

        // Die — random death index
        if (Input.GetKeyDown(KeyCode.X))
        {
            _deathIndex = Random.Range(0, 5);
            _anim.SetInteger(H_DeathIndex, _deathIndex);
            _anim.SetTrigger(H_Die);
        }

        // Respawn — Y key
        if (Input.GetKeyDown(KeyCode.Y))
        {
            isCrouching = false;
            isProne     = false;
            isGuard     = false;
            isGrounded  = true;
            _anim.SetTrigger(H_Respawn);
        }

        // Infantry-specific: ShootBolt / ShootShotgun toggles
        if (Input.GetKeyDown(KeyCode.O))
            _anim.SetBool(H_ShootBolt, !_anim.GetBool(H_ShootBolt));
        if (Input.GetKeyDown(KeyCode.P))
            _anim.SetBool(H_ShootShotgun, !_anim.GetBool(H_ShootShotgun));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUSH  — ghi tất cả state values vào Animator mỗi frame
    // ──────────────────────────────────────────────────────────────────────────
    void PushAllToAnimator()
    {
        _anim.SetInteger(H_WeaponType,   weaponType);
        _anim.SetFloat  (H_VelocityX,    velocityX,  0.1f, Time.deltaTime);
        _anim.SetFloat  (H_VelocityY,    velocityY,  0.1f, Time.deltaTime);
        _anim.SetFloat  (H_Speed,        speed,      0.1f, Time.deltaTime);
        _anim.SetBool   (H_IsCrouching,  isCrouching);
        _anim.SetBool   (H_IsProne,      isProne);
        _anim.SetBool   (H_IsGuard,      isGuard);
        _anim.SetBool   (H_IsSprinting,  isSprinting);
        _anim.SetBool   (H_IsGrounded,   isGrounded);
        _anim.SetBool   (H_IsOnLadder,   isOnLadder);
        _anim.SetBool   (H_ShootLoop,    shootLoop);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ANIMATION EVENTS  — được Unity gọi từ animation clip tại frame cụ thể
    // Đặt tên method khớp với tên event trong clip (Animation window → Events)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Gọi tại frame bắn đạn (Shoot / ShootBurst / ShootLoop clips)</summary>
    void OnFireBullet()     => Debug.Log("[AnimEvent] OnFireBullet");

    /// <summary>Gọi khi bước chân chạm đất (locomotion clips)</summary>
    void OnFootstep()       => Debug.Log("[AnimEvent] OnFootstep");

    /// <summary>Gọi khi tháo clip/đạn ra (Reload clip — đầu animation)</summary>
    void OnReloadOut()      => Debug.Log("[AnimEvent] OnReloadOut");

    /// <summary>Gọi khi lắp clip/đạn vào (Reload clip — giữa animation)</summary>
    void OnReloadIn()       => Debug.Log("[AnimEvent] OnReloadIn");

    /// <summary>Gọi khi hoàn thành reload, sẵn sàng bắn lại</summary>
    void OnReloadComplete() => Debug.Log("[AnimEvent] OnReloadComplete");

    /// <summary>Gọi khi súng/vũ khí hiện ra trong tay (Draw clip)</summary>
    void OnDrawWeapon()     => Debug.Log("[AnimEvent] OnDrawWeapon");

    /// <summary>Gọi đúng lúc tung lựu đạn ra (ThrowGrenade clip)</summary>
    void OnReleaseGrenade() => Debug.Log("[AnimEvent] OnReleaseGrenade");

    /// <summary>Gọi tại điểm đánh trúng của combo dao (Attack clip)</summary>
    void OnMeleeHit()       => Debug.Log("[AnimEvent] OnMeleeHit");

    // ──────────────────────────────────────────────────────────────────────────
    // GIZMO — hiển thị velocity vector trong Scene view
    // ──────────────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        var dir = new Vector3(velocityX, 0f, velocityY).normalized;
        Gizmos.DrawRay(transform.position + Vector3.up, dir);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.1f);
    }
}
