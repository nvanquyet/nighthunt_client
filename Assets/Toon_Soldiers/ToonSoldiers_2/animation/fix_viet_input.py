import os

root = r'w:\Unity\Shotter\NightHuntClient\Assets\_Night_Hunt\Scripts'

fixes = {
    r'Gameplay\GameplaySystems\World\Core\WorldSpawnConfig.cs': [
        (
            '[Tooltip("Lo\u1ea1i object s\u1ebd spawn t\u1ea1i \u0111i\u1ec3m n\u00e0y:\n" +\n                 "  Item      \u2192 WorldItem (item r\u01a1i \u0111\u1ea5t, scatter quanh \u0111i\u1ec3m)\n" +\n                 "  Container \u2192 WorldContainer (th\u00f9ng / crate / r\u01b0\u01a1ng / chest)")]',
            '[Tooltip("The type of object to spawn at this point:\\n  Item      \u2192 WorldItem (ground drop, scattered around the point)\\n  Container \u2192 WorldContainer (crate / chest / loot box)")]'
        ),
        (
            '[Tooltip("B\u1ea3ng t\u1ef7 l\u1ec7 item. Roll on spawn (Item) ho\u1eb7c khi m\u1edf (Container/Chest).")]',
            '[Tooltip("Item loot table. Rolled on spawn (Item) or when opened (Container/Chest).")]'
        ),
        (
            '[Tooltip("Spawn-point n\u00e0y c\u00f3 spawn l\u1ea1i after b\u1ecb loot/despawn kh\u00f4ng?\n" +\n                 "  false \u2192 ch\u1ec9 spawn 1 l\u1ea7n duy nh\u1ea5t (one-shot)\n" +\n                 "  true  \u2192 spawn l\u1ea1i sau RespawnTime")]',
            '[Tooltip("Whether this spawn point respawns after being looted/despawned.\\n  false \u2192 one-shot spawn only\\n  true  \u2192 respawns after RespawnTime")]'
        ),
        (
            '[Tooltip("Th\u1eddi gian ch\u1edd (gi\u00e2y) tr\u01b0\u1edbc on spawn l\u1ea1i.\n" +\n                 "Ch\u1ec9 d\u00f9ng khi CanRespawn = true.")]',
            '[Tooltip("Delay in seconds before respawning. Only used when CanRespawn = true.")]'
        ),
        (
            '[Tooltip("S\u1ed1 l\u1ea7n spawn t\u1ed1i \u0111a t\u1ea1i \u0111i\u1ec3m n\u00e0y.\n" +\n                 "  0 = kh\u00f4ng gi\u1edbi h\u1ea1n (\u221e)\n" +\n                 "  > 0 = after \u0111\u1ea1t gi\u1edbi h\u1ea1n, \u0111i\u1ec3m n\u00e0y ng\u01b0ng v\u0129nh vi\u1ec5n.\n" +\n                 "Ch\u1ec9 d\u00f9ng khi CanRespawn = true.")]',
            '[Tooltip("Maximum number of respawns at this point.\\n  0 = unlimited\\n  > 0 = stops permanently after reaching the limit.\\nOnly used when CanRespawn = true.")]'
        ),
        (
            '[Tooltip("S\u1ed1 l\u01b0\u1ee3ng object t\u1ed1i \u0111a \u0111\u01b0\u1ee3c active t\u1eeb spawn-point n\u00e0y c\u00f9ng l\u00fac.\n" +\n                 "Th\u01b0\u1eddng l\u00e0 1 cho Container/Chest. C\u00f3 th\u1ec3 > 1 cho Item scatter.")]',
            '[Tooltip("Maximum number of objects active from this spawn point simultaneously.\\nTypically 1 for Container/Chest; may be > 1 for item scatter.")]'
        ),
        (
            '[Header("Item Scatter \u2014 ch\u1ec9 d\u00f9ng khi SpawnType = Item")]',
            '[Header("Item Scatter \u2014 SpawnType = Item only")]'
        ),
        (
            '[Tooltip("B\u00e1n k\u00ednh (meters) \u0111\u1ec3 scatter c\u00e1c WorldItem xung quanh \u0111i\u1ec3m spawn.")]',
            '[Tooltip("Radius (meters) to scatter WorldItems around the spawn point.")]'
        ),
        (
            '[Header("Container / Chest \u2014 ch\u1ec9 d\u00f9ng khi SpawnType = Container/Chest")]',
            '[Header("Container / Chest \u2014 SpawnType = Container/Chest only")]'
        ),
        (
            '[Tooltip("Container / Chest b\u1ecb kh\u00f3a ngay on spawn.\n" +\n                 "Player c\u1ea7n key ho\u1eb7c unlock logic \u0111\u1ec3 m\u1edf.")]',
            '[Tooltip("Container / Chest spawns locked. Player needs a key or unlock logic to open it.")]'
        ),
        (
            '[Tooltip("Container / Chest c\u00f3 t\u1ef1 reset tr\u1ea1ng th\u00e1i after \u0111\u00e3 b\u1ecb loot kh\u00f4ng?\n" +\n                 "  false \u2192 \u0111\u00e3 loot x\u1eadp th\u00ec c\u1ea7n respawn m\u1edbi\n" +\n                 "  true  \u2192 sau ContainerResetDelay gi\u00e2y, r\u01b0\u01a1ng \'reset\' l\u1ea1i (c\u00f3 th\u1ec3 m\u1edf l\u1ea1i, roll loot m\u1edbi)")]',
            '[Tooltip("Whether the container auto-resets after being looted.\\n  false \u2192 requires a fresh respawn\\n  true  \u2192 resets after ContainerResetDelay seconds (can be opened again with new loot)")]'
        ),
    ],
    r'Gameplay\Input\Core\InputLayerManager.cs': [
        (
            '/// SINGLE SOURCE OF TRUTH cho to\u00e0n b\u1ed9 Input.',
            '/// Single source of truth for all input layer management.'
        ),
        (
            '/// Nguy\u00ean t\u1eafc:',
            '/// Rules:'
        ),
        (
            '///   \u2022 Ch\u1ec9 class n\u00e0y \u0111\u01b0\u1ee3c Enable/Disable ActionMap.',
            '///   \u2022 Only this class may Enable/Disable ActionMaps.'
        ),
        (
            '///   \u2022 C\u00e1c handler KH\u00d4NG t\u1ef1 g\u1ecdi map.Enable() / map.Disable().',
            '///   \u2022 Handlers must NOT call map.Enable() / map.Disable() directly.'
        ),
        (
            '///   \u2022 Uses <see cref="PushContext"/> / <see cref="PopContext"/> \u0111\u1ec3 chuy\u1ec3n state,',
            '///   \u2022 Use <see cref="PushContext"/> / <see cref="PopContext"/> to switch state'
        ),
        (
            '///     instead of g\u1ecdi <see cref="TransitionToState"/> tr\u1ef1c ti\u1ebfp t\u1eeb nhi\u1ec1u n\u01a1i.',
            '///     instead of calling <see cref="TransitionToState"/> from multiple places.'
        ),
        (
            '/// B\u1ea3ng preset Context \u2192 Layer:',
            '/// Context \u2192 Layer preset table:'
        ),
        (
            "                    Debug.LogWarning($\"[InputLayerManager] ActionMap '{map.name}' not available mapping \u2192 b\u1ecf qua.\");",
            "                    Debug.LogWarning($\"[InputLayerManager] ActionMap '{map.name}' has no layer mapping \u2014 skipping.\");"
        ),
        (
            "                    Debug.LogWarning($\"[InputLayerManager] Kh\u00f4ng t\u00ecm th\u1ea5y ActionMap '{pair.Key}' trong asset.\");",
            "                    Debug.LogWarning($\"[InputLayerManager] ActionMap '{pair.Key}' not found in asset.\");"
        ),
        (
            '/// Layer \u0111ang active hi\u1ec7n t\u1ea1i (bitwise OR).',
            '/// Currently active layers (bitwise OR).'
        ),
        (
            '/// Context hi\u1ec7n t\u1ea1i.',
            '/// Current input context.'
        ),
        (
            '/// Stack \u0111\u1ec3 h\u1ed7 tr\u1ee3 Push/Pop (v\u00ed d\u1ee5: m\u1edf map trong inventory \u2192 pop v\u1ec1 inventory).',
            '/// Stack for Push/Pop support (e.g., opening the map while in inventory \u2192 pop back to inventory).'
        ),
        (
            '/// Fired khi context thay \u0111\u1ed5i. (oldState, newState)',
            '/// Fired when the context changes. (oldState, newState)'
        ),
        (
            '/// Fired khi c\u00e1c layer active thay \u0111\u1ed5i.',
            '/// Fired when the active layers change.'
        ),
        (
            '// ── Legacy cached refs (public accessors cho code c\u0169) ────────────────────',
            '// \u2500\u2500 Legacy cached refs (public accessors for older code) \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500'
        ),
        (
            '/// Chuy\u1ec3n context, x\u00f3a to\u00e0n b\u1ed9 stack history.',
            '/// Transition to a new context, clearing the entire stack history.'
        ),
        (
            '/// Push context m\u1edbi, l\u01b0u context c\u0169 v\u00e0o stack \u0111\u1ec3 <see cref="PopContext"/> kh\u00f4i ph\u1ee5c.',
            '/// Push a new context, saving the current one on the stack for <see cref="PopContext"/> to restore.'
        ),
        (
            '/// <para>V\u00ed d\u1ee5: Gameplay \u2192 PushContext(InventoryOpen) \u2192 PopContext() \u2192 Gameplay.</para>',
            '/// <para>Example: Gameplay \u2192 PushContext(InventoryOpen) \u2192 PopContext() \u2192 Gameplay.</para>'
        ),
        (
            '/// Pop v\u1ec1 context tr\u01b0\u1edbc \u0111\u00f3. N\u1ebfu stack empty \u2192 fallback <see cref="InputState.PlayerAlive"/>.',
            '/// Pop to the previous context. If the stack is empty, falls back to <see cref="InputState.PlayerAlive"/>.'
        ),
        (
            '// ─────────────────────────────────────────────────────────────────────────\n        #region Layer API \u2013 th\u1ee7 c\u00f4ng fine-tune',
            '// \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n        #region Layer API \u2013 manual fine-tuning'
        ),
        (
            '/// B\u1eadt/t\u1eaft th\u1ee7 c\u00f4ng m\u1ed9t layer ngo\u00e0i preset context.',
            '/// Enable or disable a layer outside the preset context.'
        ),
        (
            '/// Uses when needed tweak (VD: t\u1ea1m t\u1eaft Camera nh\u01b0ng gi\u1eef nguy\u00ean context Gameplay).',
            '/// Use when a tweak is needed (e.g., temporarily disable Camera while keeping the Gameplay context).'
        ),
        (
            '/// Ki\u1ec3m tra layer c\u00f3 \u0111ang active kh\u00f4ng.',
            '/// Check whether a layer is currently active.'
        ),
        (
            '/// Disable t\u1ea5t c\u1ea3 input (d\u00f9ng cho Cinematic / Loading screen).',
            '/// Disable all input (use for Cinematic / Loading screens).'
        ),
        (
            '/// Legacy: tr\u1ea3 v\u1ec1 state hi\u1ec7n t\u1ea1i.',
            '/// Legacy: returns the current state.'
        ),
        (
            '/// Legacy: l\u1ea5y ActionMap theo t\u00ean.',
            '/// Legacy: get ActionMap by name.'
        ),
        (
            '/// Legacy: l\u1ea5y Action theo t\u00ean map v\u00e0 t\u00ean action.',
            '/// Legacy: get Action by map name and action name.'
        ),
        (
            '/// Legacy: check map c\u00f3 enabled kh\u00f4ng.',
            '/// Legacy: check whether a map is enabled.'
        ),
        (
            "            if (!ContextPresets.TryGetValue(state, out var layers))\n            {\n                Debug.LogWarning($\"[InputLayerManager] Kh\u00f4ng c\u00f3 preset cho state '{state}' \u2192 disable all\");",
            "            if (!ContextPresets.TryGetValue(state, out var layers))\n            {\n                Debug.LogWarning($\"[InputLayerManager] No preset for state '{state}' \u2014 disabling all input\");"
        ),
        (
            '            // Lu\u00f4n gi\u1eef Debug layer b\u1eadt trong Editor',
            '            // Always keep the Debug layer enabled in Editor.'
        ),
        (
            '// Di chuy\u1ec3n + Camera, KH\u00d4NG combat',
            '// Move + Camera, NO combat'
        ),
    ],
    r'Gameplay\Input\Core\InputState.cs': [
        (
            '//  InputLayer \u2013 Flags enum, m\u1ed7i bit = 1 ActionMap\n    //  Uses \u0111\u1ec3 OR c\u00e1c map l\u1ea1i th\u00e0nh context preset',
            '//  InputLayer \u2013 Flags enum; each bit represents one ActionMap.\n    //  Combine with bitwise OR to form context presets.'
        ),
        (
            '    /// C\u00e1c t\u1ea7ng input t\u01b0\u01a1ng \u1ee9ng v\u1edbi t\u1eebng ActionMap trong InputSystem_Actions.\n    /// C\u00f3 th\u1ec3 combine b\u1eb1ng bitwise OR.',
            '    /// Input layers corresponding to each ActionMap in InputSystem_Actions.\n    /// Combine with bitwise OR.'
        ),
        (
            '    //  InputState \u2013 Context c\u1ee7a game, \u00e1nh x\u1ea1 t\u1edbi t\u1ed5 h\u1ee3p InputLayer',
            '    //  InputState \u2013 Current game context, mapped to a combination of InputLayer flags.'
        ),
        (
            '    /// Tr\u1ea1ng th\u00e1i gameplay t\u1ed5ng th\u1ec3, m\u1ed7i state c\u00f3 m\u1ed9t preset InputLayer.',
            '    /// Overall gameplay state; each state maps to a preset InputLayer combination.'
        ),
        (
            '    /// <para>Uses <see cref="Core.InputLayerManager.PushContext"/> /\n    /// <see cref="Core.InputLayerManager.PopContext"/> \u0111\u1ec3 chuy\u1ec3n \u0111\u1ed5i.</para>',
            '    /// <para>Use <see cref="Core.InputLayerManager.PushContext"/> /\n    /// <see cref="Core.InputLayerManager.PopContext"/> to switch states.</para>'
        ),
        (
            '    /// <summary>To\u00e0n b\u1ed9 gameplay b\u00ecnh th\u01b0\u1eddng (Player + Combat + Camera + ...)</summary>',
            '    /// <summary>Normal gameplay (Player + Combat + Camera + ...).</summary>'
        ),
        (
            '    /// <summary>\u0110ang m\u1edf Inventory / Equipment \u2013 t\u1eaft Combat + Player movement</summary>',
            '    /// <summary>Inventory / Equipment open \u2014 Combat and Player movement disabled.</summary>'
        ),
        (
            '    /// <summary>\u0110ang xem b\u1ea3n \u0111\u1ed3 \u2013 ch\u1ec9 Camera + UI</summary>',
            '    /// <summary>Map view \u2014 Camera + UI only.</summary>'
        ),
        (
            '    /// <summary>Scout mode (move + camera, kh\u00f4ng combat)</summary>',
            '    /// <summary>Scout mode (move + camera, no combat).</summary>'
        ),
        (
            '    /// <summary>Ch\u1ec9 camera controls</summary>',
            '    /// <summary>Camera controls only.</summary>'
        ),
        (
            '    /// <summary>Dialogue \u2013 ch\u1ec9 UI</summary>',
            '    /// <summary>Dialogue \u2014 UI only.</summary>'
        ),
        (
            '    // \u2500\u2500 Legacy aliases (gi\u1eef l\u1ea1i \u0111\u1ec3 tr\u00e1nh break code c\u0169) \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n    // \u26a0\ufe0f PH\u1ea2I \u0111\u1eb7t CU\u1ed0I, SAU t\u1ea5t c\u1ea3 value th\u1ef1c, \u0111\u1ec3 tr\u00e1nh C# auto-increment collision',
            '    // \u2500\u2500 Legacy aliases (kept to avoid breaking existing code) \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\n    // \u26a0\ufe0f MUST be placed LAST, after all real values, to avoid C# auto-increment collisions.'
        ),
    ],
    r'Gameplay\Input\Core\InputConfig.cs': [
        (
            '        [Header("Action Map Names \u2013 ph\u1ea3i kh\u1edbp v\u1edbi t\u00ean trong InputSystem_Actions.inputactions")]',
            '        [Header("Action Map Names \u2013 must match names in InputSystem_Actions.inputactions")]'
        ),
    ],
}

total = 0
for rel_path, pairs in fixes.items():
    fp = os.path.join(root, rel_path)
    if not os.path.exists(fp):
        print(f'MISSING: {rel_path}')
        continue
    content = open(fp, encoding='utf-8', errors='replace').read()
    changed = 0
    for old, new in pairs:
        if old in content:
            content = content.replace(old, new)
            changed += 1
    if changed:
        open(fp, 'w', encoding='utf-8').write(content)
        print(f'{rel_path}: {changed} replacements')
        total += changed
    else:
        print(f'{rel_path}: 0 replacements')

print(f'Total: {total}')
