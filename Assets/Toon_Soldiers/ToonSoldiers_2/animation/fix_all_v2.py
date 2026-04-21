"""
fix_all_v2.py — Complete animator controller fix (regenerated file, 23616 lines)

Fixes ALL 5 issues:
  1. Base Layer: AnyState→Exit(WeaponType≠N) for all 6 sub-machines so weapon switching works
  2. UpperBody: Full AnyState transitions per weapon type (all actions, Stand/Crouch/Prone)
  3. UpperBody: AnyState→Exit(WeaponType≠N) for outer sub-machine switching
  4. Death: Fix trigger ConditionMode 9→1 for Die, Respawn, Roll
  5. Shooting per weapon: Infantry gets ShootBolt/Shotgun routing, Heavy/MG get ShootLoop,
     Knife gets Attack combos — each weapon plays ONLY its correct animations
  + Adds Unarmed_Base/UpperBody/Death for WeaponType=6 (holster/draw)
  + Adds Respawn parameter if missing
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()
print(f"Input: {len(lines)} lines")

# ─────────────────────────────────────────────────────────────────────────────
# HELPERS
# ─────────────────────────────────────────────────────────────────────────────

def find_block(fid):
    pat = re.compile(rf'^--- !u!\d+ &{fid}\b')
    for i, l in enumerate(lines):
        if pat.match(l):
            for j in range(i+1, len(lines)):
                if lines[j].startswith('--- !u!'):
                    return i, j
            return i, len(lines)
    return -1, -1

def get_anystate_fids(machine_fid):
    s, e = find_block(machine_fid)
    result = []
    for i in range(s, e):
        if '  m_AnyStateTransitions:' in lines[i]:
            j = i+1
            while j < e and '- {fileID:' in lines[j]:
                m = re.search(r'fileID: (-?\d+)', lines[j])
                if m: result.append(int(m.group(1)))
                j += 1
            break
    return result

def set_anystate_transitions(machine_fid, new_refs):
    s, e = find_block(machine_fid)
    for i in range(s, e):
        if '  m_AnyStateTransitions:' in lines[i]:
            j = i+1
            while j < len(lines) and j < e+50 and '- {fileID:' in lines[j]:
                lines.pop(j)
            if new_refs:
                lines[i] = '  m_AnyStateTransitions:\n'
                for k, ref in enumerate(new_refs):
                    lines.insert(i+1+k, f'  - {{fileID: {ref}}}\n')
            else:
                lines[i] = '  m_AnyStateTransitions: []\n'
            return True
    return False

def add_entry_transition(root_fid, trans_fid):
    s, e = find_block(root_fid)
    for i in range(s, e):
        if '  m_EntryTransitions:' in lines[i]:
            j = i+1
            while j < e and '- {fileID:' in lines[j]:
                j += 1
            lines.insert(j, f'  - {{fileID: {trans_fid}}}\n')
            return True
    return False

def add_child_statemachine(root_fid, child_fid, pos_x, pos_y):
    s, e = find_block(root_fid)
    for i in range(s, e):
        if '  m_ChildStateMachines:' in lines[i]:
            j = i+1
            while j < e and (lines[j].startswith('    ') or ('  - ' in lines[j] and j > i)):
                j += 1
            lines.insert(j,   '  - serializedVersion: 1\n')
            lines.insert(j+1, f'    m_StateMachine: {{fileID: {child_fid}}}\n')
            lines.insert(j+2, f'    m_Position: {{x: {pos_x}, y: {pos_y}, z: 0}}\n')
            return True
    return False

# ─────────────────────────────────────────────────────────────────────────────
# BLOCK BUILDERS
# ─────────────────────────────────────────────────────────────────────────────

def anystate_to_state(fid, dst_state, conditions):
    """AnyState→state. Minimal format (no serializedVersion)."""
    cond = ''
    for mode, event, thr in conditions:
        cond += f'  - m_ConditionMode: {mode}\n    m_ConditionEvent: {event}\n    m_EventTreshold: {thr}\n'
    return (
        f'--- !u!1101 &{fid}\nAnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: \n'
        f'  m_Conditions:\n{cond}'
        f'  m_DstStateMachine: {{fileID: 0}}\n  m_DstState: {{fileID: {dst_state}}}\n'
        f'  m_Solo: 0\n  m_Mute: 0\n  m_IsExit: 0\n'
    )

def anystate_exit(fid, wtype):
    """AnyState→Exit when WeaponType≠wtype."""
    return (
        f'--- !u!1101 &{fid}\nAnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: \n'
        f'  m_Conditions:\n'
        f'  - m_ConditionMode: 7\n    m_ConditionEvent: WeaponType\n    m_EventTreshold: {wtype}\n'
        f'  m_DstStateMachine: {{fileID: 0}}\n  m_DstState: {{fileID: 0}}\n'
        f'  m_Solo: 0\n  m_Mute: 0\n  m_IsExit: 1\n'
    )

def make_sm(fid, name, child_state_fids, anystate_fids, entry_fids, default_fid, positions):
    child_states = ''
    for sfid, (px, py) in zip(child_state_fids, positions):
        child_states += f'  - serializedVersion: 1\n    m_State: {{fileID: {sfid}}}\n    m_Position: {{x: {px}, y: {py}, z: 0}}\n'
    any_list = ''.join(f'  - {{fileID: {r}}}\n' for r in anystate_fids) if anystate_fids else ''
    any_line = f'  m_AnyStateTransitions:\n{any_list}' if anystate_fids else '  m_AnyStateTransitions: []\n'
    entry_list = ''.join(f'  - {{fileID: {r}}}\n' for r in entry_fids)
    return (
        f'--- !u!1107 &{fid}\nAnimatorStateMachine:\n  serializedVersion: 6\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: {name}\n'
        f'  m_ChildStates:\n{child_states}'
        f'  m_ChildStateMachines: []\n{any_line}'
        f'  m_EntryTransitions:\n{entry_list}'
        f'  m_StateMachineTransitions:\n  - first: {{fileID: 0}}\n    second: []\n'
        f'  m_StateMachineBehaviours: []\n'
        f'  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}\n  m_EntryPosition: {{x: 50, y: 120, z: 0}}\n'
        f'  m_ExitPosition: {{x: 800, y: 120, z: 0}}\n  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}\n'
        f'  m_DefaultState: {{fileID: {default_fid}}}\n'
    )

def make_empty_state(fid, name, px, py):
    return (
        f'--- !u!1102 &{fid}\nAnimatorState:\n  serializedVersion: 6\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: {name}\n'
        f'  m_Speed: 1\n  m_CycleOffset: 0\n  m_Transitions: []\n  m_StateMachineBehaviours: []\n'
        f'  m_Position: {{x: {px}, y: {py}, z: 0}}\n  m_IKOnFeet: 0\n  m_WriteDefaultValues: 1\n'
        f'  m_Mirror: 0\n  m_SpeedParameterActive: 0\n  m_MirrorParameterActive: 0\n'
        f'  m_CycleOffsetParameterActive: 0\n  m_TimeParameterActive: 0\n  m_Motion: {{fileID: 0}}\n'
        f'  m_Tag: \n  m_SpeedParameter: \n  m_MirrorParameter: \n  m_CycleOffsetParameter: \n  m_TimeParameter: \n'
    )

def make_motion_state(fid, name, guid, px, py):
    return (
        f'--- !u!1102 &{fid}\nAnimatorState:\n  serializedVersion: 6\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: {name}\n'
        f'  m_Speed: 1\n  m_CycleOffset: 0\n  m_Transitions: []\n  m_StateMachineBehaviours: []\n'
        f'  m_Position: {{x: {px}, y: {py}, z: 0}}\n  m_IKOnFeet: 0\n  m_WriteDefaultValues: 1\n'
        f'  m_Mirror: 0\n  m_SpeedParameterActive: 0\n  m_MirrorParameterActive: 0\n'
        f'  m_CycleOffsetParameterActive: 0\n  m_TimeParameterActive: 0\n'
        f'  m_Motion: {{fileID: 7400000, guid: {guid}, type: 3}}\n'
        f'  m_Tag: \n  m_SpeedParameter: \n  m_MirrorParameter: \n  m_CycleOffsetParameter: \n  m_TimeParameter: \n'
    )

def make_entry_trans(fid, dst_state=0, dst_machine=0, conditions=None):
    cond = ''
    if conditions:
        for mode, event, thr in conditions:
            cond += f'  - m_ConditionMode: {mode}\n    m_ConditionEvent: {event}\n    m_EventTreshold: {thr}\n'
        cond_block = f'  m_Conditions:\n{cond}'
    else:
        cond_block = '  m_Conditions: []\n'
    return (
        f'--- !u!1109 &{fid}\nAnimatorTransition:\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: \n'
        f'{cond_block}'
        f'  m_DstStateMachine: {{fileID: {dst_machine}}}\n  m_DstState: {{fileID: {dst_state}}}\n'
        f'  m_Solo: 0\n  m_Mute: 0\n  m_IsExit: 0\n  serializedVersion: 1\n'
    )

def make_death_anystate(fid, dst_fid, conditions):
    cond = ''
    for mode, event, thr in conditions:
        cond += f'  - m_ConditionMode: {mode}\n    m_ConditionEvent: {event}\n    m_EventTreshold: {thr}\n'
    return (
        f'--- !u!1101 &{fid}\nAnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_Name: \n'
        f'  m_Conditions:\n{cond}'
        f'  m_DstStateMachine: {{fileID: 0}}\n  m_DstState: {{fileID: {dst_fid}}}\n'
        f'  m_Solo: 0\n  m_Mute: 0\n  m_IsExit: 0\n'
        f'  serializedVersion: 3\n  m_TransitionDuration: 0.05\n  m_TransitionOffset: 0\n'
        f'  m_ExitTime: 0\n  m_HasExitTime: 0\n  m_HasFixedDuration: 0\n'
        f'  m_InterruptionSource: 0\n  m_OrderedInterruption: 1\n  m_CanTransitionToSelf: 0\n'
    )

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1: FIX TRIGGER CONDITION MODES (9 → 1 for triggers)
# ─────────────────────────────────────────────────────────────────────────────
# Find all parameters that are triggers (type=9) and collect their names
trigger_params = set()
in_params = False
i = 0
while i < len(lines):
    l = lines[i]
    if 'm_Parameters:' in l:
        in_params = True
    if in_params:
        if '  m_Name:' in l and i > 0:
            param_name = l.split(':',1)[1].strip()
        if '  m_Type: 9' in l and param_name:
            trigger_params.add(param_name)
            param_name = ''
        if l.startswith('  m_Controller:') or l.startswith('  m_AnimatorParameters:'):
            break
    i += 1

print(f"Trigger params found: {trigger_params}")

# Now fix ConditionMode: for trigger params, mode=9 (NotEqual) should be mode=1 (If)
fixes = 0
i = 0
while i < len(lines):
    if '    m_ConditionMode: 9' in lines[i]:
        # Check next line for the event name
        if i+1 < len(lines) and '    m_ConditionEvent:' in lines[i+1]:
            event = lines[i+1].split(':',1)[1].strip()
            if event in trigger_params:
                lines[i] = lines[i].replace('m_ConditionMode: 9', 'm_ConditionMode: 1')
                fixes += 1
    i += 1
print(f"Fixed {fixes} trigger ConditionMode 9→1")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2: ADD RESPAWN PARAMETER if missing
# ─────────────────────────────────────────────────────────────────────────────
if 'Respawn' not in trigger_params:
    # Find parameter list and add Respawn trigger after Roll
    for i, l in enumerate(lines):
        if '    m_Name: Roll' in l:
            # Find end of this param block
            j = i
            while j < len(lines) and not (lines[j].strip().startswith('- m_Name:') and j > i):
                j += 1
            # Insert Respawn after Roll block
            insert_at = j
            lines.insert(insert_at, '  - m_Name: Respawn\n')
            lines.insert(insert_at+1, '    m_Type: 9\n')
            lines.insert(insert_at+2, '    m_DefaultFloat: 0\n')
            lines.insert(insert_at+3, '    m_DefaultInt: 0\n')
            lines.insert(insert_at+4, '    m_DefaultBool: 0\n')
            lines.insert(insert_at+5, '    m_Controller: {fileID: 0}\n')
            print("Added Respawn trigger parameter")
            trigger_params.add('Respawn')
            break

# Now also fix Respawn in conditions (was mode=9, now should be 1)
fixes2 = 0
i = 0
while i < len(lines):
    if '    m_ConditionMode: 9' in lines[i]:
        if i+1 < len(lines) and 'm_ConditionEvent: Respawn' in lines[i+1]:
            lines[i] = lines[i].replace('m_ConditionMode: 9', 'm_ConditionMode: 1')
            fixes2 += 1
    i += 1
print(f"Fixed {fixes2} Respawn ConditionMode")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3: BUILD ALL NEW BLOCKS
# ─────────────────────────────────────────────────────────────────────────────
new_blocks = []
fid_counter = [9202000]

def next_fid():
    f = fid_counter[0]
    fid_counter[0] += 1
    return f

# ── Condition shorthands ──────────────────────────────────────────────────────
# Stance conditions
STAND  = [(2,'IsCrouching',0),(2,'IsProne',0)]  # !Crouch !Prone
CROUCH = [(1,'IsCrouching',0)]
PRONE  = [(1,'IsProne',0)]

# AnyState transition for each (conditions, dst_state)
def build_ub_anystate_for_machine(machine_fid, wtype, actions_data):
    """
    actions_data = list of (conditions, dst_state_fid) tuples.
    Last item should be the weapon exit.
    Returns list of new FIDs for m_AnyStateTransitions.
    """
    fids = []
    for conditions, dst in actions_data:
        if dst == 'EXIT':
            fid = next_fid()
            new_blocks.append(anystate_exit(fid, wtype))
        else:
            fid = next_fid()
            new_blocks.append(anystate_to_state(fid, dst, conditions))
        fids.append(fid)
    set_anystate_transitions(machine_fid, fids)
    return fids

# ════════════════════════════════════════════════════════════════════════════
# HANDGUN_UPPERBODY (&9200048) — WeaponType=0
# States: UB_Empty=9200049, Draw_Stand=9200050, Shoot_Stand=9200052,
#   Reload_Stand=9200054, Grenade_Stand=9200056, Interact_A=9200058,
#   Interact_B=9200060, Damage_Stand=9200062, ShootBurst_Stand=9200064,
#   Draw_Crouch=9200066, Shoot_Crouch=9200068, Reload_Crouch=9200070,
#   Grenade_Crouch=9200072, Damage_Crouch=9200074, ShootBurst_Crouch=9200076,
#   Draw_Prone=9200078, Shoot_Prone=9200080, Reload_Prone=9200082,
#   Grenade_Prone=9200084, Damage_Prone=9200086, ShootBurst_Prone=9200088
# ════════════════════════════════════════════════════════════════════════════
HG_ACTIONS = [
    # Stand variants (no crouch, no prone)
    ([(1,'Draw',0)]       + STAND,  9200050),  # Draw_Stand
    ([(1,'Shoot',0)]      + STAND,  9200052),  # Shoot_Stand
    ([(1,'ShootBurst',0)] + STAND,  9200064),  # ShootBurst_Stand
    ([(1,'Reload',0)]     + STAND,  9200054),  # Reload_Stand
    ([(1,'ThrowGrenade',0)] + STAND, 9200056), # Grenade_Stand
    ([(1,'TakeDamage',0)] + STAND,  9200062),  # Damage_Stand
    # Crouch variants
    ([(1,'Draw',0)]       + CROUCH, 9200066),  # Draw_Crouch
    ([(1,'Shoot',0)]      + CROUCH, 9200068),  # Shoot_Crouch
    ([(1,'ShootBurst',0)] + CROUCH, 9200076),  # ShootBurst_Crouch
    ([(1,'Reload',0)]     + CROUCH, 9200070),  # Reload_Crouch
    ([(1,'ThrowGrenade',0)] + CROUCH, 9200072),# Grenade_Crouch
    ([(1,'TakeDamage',0)] + CROUCH, 9200074),  # Damage_Crouch
    # Prone variants
    ([(1,'Draw',0)]       + PRONE,  9200078),  # Draw_Prone
    ([(1,'Shoot',0)]      + PRONE,  9200080),  # Shoot_Prone
    ([(1,'ShootBurst',0)] + PRONE,  9200088),  # ShootBurst_Prone
    ([(1,'Reload',0)]     + PRONE,  9200082),  # Reload_Prone
    ([(1,'ThrowGrenade',0)] + PRONE, 9200084), # Grenade_Prone
    ([(1,'TakeDamage',0)] + PRONE,  9200086),  # Damage_Prone
    # Generic (any stance)
    ([(1,'Interact',0),(6,'InteractIndex',0)], 9200058),  # Interact_A
    ([(1,'Interact',0),(6,'InteractIndex',1)], 9200060),  # Interact_B
    # Weapon exit
    ([], 'EXIT'),
]
build_ub_anystate_for_machine(9200048, 0, HG_ACTIONS)
print("Handgun_UpperBody: done")

# ════════════════════════════════════════════════════════════════════════════
# INFANTRY_UPPERBODY (&9200174) — WeaponType=1
# States: UB_Empty=9200175, Draw_Stand=9200176, Shoot_Stand=9200178,
#   Reload_Stand=9200180, Grenade_Stand=9200182, Interact_A=9200184,
#   Interact_B=9200186, Damage_Stand=9200188, ShootBurst_Stand=9200190,
#   ShootBolt_Stand=9200192, ShootShotgun_Stand=9200194,
#   Draw_Crouch=9200196, Shoot_Crouch=9200198, Reload_Crouch=9200200,
#   Grenade_Crouch=9200202, Damage_Crouch=9200204, ShootBurst_Crouch=9200206,
#   Draw_Prone=9200208, Shoot_Prone=9200210, Reload_Prone=9200212,
#   Grenade_Prone=9200214, Damage_Prone=9200216, ShootBurst_Prone=9200218
# ════════════════════════════════════════════════════════════════════════════
# For Infantry: ShootBolt and ShootShotgun are BOOLS. When ShootBolt=true and
# Shoot trigger fires → ShootBolt_Stand. When ShootShotgun=true → ShootShotgun_Stand.
# These checks must come BEFORE the generic Shoot check.
INF_ACTIONS = [
    # Stand — specific shoot modes first (take priority)
    ([(1,'Draw',0)]       + STAND,  9200176),  # Draw_Stand
    # ShootBolt: Shoot trigger + ShootBolt bool=true + !Crouch + !Prone
    ([(1,'Shoot',0),(1,'ShootBolt',0)] + STAND,     9200192),  # ShootBolt_Stand
    # ShootShotgun: Shoot trigger + ShootShotgun bool=true + !Crouch + !Prone
    ([(1,'Shoot',0),(1,'ShootShotgun',0)] + STAND,  9200194),  # ShootShotgun_Stand
    # Generic Shoot (neither bolt nor shotgun)
    ([(1,'Shoot',0),(2,'ShootBolt',0),(2,'ShootShotgun',0)] + STAND, 9200178),
    ([(1,'ShootBurst',0)] + STAND,  9200190),  # ShootBurst_Stand
    ([(1,'Reload',0)]     + STAND,  9200180),
    ([(1,'ThrowGrenade',0)] + STAND, 9200182),
    ([(1,'TakeDamage',0)] + STAND,  9200188),
    # Crouch
    ([(1,'Draw',0)]       + CROUCH, 9200196),
    ([(1,'Shoot',0)]      + CROUCH, 9200198),
    ([(1,'ShootBurst',0)] + CROUCH, 9200206),
    ([(1,'Reload',0)]     + CROUCH, 9200200),
    ([(1,'ThrowGrenade',0)] + CROUCH, 9200202),
    ([(1,'TakeDamage',0)] + CROUCH, 9200204),
    # Prone
    ([(1,'Draw',0)]       + PRONE,  9200208),
    ([(1,'Shoot',0)]      + PRONE,  9200210),
    ([(1,'ShootBurst',0)] + PRONE,  9200218),
    ([(1,'Reload',0)]     + PRONE,  9200212),
    ([(1,'ThrowGrenade',0)] + PRONE, 9200214),
    ([(1,'TakeDamage',0)] + PRONE,  9200216),
    # Generic
    ([(1,'Interact',0),(6,'InteractIndex',0)], 9200184),
    ([(1,'Interact',0),(6,'InteractIndex',1)], 9200186),
    ([], 'EXIT'),
]
build_ub_anystate_for_machine(9200174, 1, INF_ACTIONS)
print("Infantry_UpperBody: done")

# ════════════════════════════════════════════════════════════════════════════
# HEAVY_UPPERBODY (&9200306) — WeaponType=2
# States: UB_Empty=9200307, Draw_Stand=9200308, Shoot_Stand=9200310,
#   Reload_Stand=9200312, Grenade_Stand=9200314, Interact_A=9200316,
#   Interact_B=9200318, Damage_Stand=9200320, ShootBurst_Stand=9200322,
#   ShootLoop_Stand=9200324,
#   Draw_Crouch=9200326, Shoot_Crouch=9200328, Reload_Crouch=9200330,
#   Grenade_Crouch=9200332, Damage_Crouch=9200334, ShootBurst_Crouch=9200336,
#   ShootLoop_Crouch=9200338,
#   Draw_Prone=9200340, Shoot_Prone=9200342, Reload_Prone=9200344,
#   Grenade_Prone=9200346, Damage_Prone=9200348, ShootBurst_Prone=9200350,
#   ShootLoop_Prone=9200352
# ShootLoop is a BOOL (hold trigger). Check BEFORE generic Shoot.
# ════════════════════════════════════════════════════════════════════════════
HVY_ACTIONS = [
    # Stand
    ([(1,'Draw',0)]       + STAND,  9200308),
    ([(1,'ShootLoop',0)]  + STAND,  9200324),  # ShootLoop bool=true, Stand — first!
    ([(1,'Shoot',0),(2,'ShootLoop',0)] + STAND, 9200310),  # Single shot when not looping
    ([(1,'ShootBurst',0)] + STAND,  9200322),
    ([(1,'Reload',0)]     + STAND,  9200312),
    ([(1,'ThrowGrenade',0)] + STAND, 9200314),
    ([(1,'TakeDamage',0)] + STAND,  9200320),
    # Crouch
    ([(1,'Draw',0)]       + CROUCH, 9200326),
    ([(1,'ShootLoop',0)]  + CROUCH, 9200338),
    ([(1,'Shoot',0),(2,'ShootLoop',0)] + CROUCH, 9200328),
    ([(1,'ShootBurst',0)] + CROUCH, 9200336),
    ([(1,'Reload',0)]     + CROUCH, 9200330),
    ([(1,'ThrowGrenade',0)] + CROUCH, 9200332),
    ([(1,'TakeDamage',0)] + CROUCH, 9200334),
    # Prone
    ([(1,'Draw',0)]       + PRONE,  9200340),
    ([(1,'ShootLoop',0)]  + PRONE,  9200352),
    ([(1,'Shoot',0),(2,'ShootLoop',0)] + PRONE, 9200342),
    ([(1,'ShootBurst',0)] + PRONE,  9200350),
    ([(1,'Reload',0)]     + PRONE,  9200344),
    ([(1,'ThrowGrenade',0)] + PRONE, 9200346),
    ([(1,'TakeDamage',0)] + PRONE,  9200348),
    # Generic
    ([(1,'Interact',0),(6,'InteractIndex',0)], 9200316),
    ([(1,'Interact',0),(6,'InteractIndex',1)], 9200318),
    ([], 'EXIT'),
]
build_ub_anystate_for_machine(9200306, 2, HVY_ACTIONS)
print("Heavy_UpperBody: done")

# ════════════════════════════════════════════════════════════════════════════
# KNIFE_UPPERBODY (&9200441) — WeaponType=3
# States: UB_Empty=9200442, Draw_Stand=9200443, Shoot_Stand=9200445,
#   Reload_Stand=9200447, Grenade_Stand=9200449, Interact_A=9200451,
#   Interact_B=9200453, Damage_Stand=9200455, Attack_A_Stand=9200457,
#   Attack_B_Stand=9200459, Draw_Crouch=9200461, Shoot_Crouch=9200463,
#   Reload_Crouch=9200465, Grenade_Crouch=9200467, Damage_Crouch=9200469,
#   Attack_A_Crouch=9200471, Attack_B_Crouch=9200473,
#   Draw_Prone=9200475, Shoot_Prone=9200477, Reload_Prone=9200479,
#   Grenade_Prone=9200481, Damage_Prone=9200483, Attack_Prone=9200485
# Knife: Attack trigger + AttackIndex for combos; Shoot_Stand = quick slash
# ════════════════════════════════════════════════════════════════════════════
KNF_ACTIONS = [
    # Stand
    ([(1,'Draw',0)]         + STAND, 9200443),
    # Attack combo: Attack trigger + AttackIndex==0/1 + !Prone (for stand/crouch variants)
    ([(1,'Attack',0),(6,'AttackIndex',0)] + STAND,   9200457),  # Attack_A_Stand
    ([(1,'Attack',0),(6,'AttackIndex',1)] + STAND,   9200459),  # Attack_B_Stand
    ([(1,'Shoot',0)]        + STAND, 9200445),   # Quick slash stand
    ([(1,'Reload',0)]       + STAND, 9200447),
    ([(1,'ThrowGrenade',0)] + STAND, 9200449),
    ([(1,'TakeDamage',0)]   + STAND, 9200455),
    # Crouch
    ([(1,'Draw',0)]         + CROUCH, 9200461),
    ([(1,'Attack',0),(6,'AttackIndex',0)] + CROUCH,  9200471),  # Attack_A_Crouch
    ([(1,'Attack',0),(6,'AttackIndex',1)] + CROUCH,  9200473),  # Attack_B_Crouch
    ([(1,'Shoot',0)]        + CROUCH, 9200463),
    ([(1,'Reload',0)]       + CROUCH, 9200465),
    ([(1,'ThrowGrenade',0)] + CROUCH, 9200467),
    ([(1,'TakeDamage',0)]   + CROUCH, 9200469),
    # Prone
    ([(1,'Draw',0)]         + PRONE,  9200475),
    ([(1,'Attack',0)]       + PRONE,  9200485),  # Attack_Prone (generic, no index)
    ([(1,'Shoot',0)]        + PRONE,  9200477),
    ([(1,'Reload',0)]       + PRONE,  9200479),
    ([(1,'ThrowGrenade',0)] + PRONE,  9200481),
    ([(1,'TakeDamage',0)]   + PRONE,  9200483),
    # Generic
    ([(1,'Interact',0),(6,'InteractIndex',0)], 9200451),
    ([(1,'Interact',0),(6,'InteractIndex',1)], 9200453),
    ([], 'EXIT'),
]
build_ub_anystate_for_machine(9200441, 3, KNF_ACTIONS)
print("Knife_UpperBody: done")

# ════════════════════════════════════════════════════════════════════════════
# MACHINEGUN_UPPERBODY (&9200573) — WeaponType=4
# States: UB_Empty=9200574, Draw_Stand=9200575, Shoot_Stand=9200577,
#   Reload_Stand=9200579, Grenade_Stand=9200581, Interact_A=9200583,
#   Interact_B=9200585, Damage_Stand=9200587, ShootBurst_Stand=9200589,
#   ShootLoop_Stand=9200591,
#   Draw_Crouch=9200593, Shoot_Crouch=9200595, Reload_Crouch=9200597,
#   Grenade_Crouch=9200599, Damage_Crouch=9200601, ShootBurst_Crouch=9200603,
#   ShootLoop_Crouch=9200605,
#   Draw_Prone=9200607, Shoot_Prone=9200609, Reload_Prone=9200611,
#   Grenade_Prone=9200613, Damage_Prone=9200615, ShootBurst_Prone=9200617,
#   ShootLoop_Prone=9200619
# Same as Heavy but different weapon type
# ════════════════════════════════════════════════════════════════════════════
MG_ACTIONS = [
    # Stand
    ([(1,'Draw',0)]       + STAND,  9200575),
    ([(1,'ShootLoop',0)]  + STAND,  9200591),
    ([(1,'Shoot',0),(2,'ShootLoop',0)] + STAND, 9200577),
    ([(1,'ShootBurst',0)] + STAND,  9200589),
    ([(1,'Reload',0)]     + STAND,  9200579),
    ([(1,'ThrowGrenade',0)] + STAND, 9200581),
    ([(1,'TakeDamage',0)] + STAND,  9200587),
    # Crouch
    ([(1,'Draw',0)]       + CROUCH, 9200593),
    ([(1,'ShootLoop',0)]  + CROUCH, 9200605),
    ([(1,'Shoot',0),(2,'ShootLoop',0)] + CROUCH, 9200595),
    ([(1,'ShootBurst',0)] + CROUCH, 9200603),
    ([(1,'Reload',0)]     + CROUCH, 9200597),
    ([(1,'ThrowGrenade',0)] + CROUCH, 9200599),
    ([(1,'TakeDamage',0)] + CROUCH, 9200601),
    # Prone
    ([(1,'Draw',0)]       + PRONE,  9200607),
    ([(1,'ShootLoop',0)]  + PRONE,  9200619),
    ([(1,'Shoot',0),(2,'ShootLoop',0)] + PRONE, 9200609),
    ([(1,'ShootBurst',0)] + PRONE,  9200617),
    ([(1,'Reload',0)]     + PRONE,  9200611),
    ([(1,'ThrowGrenade',0)] + PRONE, 9200613),
    ([(1,'TakeDamage',0)] + PRONE,  9200615),
    # Generic
    ([(1,'Interact',0),(6,'InteractIndex',0)], 9200583),
    ([(1,'Interact',0),(6,'InteractIndex',1)], 9200585),
    ([], 'EXIT'),
]
build_ub_anystate_for_machine(9200573, 4, MG_ACTIONS)
print("Machinegun_UpperBody: done")

# ════════════════════════════════════════════════════════════════════════════
# ROCKETLAUNCHER_UPPERBODY (&9200708) — WeaponType=5
# States: UB_Empty=9200709, Draw_Stand=9200710, Shoot_Stand=9200712,
#   Reload_Stand=9200714, Grenade_Stand=9200716, Interact_A=9200718,
#   Interact_B=9200720, Damage_Stand=9200722,
#   Draw_Crouch=9200724, Shoot_Crouch=9200726, Reload_Crouch=9200728,
#   Grenade_Crouch=9200730, Damage_Crouch=9200732,
#   Draw_Prone=9200734, Shoot_Prone=9200736, Reload_Prone=9200738,
#   Grenade_Prone=9200740, Damage_Prone=9200742
# No ShootBurst, no ShootLoop — single shot only
# ════════════════════════════════════════════════════════════════════════════
RL_ACTIONS = [
    # Stand
    ([(1,'Draw',0)]       + STAND,  9200710),
    ([(1,'Shoot',0)]      + STAND,  9200712),
    ([(1,'Reload',0)]     + STAND,  9200714),
    ([(1,'ThrowGrenade',0)] + STAND, 9200716),
    ([(1,'TakeDamage',0)] + STAND,  9200722),
    # Crouch
    ([(1,'Draw',0)]       + CROUCH, 9200724),
    ([(1,'Shoot',0)]      + CROUCH, 9200726),
    ([(1,'Reload',0)]     + CROUCH, 9200728),
    ([(1,'ThrowGrenade',0)] + CROUCH, 9200730),
    ([(1,'TakeDamage',0)] + CROUCH, 9200732),
    # Prone
    ([(1,'Draw',0)]       + PRONE,  9200734),
    ([(1,'Shoot',0)]      + PRONE,  9200736),
    ([(1,'Reload',0)]     + PRONE,  9200738),
    ([(1,'ThrowGrenade',0)] + PRONE, 9200740),
    ([(1,'TakeDamage',0)] + PRONE,  9200742),
    # Generic
    ([(1,'Interact',0),(6,'InteractIndex',0)], 9200718),
    ([(1,'Interact',0),(6,'InteractIndex',1)], 9200720),
    ([], 'EXIT'),
]
build_ub_anystate_for_machine(9200708, 5, RL_ACTIONS)
print("RocketLauncher_UpperBody: done")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 4: BASE LAYER — AnyState→Exit(WeaponType≠N) for all 6 sub-machines
# ─────────────────────────────────────────────────────────────────────────────
BASE_MACHINES = [
    (9200001, 0),   # Handgun_Base
    (9200127, 1),   # Infantry_Base
    (9200259, 2),   # Heavy_Base
    (9200394, 3),   # Knife_Base
    (9200526, 4),   # Machinegun_Base
    (9200661, 5),   # RocketLauncher_Base
]
BASE_NAMES = ['Handgun','Infantry','Heavy','Knife','Machinegun','RocketLauncher']
for (mfid, wtype), name in zip(BASE_MACHINES, BASE_NAMES):
    fid = next_fid()
    new_blocks.append(anystate_exit(fid, wtype))
    set_anystate_transitions(mfid, [fid])
    print(f"  {name}_Base: AnyState→Exit(WT≠{wtype}) added FID &{fid}")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 5: UNARMED sub-machines (WeaponType=6)
# ─────────────────────────────────────────────────────────────────────────────
# FID allocations:
# Unarmed_Base:        machine=9202200, state=9202201, entry=9202202, exit=9202203, root_entry=9202204
# Unarmed_UpperBody:   machine=9202210, state=9202211, entry=9202212, exit=9202213, root_entry=9202214
# Unarmed_Death: machine=9202220, Death_Empty=9202221, Death_A=9202222...Prone=9202227
#   AnyState: die0..4=9202228-9202232, prone=9202233, respawn=9202234
#   entry=9202235, root_entry=9202236

# ── 5a: Unarmed_Base ──────────────────────────────────────────────────────
new_blocks.append(make_sm(
    fid=9202200, name='Unarmed_Base',
    child_state_fids=[9202201], anystate_fids=[9202203], entry_fids=[9202202],
    default_fid=9202201, positions=[(230,80)]
))
new_blocks.append(make_empty_state(9202201, 'Unarmed_Locomotion', 230, 80))
new_blocks.append(make_entry_trans(fid=9202202, dst_state=9202201))
new_blocks.append(anystate_exit(9202203, 6))
new_blocks.append(make_entry_trans(fid=9202204, dst_machine=9202200, conditions=[(6,'WeaponType',6)]))
add_child_statemachine(9200774, 9202200, 250, 580)
add_entry_transition(9200774, 9202204)
print("Unarmed_Base: added")

# ── 5b: Unarmed_UpperBody ─────────────────────────────────────────────────
new_blocks.append(make_sm(
    fid=9202210, name='Unarmed_UpperBody',
    child_state_fids=[9202211], anystate_fids=[9202213], entry_fids=[9202212],
    default_fid=9202211, positions=[(200,80)]
))
new_blocks.append(make_empty_state(9202211, 'UB_Empty', 200, 80))
new_blocks.append(make_entry_trans(fid=9202212, dst_state=9202211))
new_blocks.append(anystate_exit(9202213, 6))
new_blocks.append(make_entry_trans(fid=9202214, dst_machine=9202210, conditions=[(6,'WeaponType',6)]))
add_child_statemachine(9200812, 9202210, -30, 560)
add_entry_transition(9200812, 9202214)
print("Unarmed_UpperBody: added")

# ── 5c: Unarmed_Death ─────────────────────────────────────────────────────
DEATH_GUIDS = [
    '137155502bade354080c0c9ccdf60d9f',  # Death_A
    '56c88776f884dda4a948b1b65325eefd',  # Death_B
    'd7583335a6287e547afc1f3d264ce9cf',  # Death_C
    'd22c614baf27e7b43bf90f1abc111163',  # Death_D
    '4404c374a950ef546835eb97b753fc5f',  # Death_E
    '99e19cc10d490ce4fb872981f0d6174b',  # Death_Prone
]
DEATH_STATE_FIDS = list(range(9202221, 9202228))  # 7 states (Empty + A-E + Prone)
ANY_DEATH_FIDS   = list(range(9202228, 9202235))  # 7 transitions (Die×5 + Prone + Respawn)
DEATH_POSITIONS  = [(50,20),(200,100),(400,100),(600,100),(800,100),(1000,100),(1200,100)]

new_blocks.append(make_sm(
    fid=9202220, name='Unarmed_Death',
    child_state_fids=DEATH_STATE_FIDS, anystate_fids=ANY_DEATH_FIDS,
    entry_fids=[9202235], default_fid=9202221,
    positions=DEATH_POSITIONS
))
new_blocks.append(make_empty_state(9202221, 'Death_Empty', 50, 20))
DEATH_NAMES = ['Death_A','Death_B','Death_C','Death_D','Death_E','Death_Prone']
for i,name in enumerate(DEATH_NAMES):
    new_blocks.append(make_motion_state(9202222+i, name, DEATH_GUIDS[i], DEATH_POSITIONS[i+1][0], DEATH_POSITIONS[i+1][1]))

# AnyState: Die+Index0-4+!Prone → Death_A-E
for i in range(5):
    new_blocks.append(make_death_anystate(9202228+i, 9202222+i, [
        (1,'Die',0),(6,'DeathIndex',i),(2,'IsProne',0)
    ]))
# AnyState: Die+Prone → Death_Prone
new_blocks.append(make_death_anystate(9202233, 9202227, [(1,'Die',0),(1,'IsProne',0)]))
# AnyState: Respawn → Death_Empty
new_blocks.append(anystate_to_state(9202234, 9202221, [(1,'Respawn',0)]))
new_blocks.append(make_entry_trans(fid=9202235, dst_state=9202221))
new_blocks.append(make_entry_trans(fid=9202236, dst_machine=9202220, conditions=[(6,'WeaponType',6)]))
add_child_statemachine(9200850, 9202220, 250, 580)
add_entry_transition(9200850, 9202236)
print("Unarmed_Death: added")

# ─────────────────────────────────────────────────────────────────────────────
# WRITE OUTPUT
# ─────────────────────────────────────────────────────────────────────────────
for b in new_blocks:
    lines.append(b if b.endswith('\n') else b+'\n')

with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\n=== COMPLETE ===")
print(f"Output: {len(lines)} lines (+{len(lines)-23616} from original)")
print(f"New blocks appended: {len(new_blocks)}")
print(f"FID range used: 9202000 - {fid_counter[0]-1} + Unarmed blocks 9202200-9202236")
print(f"\nSummary of fixes:")
print(f"  1. Trigger ConditionMode fixed (mode=9→1)")
print(f"  2. Base Layer: 6 AnyState→Exit transitions added")
print(f"  3. UpperBody: AnyState transitions per weapon (HG/INF/HVY/KNF/MG/RL)")
print(f"     - Handgun/RL: Shoot+ShootBurst only")
print(f"     - Infantry: Shoot+ShootBurst + ShootBolt/Shotgun routing by bool")
print(f"     - Heavy/MG: Shoot+ShootBurst + ShootLoop routing by bool")
print(f"     - Knife: Shoot(slash)+Attack combos A/B by AttackIndex")
print(f"  4. UpperBody: 6 AnyState→Exit transitions added")
print(f"  5. Unarmed (WT=6): Base+UpperBody+Death sub-machines added")
