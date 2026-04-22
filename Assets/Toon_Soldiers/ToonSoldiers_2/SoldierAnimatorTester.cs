using System.Collections;
using UnityEngine;

/// <summary>
/// Standalone test driver for SoldierAnimatorController.
/// Attach to the character GameObject that has an Animator component.
/// NO NightHunt / FishNet dependency.
///
/// ---- FIX LOG ----
/// [BUG FIX] WeaponType bị evaluate TRƯỚC khi SetInteger hoàn tất trong cùng frame.
///   → Dùng Coroutine: SetInteger trước → WaitForEndOfFrame → SetTrigger sau.
///   → Thêm ResetTrigger để xóa trigger cũ còn tồn đọng (stale trigger).
///   → Bỏ PushAllToAnimator ghi đè WeaponType mỗi frame (chỉ ghi khi thay đổi).
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
/// F / LMB       : Shoot  — nếu weapon=Knife (WeaponType=4): gọi Attack trigger + random AttackIndex thay vì Shoot
/// B / MMB       : ShootBurst (bị bỏ qua khi Knife)
/// R (hold)      : ShootLoop (bool) — Heavy / Machinegun; bị bỏ qua khi Knife
/// T             : Reload (trigger)
/// E             : Draw (trigger — cũng auto-fire khi Equip)
/// N             : ThrowGrenade (trigger)
/// I             : Interact (cycles A/B; bị Shoot interrupt)
/// H             : TakeDamage (trigger)
/// X             : Die (trigger + random DeathIndex, Death layer weight=1)
/// Y             : Respawn (trigger, Death layer weight=0)
/// O (toggle)    : ShootBolt bool (Infantry — bắn bolt action)
/// P (toggle)    : ShootShotgun bool (Infantry — bắn shotgun)
/// K             : Attack Knife (cycles A→B→A)
///
/// ---- WEAPON SWITCH ----
/// 0 / 7         : Holster (WeaponType=0, Unarmed, không Draw)
/// 1-6           : Equip vũ khí đó + auto Draw
///                 Nhấn lại cùng số  → Holster (toggle off)
///                 Nhấn số khác      → Swap (WeaponChanged + Draw mới)
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
    static readonly int H_WeaponType         = Animator.StringToHash("WeaponType");
    static readonly int H_VelocityX          = Animator.StringToHash("VelocityX");
    static readonly int H_VelocityY          = Animator.StringToHash("VelocityY");
    static readonly int H_Speed              = Animator.StringToHash("Speed");
    static readonly int H_IsCrouching        = Animator.StringToHash("IsCrouching");
    static readonly int H_IsProne            = Animator.StringToHash("IsProne");
    static readonly int H_IsGuard            = Animator.StringToHash("IsGuard");
    static readonly int H_IsSprinting        = Animator.StringToHash("IsSprinting");
    static readonly int H_IsGrounded         = Animator.StringToHash("IsGrounded");
    static readonly int H_IsOnLadder         = Animator.StringToHash("IsOnLadder");
    static readonly int H_ShootLoop          = Animator.StringToHash("ShootLoop");
    static readonly int H_ShootBolt          = Animator.StringToHash("ShootBolt");
    static readonly int H_ShootShotgun       = Animator.StringToHash("ShootShotgun");
    static readonly int H_DeathIndex         = Animator.StringToHash("DeathIndex");
    static readonly int H_InteractIndex      = Animator.StringToHash("InteractIndex");
    static readonly int H_AttackIndex        = Animator.StringToHash("AttackIndex");

    static readonly int H_Shoot              = Animator.StringToHash("Shoot");
    static readonly int H_ShootBurst         = Animator.StringToHash("ShootBurst");
    static readonly int H_Reload             = Animator.StringToHash("Reload");
    static readonly int H_Draw               = Animator.StringToHash("Draw");
    static readonly int H_Holster            = Animator.StringToHash("Holster");
    static readonly int H_ThrowGrenade       = Animator.StringToHash("ThrowGrenade");
    static readonly int H_Interact           = Animator.StringToHash("Interact");
    static readonly int H_TakeDamage         = Animator.StringToHash("TakeDamage");
    static readonly int H_Attack             = Animator.StringToHash("Attack");
    static readonly int H_Die                = Animator.StringToHash("Die");
    static readonly int H_Roll               = Animator.StringToHash("Roll");
    static readonly int H_Respawn            = Animator.StringToHash("Respawn");
    static readonly int H_WeaponChanged      = Animator.StringToHash("WeaponChanged");
    static readonly int H_WeaponChangedUB    = Animator.StringToHash("WeaponChangedUB");
    static readonly int H_WeaponChangedDeath = Animator.StringToHash("WeaponChangedDeath");

    // ---- FIX: Flag chặn Equip/Holster spam trong lúc coroutine đang chạy ----
    private bool _isSwitching = false;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        _prevStateHash = new int[3];
    }

    int[] _prevStateHash;

    void LateUpdate()
    {
        for (int i = 0; i < 3; i++)
        {
            var info = _anim.GetCurrentAnimatorStateInfo(i);
            // Use fullPathHash to distinguish states with same name in different sub-machines
            if (info.fullPathHash != _prevStateHash[i])
            {
                // Get clip info for definitive confirmation
                var clips = _anim.GetCurrentAnimatorClipInfo(i);
                string clipName = clips.Length > 0 ? clips[0].clip.name : "(no clip)";
                Debug.Log($"[State] L{i} {LAYER_NAMES[i]}: fullPath=#{info.fullPathHash:X8}  short=#{info.shortNameHash:X8}  loop={info.loop}  CLIP=[{clipName}]");
                _prevStateHash[i] = info.fullPathHash;
            }
        }
    }

    void Update()
    {
        HandleWeaponSwitch();
        HandleMovement();
        HandleStanceToggles();
        HandleCombatTriggers();
        PushMovementToAnimator();  // FIX: Tách riêng — chỉ push movement, KHÔNG push WeaponType mỗi frame
    }

    // ──────────────────────────────────────────────────────────────────────────
    // WEAPON SWITCH
    // ──────────────────────────────────────────────────────────────────────────
    void HandleWeaponSwitch()
    {
        // FIX: Chặn input trong lúc đang switch để tránh stale trigger chồng nhau
        if (_isSwitching) return;

        int pressed = -1;
        if (Input.GetKeyDown(KeyCode.Alpha0)) pressed = 0;
        if (Input.GetKeyDown(KeyCode.Alpha1)) pressed = 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) pressed = 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) pressed = 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) pressed = 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) pressed = 5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) pressed = 6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) pressed = 0;

        if (pressed < 0) return;

        if (pressed == 0)
        {
            if (weaponType == 0) return;
            StartCoroutine(HolsterCoroutine());
        }
        else if (pressed == weaponType)
        {
            StartCoroutine(HolsterCoroutine());
        }
        else
        {
            StartCoroutine(EquipCoroutine(pressed));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FIX CORE: Dùng Coroutine để đảm bảo thứ tự:
    //   1. ResetTrigger (xóa trigger cũ còn tồn đọng)
    //   2. SetInteger WeaponType
    //   3. WaitForEndOfFrame (cho Animator đọc param mới xong)
    //   4. SetTrigger WeaponChanged (lúc này Animator mới evaluate transition với WeaponType đúng)
    //   5. SetTrigger Draw (nếu Equip)
    //
    // Nếu KHÔNG dùng coroutine: SetInteger và SetTrigger cùng frame
    // → Animator evaluate transition VỚI WeaponType CŨ → vào sai SM
    // ──────────────────────────────────────────────────────────────────────────
    IEnumerator HolsterCoroutine()
    {
        _isSwitching = true;
        int prevWeapon = weaponType;

        Debug.Log($"[Weapon] HOLSTER: {prevWeapon} -> 0 | Step1: Fire Holster trigger (play draw backward)");

        // Bước 1: Xóa trigger cũ tồn đọng
        ClearWeaponTriggers();

        // Bước 2: Fire Holster trigger (UB_Empty → Holster_Stand/Crouch/Prone, speed=-1)
        if (prevWeapon != 0)
            _anim.SetTrigger(H_Holster);

        // Bước 3: Chờ holster animation hoàn thành (~0.4s)
        yield return new WaitForSeconds(0.4f);

        // Bước 4: Đổi weapon type sang Unarmed
        weaponType = 0;
        _anim.SetInteger(H_WeaponType, 0);
        yield return new WaitForEndOfFrame();

        // Bước 5: Fire trigger SAU khi WeaponType đã được ghi nhận
        Debug.Log($"[Weapon] HOLSTER: Step2: SetTrigger WeaponChanged/UB/Death | Animator.WeaponType={_anim.GetInteger(H_WeaponType)}");
        _anim.SetTrigger(H_WeaponChanged);
        _anim.SetTrigger(H_WeaponChangedUB);
        _anim.SetTrigger(H_WeaponChangedDeath);
        // Unarmed không có Draw

        _isSwitching = false;
    }

    IEnumerator EquipCoroutine(int newWeapon)
    {
        _isSwitching = true;
        int prevWeapon = weaponType;
        weaponType = newWeapon;

        Debug.Log($"[Weapon] EQUIP: {prevWeapon} -> {newWeapon} | Step1: SetInteger WeaponType={newWeapon}");

        // Bước 1: Xóa trigger cũ tồn đọng
        ClearWeaponTriggers();

        // Bước 2: Set parameter trước
        _anim.SetInteger(H_WeaponType, newWeapon);

        // Bước 3: Chờ frame kết thúc — Animator đọc param mới
        yield return new WaitForEndOfFrame();

        // Bước 4: Fire trigger SAU khi WeaponType đã được ghi nhận
        Debug.Log($"[Weapon] EQUIP: Step2: SetTrigger WeaponChanged/UB/Death+Draw | Animator.WeaponType={_anim.GetInteger(H_WeaponType)}");
        _anim.SetTrigger(H_WeaponChanged);
        _anim.SetTrigger(H_WeaponChangedUB);
        _anim.SetTrigger(H_WeaponChangedDeath);
        _anim.SetTrigger(H_Draw);

        _isSwitching = false;
    }

    /// <summary>
    /// FIX: Xóa tất cả weapon-related trigger còn tồn đọng từ lần switch trước.
    /// Stale trigger là nguyên nhân khiến Animator fire transition sai ngay sau khi
    /// quay về Entry, dẫn đến vào Handgun dù WeaponType=2.
    /// </summary>
    void ClearWeaponTriggers()
    {
        _anim.ResetTrigger(H_WeaponChanged);
        _anim.ResetTrigger(H_WeaponChangedUB);
        _anim.ResetTrigger(H_WeaponChangedDeath);
        _anim.ResetTrigger(H_Draw);
        _anim.ResetTrigger(H_Holster);
        Debug.Log("[Weapon] ClearWeaponTriggers: tất cả trigger weapon đã reset");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MOVEMENT  WASD + Shift
    // ──────────────────────────────────────────────────────────────────────────
    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        isSprinting = Input.GetKey(KeyCode.LeftShift) && v > 0.1f && !isCrouching && !isProne;

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

        speed = Mathf.Clamp(new Vector2(velocityX, velocityY).magnitude, 0f, 2f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // STANCE TOGGLES
    // ──────────────────────────────────────────────────────────────────────────
    void HandleStanceToggles()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            if (isCrouching) isProne = false;
            Debug.Log($"[Stance] Crouch={isCrouching}");
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            isProne = !isProne;
            if (isProne) isCrouching = false;
            Debug.Log($"[Stance] Prone={isProne}");
        }
        if (Input.GetKeyDown(KeyCode.G))
        { isGuard = !isGuard; Debug.Log($"[Stance] Guard={isGuard}"); }

        if (Input.GetKeyDown(KeyCode.Space))
        { isGrounded = !isGrounded; Debug.Log($"[Stance] Grounded={isGrounded}"); }

        if (Input.GetKeyDown(KeyCode.L))
        {
            isOnLadder = !isOnLadder;
            if (isOnLadder) { isCrouching = false; isProne = false; }
            Debug.Log($"[Stance] OnLadder={isOnLadder}");
        }
    }

    // WeaponType==4 is Knife (melee) — uses Attack trigger instead of Shoot.
    bool IsMeleeEquipped => weaponType == 4;

    // ──────────────────────────────────────────────────────────────────────────
    // COMBAT TRIGGERS
    // ──────────────────────────────────────────────────────────────────────────
    void HandleCombatTriggers()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        { Debug.Log("[Trigger] Roll"); _anim.SetTrigger(H_Roll); }

        if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0))
        {
            if (IsMeleeEquipped)
            {
                // Knife: random A/B combo, no Shoot trigger
                _attackIndex = Random.Range(0, 2);
                _anim.SetInteger(H_AttackIndex, _attackIndex);
                Debug.Log($"[Trigger] Knife Attack  AttackIndex={_attackIndex}");
                _anim.SetTrigger(H_Attack);
            }
            else
            {
                Debug.Log($"[Trigger] Shoot (weapon={weaponType})");
                _anim.SetTrigger(H_Shoot);
            }
        }

        if (Input.GetKeyDown(KeyCode.B) || Input.GetMouseButtonDown(2))
        {
            if (!IsMeleeEquipped)   // Knife has no burst fire
            { Debug.Log($"[Trigger] ShootBurst (weapon={weaponType})"); _anim.SetTrigger(H_ShootBurst); }
        }

        bool prevShootLoop = shootLoop;
        shootLoop = !IsMeleeEquipped && (Input.GetKey(KeyCode.R) || Input.GetMouseButton(1));
        if (shootLoop != prevShootLoop)
            Debug.Log($"[Bool] ShootLoop={shootLoop}");

        if (Input.GetKeyDown(KeyCode.T))
        { Debug.Log($"[Trigger] Reload (weapon={weaponType})"); _anim.SetTrigger(H_Reload); }

        if (Input.GetKeyDown(KeyCode.E))
        { Debug.Log($"[Trigger] Draw (weapon={weaponType})"); _anim.SetTrigger(H_Draw); }

        if (Input.GetKeyDown(KeyCode.N))
        { Debug.Log("[Trigger] ThrowGrenade"); _anim.SetTrigger(H_ThrowGrenade); }

        if (Input.GetKeyDown(KeyCode.I))
        {
            _interactIndex = (_interactIndex + 1) % 2;
            _anim.SetInteger(H_InteractIndex, _interactIndex);
            Debug.Log($"[Trigger] Interact  InteractIndex={_interactIndex}");
            _anim.SetTrigger(H_Interact);
        }

        if (Input.GetKeyDown(KeyCode.H))
        { Debug.Log("[Trigger] TakeDamage"); _anim.SetTrigger(H_TakeDamage); }

        if (Input.GetKeyDown(KeyCode.K))
        {
            _attackIndex = (_attackIndex + 1) % 2;
            _anim.SetInteger(H_AttackIndex, _attackIndex);
            Debug.Log($"[Trigger] Attack (Knife)  AttackIndex={_attackIndex}");
            _anim.SetTrigger(H_Attack);
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            _deathIndex = Random.Range(0, 5);
            _anim.SetInteger(H_DeathIndex, _deathIndex);
            _anim.SetLayerWeight(2, 1f);
            Debug.Log($"[Trigger] Die  DeathIndex={_deathIndex}  weapon={weaponType}  Death layer weight=1");
            _anim.SetTrigger(H_Die);
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            isCrouching = false;
            isProne     = false;
            isGuard     = false;
            isGrounded  = true;
            _anim.SetLayerWeight(2, 0f);
            Debug.Log("[Trigger] Respawn  Death layer weight=0");
            _anim.SetTrigger(H_Respawn);
        }

        if (Input.GetKeyDown(KeyCode.O))
        { bool v = !_anim.GetBool(H_ShootBolt); Debug.Log($"[Bool] ShootBolt={v}"); _anim.SetBool(H_ShootBolt, v); }
        if (Input.GetKeyDown(KeyCode.P))
        { bool v = !_anim.GetBool(H_ShootShotgun); Debug.Log($"[Bool] ShootShotgun={v}"); _anim.SetBool(H_ShootShotgun, v); }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FIX: Tách PushAllToAnimator thành PushMovementToAnimator
    // KHÔNG push WeaponType ở đây nữa — WeaponType chỉ được set trong coroutine
    // để tránh ghi đè làm mất sync với trigger
    // ──────────────────────────────────────────────────────────────────────────
    void PushMovementToAnimator()
    {
        _anim.SetFloat(H_VelocityX,   velocityX,   0.1f, Time.deltaTime);
        _anim.SetFloat(H_VelocityY,   velocityY,   0.1f, Time.deltaTime);
        _anim.SetFloat(H_Speed,       speed,        0.1f, Time.deltaTime);
        _anim.SetBool (H_IsCrouching, isCrouching);
        _anim.SetBool (H_IsProne,     isProne);
        _anim.SetBool (H_IsGuard,     isGuard);
        _anim.SetBool (H_IsSprinting, isSprinting);
        _anim.SetBool (H_IsGrounded,  isGrounded);
        _anim.SetBool (H_IsOnLadder,  isOnLadder);
        _anim.SetBool (H_ShootLoop,   shootLoop);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ANIMATION EVENTS
    // ──────────────────────────────────────────────────────────────────────────
    void OnFireBullet()     => Debug.Log("[AnimEvent] OnFireBullet");
    void OnFootstep()       => Debug.Log("[AnimEvent] OnFootstep");
    void OnReloadOut()      => Debug.Log("[AnimEvent] OnReloadOut");
    void OnReloadIn()       => Debug.Log("[AnimEvent] OnReloadIn");
    void OnReloadComplete() => Debug.Log("[AnimEvent] OnReloadComplete");
    void OnDrawWeapon()     => Debug.Log("[AnimEvent] OnDrawWeapon");
    void OnReleaseGrenade() => Debug.Log("[AnimEvent] OnReleaseGrenade");
    void OnMeleeHit()       => Debug.Log("[AnimEvent] OnMeleeHit");

    // ──────────────────────────────────────────────────────────────────────────
    // ON-SCREEN DEBUG
    // ──────────────────────────────────────────────────────────────────────────
    static readonly string[] LAYER_NAMES = { "Base Layer", "UpperBody", "Death" };

    void OnGUI()
    {
        if (!Application.isPlaying) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize  = 14,
            alignment = TextAnchor.UpperLeft
        };
        style.normal.textColor = Color.white;

        float x = 10, y = 10, w = 520, lh = 20;
        GUI.Box(new Rect(x - 4, y - 4, w + 8, lh * 20 + 8), "");

        GUI.Label(new Rect(x, y, w, lh), "=== SOLDIER ANIMATOR STATE ===", style); y += lh;

        // FIX status
        var fixStyle = new GUIStyle(style);
        fixStyle.normal.textColor = _isSwitching ? Color.yellow : Color.green;
        GUI.Label(new Rect(x, y, w, lh),
            _isSwitching ? "⚠ SWITCHING... (input locked)" : "✓ READY", fixStyle);
        y += lh;

        for (int i = 0; i < 3; i++)
        {
            float weight = _anim.GetLayerWeight(i);
            var cur  = _anim.GetCurrentAnimatorStateInfo(i);
            var next = _anim.GetNextAnimatorStateInfo(i);
            bool trans = _anim.IsInTransition(i);

            string curName  = cur.shortNameHash  == 0 ? "(none)" : $"#{cur.shortNameHash:X8}";
            string nextName = next.shortNameHash == 0 ? "" : $" -> #{next.shortNameHash:X8}";
            string transStr = trans ? $"  [TRANSITIONING{nextName}]" : "";

            GUI.Label(new Rect(x, y, w, lh),
                $"L{i} {LAYER_NAMES[i]} (w={weight:F2}): {curName}  t={cur.normalizedTime:F2}{transStr}", style);
            y += lh;
        }

        y += 4;

        // FIX: Hiển thị cả giá trị C# và giá trị thực tế trong Animator để dễ so sánh
        var animWT = _anim.GetInteger(H_WeaponType);
        var wtStyle = new GUIStyle(style);
        wtStyle.normal.textColor = (weaponType == animWT) ? Color.white : Color.red;
        GUI.Label(new Rect(x, y, w, lh),
            $"WeaponType: C#={weaponType}  Animator={animWT} {(weaponType != animWT ? "← MISMATCH!" : "✓")}",
            wtStyle);
        y += lh;

        GUI.Label(new Rect(x, y, w, lh),
            $"InteractIdx={_interactIndex}  AttackIdx={_attackIndex}  DeathIdx={_deathIndex}", style);
        y += lh;
        GUI.Label(new Rect(x, y, w, lh),
            $"Vel=({velocityX:F2},{velocityY:F2})  Speed={speed:F2}  Sprint={isSprinting}", style);
        y += lh;
        GUI.Label(new Rect(x, y, w, lh),
            $"Crouch={isCrouching}  Prone={isProne}  Guard={isGuard}  Grounded={isGrounded}  Ladder={isOnLadder}", style);
        y += lh;
        GUI.Label(new Rect(x, y, w, lh),
            $"ShootLoop={shootLoop}  ShootBolt={_anim.GetBool(H_ShootBolt)}  ShootShotgun={_anim.GetBool(H_ShootShotgun)}", style);
        y += lh;

        y += 4;
        style.fontSize = 12;
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(x, y, w, lh), "1-6=Equip  0/7=Holster  F=Shoot(Knife:Attack)  T=Reload  E=Draw  N=Grenade", style); y += lh;
        GUI.Label(new Rect(x, y, w, lh), "I=Interact  H=TakeDmg  X=Die  Y=Respawn  Q=Roll  K=KnifeExplicit  B=Burst(no Knife)", style); y += lh;
        GUI.Label(new Rect(x, y, w, lh), "C=Crouch  Z=Prone  G=Guard  Space=Jump  L=Ladder  R(hold)=ShootLoop(no Knife)", style);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GIZMO
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