import os

root = r'w:\Unity\Shotter\NightHuntClient\Assets\_Night_Hunt\Scripts'

fixes = {
    r'Gameplay\GameplaySystems\World\Core\WorldSpawnConfig.cs': [
        ('/// ScriptableObject c\u1ea5u h\u00ecnh cho m\u1ed9t WorldItemSpawnPoint.', '/// ScriptableObject that configures a single WorldItemSpawnPoint.'),
        ('/// X\u00e1c \u0111\u1ecbnh:', '/// Defines:'),
        ('///   - Lo\u1ea1i object s\u1ebd spawn (Item / Container / Chest)', '///   - The type of object to spawn (Item / Container / Chest)'),
        ('///   - SpawnTable (t\u1ef7 l\u1ec7 item s\u1ebd roll)', '///   - SpawnTable (item roll weights)'),
        ('///   - Th\u1eddi gian respawn after b\u1ecb loot', '///   - Respawn delay after being looted'),
        ('///   - S\u1ed1 l\u01b0\u1ee3ng active t\u1ed1i \u0111a', '///   - Maximum number of active spawns'),
        ('///        v\u00e0 g\u00e1n v\u00e0o WorldItemSpawnPoint tr\u00ean scene.', '///        and assign it to a WorldItemSpawnPoint in the scene.'),
    ],
    r'Gameplay\GameplaySystems\World\Core\WorldSpawnType.cs': [
        ('/// Lo\u1ea1i object s\u1ebd spawn t\u1ea1i WorldItemSpawnPoint.', '/// The type of object spawned at a WorldItemSpawnPoint.'),
        ('///   Item      \u2192 WorldItem (item r\u01a1i \u0111\u1ea5t, scatter t\u1eeb SpawnTable)', '///   Item      \u2192 WorldItem (ground drop, scattered from SpawnTable)'),
        ('///   Container \u2192 WorldContainer (th\u00f9ng / crate / r\u01b0\u01a1ng / chest)', '///   Container \u2192 WorldContainer (crate / chest / loot box)'),
        ('/// <summary>Item dropped on the ground (WorldItem) \u2014 scatter t\u1eeb SpawnTable</summary>', '/// <summary>Item dropped on the ground (WorldItem) \u2014 scattered from SpawnTable.</summary>'),
        ('/// <summary>Th\u00f9ng ch\u1ee9a / Crate / R\u01b0\u01a1ng / Chest (WorldContainer)</summary>', '/// <summary>Loot container / Crate / Chest (WorldContainer).</summary>'),
    ],
    r'Gameplay\GameplaySystems\World\Interactable\WorldDoor.cs': [
        ('/// Static world door \u2014 \u0111\u1eb7t s\u1eb5n tr\u00ean Scene, kh\u00f4ng spawn dynamic.', '/// Static world door placed in the scene \u2014 not spawned dynamically.'),
        ('///   - Implements IHoldInteractable: HoldDuration > 0 n\u1ebfu config = Hold, else 0 (Instant).', '///   - Implements IHoldInteractable: HoldDuration > 0 when config = Hold, 0 for Instant.'),
        ('///   - PlayerInteractionSystem handle c\u1ea3 hai mode t\u1ef1 \u0111\u1ed9ng qua interface.', '///   - PlayerInteractionSystem handles both modes automatically via the interface.'),
        ('///   - State (IsOpen) sync qua SyncVar cho t\u1ea5t c\u1ea3 client.', '///   - State (IsOpen) is synced via SyncVar to all clients.'),
        ('///   1. Add WorldDoor component v\u00e0o door GameObject tr\u00ean Scene.', '///   1. Add the WorldDoor component to the door GameObject in the scene.'),
        ('///   2. G\u00e1n InteractableConfig asset (Create \u2192 GameplaySystems \u2192 Config \u2192 Interactable Config).', '///   2. Assign an InteractableConfig asset (Create \u2192 GameplaySystems \u2192 Config \u2192 Interactable Config).'),
        ('/// 0 = Instant (InteractionMode != Hold ho\u1eb7c not available config).', '/// 0 = Instant (InteractionMode != Hold or no config).'),
        ('/// > 0 = gi\u00e2y player ph\u1ea3i gi\u1eef n\u00fat before Interact() fire.', '/// > 0 = seconds the player must hold the button before Interact() fires.'),
        ('// Auto-close: n\u1ebfu v\u1eeba m\u1edf v\u00e0 config c\u00f3 AutoReset', '// Auto-close: if just opened and config has AutoReset enabled.'),
        ('/// <summary>C\u1eeda t\u1ef1 \u0111\u00f3ng l\u1ea1i sau AutoResetDelay gi\u00e2y.</summary>', '/// <summary>Automatically closes the door after AutoResetDelay seconds.</summary>'),
        ('if (syncIsOpen.Value) // v\u1eabn \u0111ang m\u1edf th\u00ec m\u1edbi \u0111\u00f3ng', 'if (syncIsOpen.Value) // only close if still open'),
    ],
    r'Gameplay\GameplaySystems\World\Interactable\WorldSwitch.cs': [
        ('/// Static world switch / button \u2014 \u0111\u1eb7t s\u1eb5n tr\u00ean Scene, kh\u00f4ng spawn dynamic.', '/// Static world switch / button placed in the scene \u2014 not spawned dynamically.'),
        ('///   - Toggle switch: m\u1ed7i l\u1ea7n Interact() \u0111\u1ed5i state On\u2194Off \u2192 trigger OnActivated / OnDeactivated.', '///   - Toggle switch: each Interact() flips state On\u2194Off \u2192 fires OnActivated / OnDeactivated.'),
        ('///   - Button (OneTimeUse): ch\u1ec9 trigger 1 l\u1ea7n \u2192 OnActivated, sau \u0111\u00f3 CanInteract = false.', '///   - Button (OneTimeUse): triggers once \u2192 OnActivated; CanInteract becomes false afterwards.'),
        ('///   - Uses UnityEvent \u0111\u1ec3 connect v\u1edbi b\u1ea5t k\u1ef3 logic n\u00e0o m\u00e0 kh\u00f4ng c\u1ea7n code th\u00eam.', '///   - Uses UnityEvent so any logic can be connected without additional code.'),
        ('///   1. Add WorldSwitch v\u00e0o GameObject tr\u00ean Scene.', '///   1. Add WorldSwitch to a GameObject in the scene.'),
        ('///   2. G\u00e1n InteractableConfig (Type = Switch ho\u1eb7c Button).', '///   2. Assign an InteractableConfig (Type = Switch or Button).'),
    ],
    r'Gameplay\GameplaySystems\World\SpawnPoint\WorldItemSpawnPoint.cs': [
        ('/// Scene component \u0111\u00e1nh d\u1ea5u m\u1ed9t v\u1ecb tr\u00ed spawn World object (item/container/chest).', '/// Scene component marking a world-object spawn location (item / container / chest).'),
        ('///   - Ch\u1ec9 ch\u1ee9a data v\u1ecb tr\u00ed + WorldSpawnConfig.', '///   - Stores only position data and a WorldSpawnConfig reference.'),
        ('///   - WorldSpawnManager \u0111\u1ecdc t\u1ea5t c\u1ea3 WorldItemSpawnPoint trong scene khi server start', '///   - WorldSpawnManager reads all WorldItemSpawnPoint instances in the scene on server start'),
        ('///     v\u00e0 manage to\u00e0n b\u1ed9 lifecycle (spawn \u2192 loot \u2192 respawn).', '///     and manages the full lifecycle (spawn \u2192 loot \u2192 respawn).'),
        ('///   - Kh\u00f4ng ch\u1ee9a logic spawn \u2014 \u0111\u00fang SRP.', '///   - Contains no spawn logic \u2014 correct SRP.'),
        ('/// <summary>Config g\u1eafn v\u00e0o \u0111i\u1ec3m spawn n\u00e0y.</summary>', '/// <summary>SpawnConfig assigned to this spawn point.</summary>'),
        ('/// Lo\u1ea1i object s\u1ebd spawn t\u1ea1i \u0111\u00e2y.', '/// The type of object to spawn here.'),
        ('/// Shortcut \u0111\u1ec3 WorldSpawnManager kh\u00f4ng c\u1ea7n null-check SpawnConfig m\u1ed7i l\u1ea7n.', '/// Shortcut so WorldSpawnManager avoids null-checking SpawnConfig every frame.'),
        ('private int _spawnCount; // t\u1ed5ng s\u1ed1 l\u1ea7n \u0111\u00e3 spawn', 'private int _spawnCount; // total spawn count over the lifetime of this point'),
        ('/// <summary>S\u1ed1 object hi\u1ec7n \u0111ang active t\u1eeb \u0111i\u1ec3m spawn n\u00e0y.</summary>', '/// <summary>Number of currently active objects spawned from this point.</summary>'),
        ('/// <summary>T\u1ed5ng s\u1ed1 l\u1ea7n \u0111\u00e3 spawn (c\u1ea3 cu\u1ed9c \u0111\u1eddi c\u1ee7a spawn-point).</summary>', '/// <summary>Total number of spawns over the lifetime of this spawn point.</summary>'),
        ('/// <summary>True n\u1ebfu \u0111\u00e3 \u0111\u1ea1t MaxRespawnCount v\u00e0 kh\u00f4ng spawn n\u1eefa.</summary>', '/// <summary>True when MaxRespawnCount has been reached and no further spawns will occur.</summary>'),
        ('if (!spawnConfig.CanRespawn && _spawnCount > 0) return true; // one-shot: \u0111\u00e3 spawn r\u1ed3i', 'if (!spawnConfig.CanRespawn && _spawnCount > 0) return true; // one-shot: already spawned'),
        ('/// <summary>True n\u1ebfu \u0111\u00e3 \u0111\u1ea1t MaxActive v\u00e0 cannot spawn th\u00eam.</summary>', '/// <summary>True when MaxActive has been reached and no further spawns can occur.</summary>'),
        ('/// Tr\u1ea3 v\u1ec1 v\u1ecb tr\u00ed spawn.', '/// Returns the spawn position.'),
        ('/// V\u1edbi Item mode: random trong ScatterRadius.', '/// In Item mode: random point within ScatterRadius.'),
        ('/// V\u1edbi Container / Chest: ch\u00ednh x\u00e1c v\u1ecb tr\u00ed c\u1ee7a transform.', '/// In Container / Chest mode: exact transform position.'),
        ('// Scatter radius (ch\u1ec9 cho Item mode)', '// Scatter radius (Item mode only).'),
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
        print(f'{rel_path}: 0 replacements (may already be fixed or encoding mismatch)')

print(f'Total: {total}')
