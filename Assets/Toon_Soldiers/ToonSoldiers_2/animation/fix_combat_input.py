import os

root = r'w:\Unity\Shotter\NightHuntClient\Assets\_Night_Hunt\Scripts'
fp = os.path.join(root, r'Gameplay\Input\Handlers\Combat\CombatInputHandler.cs')

content = open(fp, encoding='utf-8').read()
original = content

replacements = [
    # Class summary fire flow
    (
        '///   PC:\n    ///     Mouse Down (LMB performed) \u2192 BeginFire()\n    ///       \u2192 Freeze camera (CameraStateManager.ForceState Locked)\n    ///       \u2192 Force STRAFE (MovementInputHandler.SetCameraLockOverride true)\n    ///     Mouse Move (while held)    \u2192 UpdateAimDirection() update _aimDirection\n    ///       \u2192 GetAimDirection() tr\u1ea3 v\u1ec1 _aimDirection (\u2260 zero v\u00ec _isFiring = true)\n    ///       \u2192 GatherInput() d\u00f9ng l\u00e0m _aimYaw \u2192 character xoay nh\u00ecn theo cursor\n    ///     Mouse Up (LMB canceled)    \u2192 EndFire()\n    ///       \u2192 Restore camera state\n    ///       \u2192 Restore movement lock state\n    ///       \u2192 GetAimDirection() tr\u1ea3 zero \u2192 _aimYaw fallback v\u1ec1 camera yaw',
        '///   PC:\n    ///     Mouse Down (LMB performed) \u2192 BeginFire()\n    ///       \u2192 Freeze camera (CameraStateManager.ForceState Locked)\n    ///       \u2192 Force STRAFE (MovementInputHandler.SetCameraLockOverride true)\n    ///     Mouse Move (while held)    \u2192 UpdateAimDirection() updates _aimDirection\n    ///       \u2192 GetAimDirection() returns _aimDirection (\u2260 zero because _isFiring = true)\n    ///       \u2192 GatherInput() uses it as _aimYaw \u2192 character rotates to face cursor\n    ///     Mouse Up (LMB canceled)    \u2192 EndFire()\n    ///       \u2192 Restore camera state\n    ///       \u2192 Restore movement lock state\n    ///       \u2192 GetAimDirection() returns zero \u2192 _aimYaw falls back to camera yaw'
    ),
    (
        '/// KEY RULE:\n    ///   GetAimDirection() tr\u1ea3 v\u1ec1 Vector3.zero khi KH\u00d4NG fire.\n    ///   \u0110i\u1ec1u n\u00e0y \u0111\u1ea3m b\u1ea3o GatherInput() kh\u00f4ng d\u00f9ng cursor direction khi ch\u01b0a b\u1eafn.',
        '/// KEY RULE:\n    ///   GetAimDirection() returns Vector3.zero when NOT firing.\n    ///   This ensures GatherInput() does not use the cursor direction when not shooting.'
    ),
    # Delegate comment
    (
        '// \u2500\u2500 Delegate fields (\u0111\u1ec3 unsubscribe \u0111\u00fang, tr\u00e1nh lambda leak) \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500',
        '// \u2500\u2500 Delegate fields (stored to allow correct unsubscription, avoids lambda leak) \u2500\u2500\u2500\u2500\u2500'
    ),
    # State comment
    (
        'private Vector3 _aimDirection;          // internal \u2014 lu\u00f4n track cursor',
        'private Vector3 _aimDirection;          // internal \u2014 always tracks cursor'
    ),
    # Mobile aim override block
    (
        '        /// True khi mobile FireButton \u0111ang drag.\n        /// Khi true, _aimDirection set b\u1edfi FireButton.OnDrag instead of mouse raycast.',
        '        /// True when the mobile FireButton is being dragged.\n        /// When true, _aimDirection is set by FireButton.OnDrag instead of mouse raycast.'
    ),
    (
        '        private Vector3 _mobileAimDirection;   // normalised \u2014 d\u00f9ng cho character rotation\n        private Vector2 _mobileJoystick01;     // raw [0,1] v\u1edbi magnitude \u2014 d\u00f9ng cho cursor placement',
        '        private Vector3 _mobileAimDirection;   // normalised \u2014 used for character rotation\n        private Vector2 _mobileJoystick01;     // raw [0,1] with magnitude \u2014 used for cursor placement'
    ),
    # Awake comment
    (
        '            // Camera.main c\u00f3 th\u1ec3 null \u1edf \u0111\u00e2y (player ch\u01b0a spawn).\n            // UpdateAimDirection() refresh m\u1ed7i frame \u2014 kh\u00f4ng rely v\u00e0o cache n\u00e0y.',
        '            // Camera.main may be null here (player not yet spawned).\n            // UpdateAimDirection() refreshes every frame \u2014 do not rely on this cache.'
    ),
    # Update comment
    (
        '            // UpdateAimDirection lu\u00f4n ch\u1ea1y \u0111\u1ec3 _aimDirection lu\u00f4n = v\u1ecb tr\u00ed cursor hi\u1ec7n t\u1ea1i.\n            // NH\u01afNG GetAimDirection() ch\u1ec9 expose value n\u00e0y khi _isFiring = true.\n            // \u2192 Khi kh\u00f4ng b\u1eafn: GatherInput() kh\u00f4ng nh\u1eadn aim direction \u2192 _aimYaw fallback v\u1ec1 camera yaw.\n            // \u2192 Khi b\u1eafn: GatherInput() nh\u1eadn aim direction \u2192 character xoay nh\u00ecn theo cursor.',
        '            // UpdateAimDirection always runs so _aimDirection always equals the current cursor position.\n            // BUT GetAimDirection() only exposes this value when _isFiring = true.\n            // \u2192 Not firing: GatherInput() receives no aim direction \u2192 _aimYaw falls back to camera yaw.\n            // \u2192 Firing:     GatherInput() receives aim direction \u2192 character rotates to face cursor.'
    ),
    # InitializeActions error
    (
        '                Debug.LogError("[CombatInputHandler] InputLayerManager.Instance l\u00e0 null!");',
        '                Debug.LogError("[CombatInputHandler] InputLayerManager.Instance is null!");'
    ),
    # EnableInput comment
    (
        '            // Fire: performed = mouse down, canceled = mouse up\n            // Press(behavior=2) trong .inputactions \u0111\u1ea3m b\u1ea3o c\u1ea3 2 event \u0111\u1ec1u fire \u0111\u00fang.',
        '            // Fire: performed = mouse down, canceled = mouse up\n            // Press(behavior=2) in .inputactions ensures both events fire correctly.'
    ),
    # DisableInput forced EndFire comment
    (
        '            // N\u1ebfu \u0111ang fire khi b\u1ecb disable (v\u00ed d\u1ee5 m\u1edf inventory), force EndFire \u0111\u1ec3 restore state.',
        '            // If firing when disabled (e.g., opening inventory), force EndFire to restore state.'
    ),
    # UpdateAimDirection summary
    (
        '        /// Lu\u00f4n update _aimDirection n\u1ed9i b\u1ed9 m\u1ed7i frame.\n        ///\n        /// PC:     Ground-plane raycast t\u1eeb mouse position.\n        /// Mobile: Direction set b\u1edfi FireButton.OnDrag via SetMobileAimDirection().\n        ///\n        /// QUAN TR\u1eccNG: H\u00e0m n\u00e0y ch\u1ec9 update internal state.\n        /// GetAimDirection() m\u1edbi l\u00e0 public API \u2014 n\u00f3 ch\u1ec9 expose direction khi \u0111ang fire.',
        '        /// Always updates _aimDirection internally each frame.\n        ///\n        /// PC:     Ground-plane raycast from mouse position.\n        /// Mobile: Direction set by FireButton.OnDrag via SetMobileAimDirection().\n        ///\n        /// IMPORTANT: This method only updates internal state.\n        /// GetAimDirection() is the public API \u2014 it only exposes the direction while firing.'
    ),
    # Mobile direction comment inside UpdateAimDirection
    (
        '                // Mobile: direction set b\u1edfi FireButton.OnDrag',
        '                // Mobile: direction set by FireButton.OnDrag'
    ),
    (
        '                // PC: lu\u00f4n refresh Camera.main \u0111\u1ec3 tr\u00e1nh stale reference (HOST spawn issue)',
        '                // PC: always refresh Camera.main to avoid stale reference (HOST spawn issue)'
    ),
    (
        '                // Read v\u1ecb tr\u00ed chu\u1ed9t t\u1eeb MousePosition action n\u1ebfu c\u00f3, fallback v\u1ec1 legacy Input',
        '                // Read mouse position from MousePosition action if available, fallback to legacy Input'
    ),
    # Push aim / weapon aim comments
    (
        '            // Lu\u00f4n push aim direction v\u00e0o WeaponSystem \u0111\u1ec3 \u0111\u1ea1n bay \u0111\u00fang h\u01b0\u1edbng.\n            // WeaponSystem c\u1ea7n direction ngay c\u1ea3 khi ch\u01b0a b\u1eafn (preview trajectory, v.v.)',
        '            // Always push aim direction to WeaponSystem so projectiles fly in the right direction.\n            // WeaponSystem needs the direction even before firing (preview trajectory, etc.).'
    ),
    (
        '            // Mobile MOBA cursor sync:\n            // Uses _mobileJoystick01 (gi\u1eef nguy\u00ean magnitude [0,1]) instead of _aimDirection (normalized).\n            // -> Cursor \u0111\u1eb7t t\u1ea1i player + joystickDir * joystickMagnitude * visionRange.\n            // -> joystick 50% ra \u0111\u01b0a cursor \u0111\u1ebfn 50% range \u2014 gi\u1ed1ng Mobile Legends.',
        '            // Mobile MOBA cursor sync:\n            // Uses _mobileJoystick01 (retains magnitude [0,1]) instead of _aimDirection (normalized).\n            // -> Cursor placed at player + joystickDir * joystickMagnitude * visionRange.\n            // -> Joystick at 50% places cursor at 50% of range \u2014 like Mobile Legends.'
    ),
    # BeginFire summary
    (
        '        /// B\u1eaft \u0111\u1ea7u b\u1eafn \u2014 called from Mouse Down (PC) ho\u1eb7c PointerDown (Mobile/Button).\n        ///\n        /// FLOW:\n        ///   1. Freeze camera \u2192 disable CinemachineInputAxisController\n        ///   2. Force STRAFE \u2192 character face cursor khi WASD\n        ///   3. T\u1eeb th\u1eddi \u0111i\u1ec3m n\u00e0y: GetAimDirection() \u2260 zero \u2192 GatherInput() d\u00f9ng cursor aim',
        '        /// Begin firing \u2014 called from Mouse Down (PC) or PointerDown (Mobile/Button).\n        ///\n        /// FLOW:\n        ///   1. Freeze camera \u2192 disable CinemachineInputAxisController\n        ///   2. Force STRAFE \u2192 character faces cursor while pressing WASD\n        ///   3. From this point: GetAimDirection() \u2260 zero \u2192 GatherInput() uses cursor aim'
    ),
    # EndFire summary
    (
        '        /// Stop b\u1eafn \u2014 called from Mouse Up (PC) ho\u1eb7c PointerUp (Mobile/Button).\n        ///\n        /// FLOW:\n        ///   1. GetAimDirection() tr\u1ea3 zero \u2192 GatherInput() fallback v\u1ec1 camera yaw\n        ///   2. Restore movement lock state\n        ///   3. Restore camera state (re-enable CinemachineInputAxisController n\u1ebfu tr\u01b0\u1edbc \u0111\u00f3 Free)',
        '        /// Stop firing \u2014 called from Mouse Up (PC) or PointerUp (Mobile/Button).\n        ///\n        /// FLOW:\n        ///   1. GetAimDirection() returns zero \u2192 GatherInput() falls back to camera yaw\n        ///   2. Restore movement lock state\n        ///   3. Restore camera state (re-enable CinemachineInputAxisController if previously Free)'
    ),
    # EndFire step comments
    (
        '            // \u2500\u2500 B\u01b0\u1edbc 1: _isFiring = false \u2192 GetAimDirection() tr\u1ea3 zero \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n            // GatherInput() Priority 1 fail \u2192 fallback v\u1ec1 camera yaw\n            // \u2192 character no longer b\u1ecb k\u00e9o nh\u00ecn theo cursor',
        '            // \u2500\u2500 Step 1: _isFiring = false \u2192 GetAimDirection() returns zero \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n            // GatherInput() Priority 1 fails \u2192 falls back to camera yaw\n            // \u2192 Character no longer forced to look toward cursor'
    ),
    (
        '            // \u2500\u2500 B\u01b0\u1edbc 2: Restore movement lock state \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500',
        '            // \u2500\u2500 Step 2: Restore movement lock state \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500'
    ),
    (
        '            // \u2500\u2500 B\u01b0\u1edbc 3: Restore camera state \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n            // N\u1ebfu tr\u01b0\u1edbc \u0111\u00f3 Free \u2192 re-enable CinemachineInputAxisController \u2192 camera xoay t\u1ef1 do l\u1ea1i\n            // N\u1ebfu tr\u01b0\u1edbc \u0111\u00f3 Locked \u2192 gi\u1eef nguy\u00ean Locked',
        '            // \u2500\u2500 Step 3: Restore camera state \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n            // Previously Free \u2192 re-enable CinemachineInputAxisController \u2192 camera rotates freely again\n            // Previously Locked \u2192 keep Locked'
    ),
    # GetAimDirection summary
    (
        '        /// Tr\u1ea3 v\u1ec1 aim direction CH\u1ec8 KHI \u0111ang fire (LMB held ho\u1eb7c mobile button held).\n        ///\n        /// Tr\u1ea3 v\u1ec1 Vector3.zero khi kh\u00f4ng fire.\n        /// \u2192 GatherInput() Priority 1 s\u1ebd fail \u2192 _aimYaw fallback v\u1ec1 camera yaw\n        /// \u2192 Character kh\u00f4ng b\u1ecb k\u00e9o nh\u00ecn theo cursor khi ch\u01b0a b\u1eafn.\n        ///\n        /// \u0110\u00e2y l\u00e0 KEY RULE c\u1ee7a to\u00e0n b\u1ed9 aim system.',
        '        /// Returns the aim direction ONLY while firing (LMB held or mobile button held).\n        ///\n        /// Returns Vector3.zero when not firing.\n        /// \u2192 GatherInput() Priority 1 fails \u2192 _aimYaw falls back to camera yaw\n        /// \u2192 Character is not forced to look toward cursor when not shooting.\n        ///\n        /// This is the KEY RULE of the entire aim system.'
    ),
    (
        '            // Ch\u1ec9 expose direction khi \u0111ang fire ho\u1eb7c mobile aim active',
        '            // Only expose direction when firing or when mobile aim is active'
    ),
    # SetMobileAimDirection summary
    (
        '        /// Override aim direction t\u1eeb mobile drag (FireButton.OnDrag).\n        ///\n        /// Call v\u1edbi active=true + dir = drag world direction khi \u0111ang drag.\n        /// Call v\u1edbi active=false khi finger lift \u0111\u1ec3 revert v\u1ec1 mouse raycast.\n        ///\n        /// NOTE: _mobileAimActive = true c\u0169ng khi\u1ebfn GetAimDirection() expose direction\n        /// ngay c\u1ea3 khi _isFiring = false \u2014 \u0111i\u1ec1u n\u00e0y cho ph\u00e9p preview aim before b\u1eafn.',
        '        /// Override aim direction from mobile drag (FireButton.OnDrag).\n        ///\n        /// Call with active=true + dir = drag world direction while dragging.\n        /// Call with active=false on finger lift to revert to mouse raycast.\n        ///\n        /// NOTE: _mobileAimActive = true also makes GetAimDirection() expose the direction\n        /// even when _isFiring = false \u2014 this allows aim preview before shooting.'
    ),
    # BindCombatSystems summary
    (
        '        /// Bind t\u1ea5t c\u1ea3 refs c\u1ea7n thi\u1ebft cho fire flow.\n        /// Call m\u1ed9t l\u1ea7n after local player spawn (trong NetworkPlayer.EnableInput).\n        ///\n        /// Params:\n        ///   movementInputHandler  \u2014 \u0111\u1ec3 force STRAFE khi b\u1eafn\n        ///   weaponSystem          \u2014 \u0111\u1ec3 push aim direction v\u00e0o weapon\n        ///   playerTransform       \u2014 origin cho ground-plane aim raycast\n        ///   cameraStateManager    \u2014 \u0111\u1ec3 freeze/unfreeze camera khi b\u1eafn',
        '        /// Bind all refs required for the fire flow.\n        /// Call once after local player spawns (inside NetworkPlayer.EnableInput).\n        ///\n        /// Params:\n        ///   movementInputHandler  \u2014 to force STRAFE while firing\n        ///   weaponSystem          \u2014 to push aim direction into the weapon\n        ///   playerTransform       \u2014 origin for ground-plane aim raycast\n        ///   cameraStateManager    \u2014 to freeze/unfreeze the camera while firing'
    ),
    # prev camera lock comment
    (
        '        /// <summary>Camera lock state before fire \u2014 restored khi EndFire.</summary>',
        '        /// <summary>Camera lock state before firing \u2014 restored when EndFire is called.</summary>'
    ),
    (
        '        /// <summary>Camera state before fire \u2014 restored khi EndFire.</summary>',
        '        /// <summary>Camera state before firing \u2014 restored when EndFire is called.</summary>'
    ),
]

changed = 0
for old, new in replacements:
    if old in content:
        content = content.replace(old, new)
        changed += 1
    else:
        print(f'NOT FOUND: {old[:60]!r}')

if changed:
    open(fp, 'w', encoding='utf-8').write(content)
    print(f'CombatInputHandler.cs: {changed} replacements')
else:
    print('No replacements made')
