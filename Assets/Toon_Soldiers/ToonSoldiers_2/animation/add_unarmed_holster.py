"""
add_unarmed_holster.py
======================
Feature: Holster (cất súng) / Draw (cầm súng lại) — WeaponType = 6 = Unarmed

Adds:
  1. Unarmed_Base sub-machine    — locomotion without weapon (no motion clip)
  2. Unarmed_UpperBody sub-machine — UB_Empty only (no combat actions)
  3. Unarmed_Death sub-machine   — full death animation set
  4. Entry WeaponType==6 in all 3 root machines
  5. Fixes Base Layer sub-machines: add AnyState→Exit(WeaponType!=N) (was missing!)
  6. Cleans corrupt/duplicate AnyState refs from UpperBody root

FID ranges used:
  9202000-9202045  — new Unarmed blocks
  9202040-9202045  — Base Layer exit transitions
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'

with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Input lines: {len(lines)}")

# ─────────────────────────────────────────────────────────────────────────────
# HELPERS
# ─────────────────────────────────────────────────────────────────────────────

def find_block(lines, fid):
    """Return (start, end) line indices for YAML block with given fileID."""
    pat = re.compile(rf'^--- !u!\d+ &{fid}\b')
    for i, l in enumerate(lines):
        if pat.match(l):
            start = i
            for j in range(i + 1, len(lines)):
                if lines[j].startswith('--- !u!'):
                    return (start, j)
            return (start, len(lines))
    return (-1, -1)


def add_child_statemachine(lines, root_fid, child_fid, pos_x, pos_y):
    """Append a child state-machine ref to m_ChildStateMachines list."""
    start, end = find_block(lines, root_fid)
    for i in range(start, end):
        if '  m_ChildStateMachines:' in lines[i]:
            # find end of the list (items have >= 4-space indent or '  - ')
            j = i + 1
            while j < end and (lines[j].startswith('    ') or lines[j].startswith('  - ')):
                j += 1
            lines.insert(j,     '  - serializedVersion: 1\n')
            lines.insert(j + 1, f'    m_StateMachine: {{fileID: {child_fid}}}\n')
            lines.insert(j + 2, f'    m_Position: {{x: {pos_x}, y: {pos_y}, z: 0}}\n')
            return True
    print(f"ERROR: m_ChildStateMachines not found in &{root_fid}")
    return False


def add_entry_transition(lines, root_fid, transition_fid):
    """Append one ref to m_EntryTransitions list."""
    start, end = find_block(lines, root_fid)
    for i in range(start, end):
        if '  m_EntryTransitions:' in lines[i]:
            j = i + 1
            while j < end and lines[j].startswith('  - '):
                j += 1
            lines.insert(j, f'  - {{fileID: {transition_fid}}}\n')
            return True
    print(f"ERROR: m_EntryTransitions not found in &{root_fid}")
    return False


def set_anystate_transitions(lines, machine_fid, new_refs):
    """
    Replace m_AnyStateTransitions content with new_refs list.
    new_refs = list of int FIDs
    """
    start, end = find_block(lines, machine_fid)
    for i in range(start, end):
        if '  m_AnyStateTransitions:' in lines[i]:
            # Remove existing items
            j = i + 1
            while j < end and lines[j].strip().startswith('- {fileID:'):
                lines.pop(j)
                end -= 1
            if new_refs:
                lines[i] = '  m_AnyStateTransitions:\n'
                for k, ref in enumerate(new_refs):
                    lines.insert(i + 1 + k, f'  - {{fileID: {ref}}}\n')
            else:
                lines[i] = '  m_AnyStateTransitions: []\n'
            return True
    print(f"ERROR: m_AnyStateTransitions not found in &{machine_fid}")
    return False


# ─────────────────────────────────────────────────────────────────────────────
# YAML BLOCK TEMPLATES
# ─────────────────────────────────────────────────────────────────────────────

def make_state_machine(fid, name, child_state_fids, anystate_fids, entry_fids, default_fid, child_positions):
    child_states = ''
    for sfid, (px, py) in zip(child_state_fids, child_positions):
        child_states += f'  - serializedVersion: 1\n    m_State: {{fileID: {sfid}}}\n    m_Position: {{x: {px}, y: {py}, z: 0}}\n'
    anystate_list = ''.join(f'  - {{fileID: {r}}}\n' for r in anystate_fids) if anystate_fids else ''
    entry_list    = ''.join(f'  - {{fileID: {r}}}\n' for r in entry_fids)
    anystate_line = f'  m_AnyStateTransitions:\n{anystate_list}' if anystate_fids else '  m_AnyStateTransitions: []\n'
    return (
        f'--- !u!1107 &{fid}\n'
        f'AnimatorStateMachine:\n'
        f'  serializedVersion: 6\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: {name}\n'
        f'  m_ChildStates:\n'
        f'{child_states}'
        f'  m_ChildStateMachines: []\n'
        f'{anystate_line}'
        f'  m_EntryTransitions:\n'
        f'{entry_list}'
        f'  m_StateMachineTransitions:\n'
        f'  - first: {{fileID: 0}}\n'
        f'    second: []\n'
        f'  m_StateMachineBehaviours: []\n'
        f'  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}\n'
        f'  m_EntryPosition: {{x: 50, y: 120, z: 0}}\n'
        f'  m_ExitPosition: {{x: 800, y: 120, z: 0}}\n'
        f'  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}\n'
        f'  m_DefaultState: {{fileID: {default_fid}}}\n'
    )


def make_empty_state(fid, name, px, py):
    return (
        f'--- !u!1102 &{fid}\n'
        f'AnimatorState:\n'
        f'  serializedVersion: 6\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: {name}\n'
        f'  m_Speed: 1\n'
        f'  m_CycleOffset: 0\n'
        f'  m_Transitions: []\n'
        f'  m_StateMachineBehaviours: []\n'
        f'  m_Position: {{x: {px}, y: {py}, z: 0}}\n'
        f'  m_IKOnFeet: 0\n'
        f'  m_WriteDefaultValues: 1\n'
        f'  m_Mirror: 0\n'
        f'  m_SpeedParameterActive: 0\n'
        f'  m_MirrorParameterActive: 0\n'
        f'  m_CycleOffsetParameterActive: 0\n'
        f'  m_TimeParameterActive: 0\n'
        f'  m_Motion: {{fileID: 0}}\n'
        f'  m_Tag: \n'
        f'  m_SpeedParameter: \n'
        f'  m_MirrorParameter: \n'
        f'  m_CycleOffsetParameter: \n'
        f'  m_TimeParameter: \n'
    )


def make_motion_state(fid, name, guid, px, py):
    return (
        f'--- !u!1102 &{fid}\n'
        f'AnimatorState:\n'
        f'  serializedVersion: 6\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: {name}\n'
        f'  m_Speed: 1\n'
        f'  m_CycleOffset: 0\n'
        f'  m_Transitions: []\n'
        f'  m_StateMachineBehaviours: []\n'
        f'  m_Position: {{x: {px}, y: {py}, z: 0}}\n'
        f'  m_IKOnFeet: 0\n'
        f'  m_WriteDefaultValues: 1\n'
        f'  m_Mirror: 0\n'
        f'  m_SpeedParameterActive: 0\n'
        f'  m_MirrorParameterActive: 0\n'
        f'  m_CycleOffsetParameterActive: 0\n'
        f'  m_TimeParameterActive: 0\n'
        f'  m_Motion: {{fileID: 7400000, guid: {guid}, type: 3}}\n'
        f'  m_Tag: \n'
        f'  m_SpeedParameter: \n'
        f'  m_MirrorParameter: \n'
        f'  m_CycleOffsetParameter: \n'
        f'  m_TimeParameter: \n'
    )


def make_entry_transition(fid, dst_state=0, dst_machine=0, conditions=None):
    """AnimatorTransition (!u!1109) — Entry/sub-machine transition."""
    cond_text = ''
    if conditions:
        cond_text = '  m_Conditions:\n'
        for mode, event, thr in conditions:
            cond_text += (
                f'  - m_ConditionMode: {mode}\n'
                f'    m_ConditionEvent: {event}\n'
                f'    m_EventTreshold: {thr}\n'
            )
    else:
        cond_text = '  m_Conditions: []\n'
    return (
        f'--- !u!1109 &{fid}\n'
        f'AnimatorTransition:\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: \n'
        f'{cond_text}'
        f'  m_DstStateMachine: {{fileID: {dst_machine}}}\n'
        f'  m_DstState: {{fileID: {dst_state}}}\n'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: 0\n'
        f'  serializedVersion: 1\n'
    )


def make_anystate_exit(fid, weapon_type_value):
    """AnyState → Exit when WeaponType != weapon_type_value (mode=7 = NotEqual Int)."""
    return (
        f'--- !u!1101 &{fid}\n'
        f'AnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: \n'
        f'  m_Conditions:\n'
        f'  - m_ConditionMode: 7\n'
        f'    m_ConditionEvent: WeaponType\n'
        f'    m_EventTreshold: {weapon_type_value}\n'
        f'  m_DstStateMachine: {{fileID: 0}}\n'
        f'  m_DstState: {{fileID: 0}}\n'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: 1\n'
    )


def make_death_anystate(fid, dst_fid, conditions):
    """AnyState → death state (full transition format with settings)."""
    cond_text = ''
    for mode, event, thr in conditions:
        cond_text += (
            f'  - m_ConditionMode: {mode}\n'
            f'    m_ConditionEvent: {event}\n'
            f'    m_EventTreshold: {thr}\n'
        )
    return (
        f'--- !u!1101 &{fid}\n'
        f'AnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: \n'
        f'  m_Conditions:\n'
        f'{cond_text}'
        f'  m_DstStateMachine: {{fileID: 0}}\n'
        f'  m_DstState: {{fileID: {dst_fid}}}\n'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: 0\n'
        f'  serializedVersion: 3\n'
        f'  m_TransitionDuration: 0.05\n'
        f'  m_TransitionOffset: 0\n'
        f'  m_ExitTime: 0\n'
        f'  m_HasExitTime: 0\n'
        f'  m_HasFixedDuration: 0\n'
        f'  m_InterruptionSource: 0\n'
        f'  m_OrderedInterruption: 1\n'
        f'  m_CanTransitionToSelf: 0\n'
    )


# ─────────────────────────────────────────────────────────────────────────────
# BUILD NEW BLOCKS
# ─────────────────────────────────────────────────────────────────────────────

new_blocks = []

# ══════════════════════════════════════════════════════════════════════════════
# A) UNARMED_BASE  (WeaponType = 6)
# ══════════════════════════════════════════════════════════════════════════════
#   FIDs: 9202000=machine, 9202001=Unarmed_Locomotion state,
#         9202002=Entry→state, 9202003=AnyState→Exit(WT!=6)
#         9202004=Base-root Entry WeaponType==6→machine

new_blocks.append(make_state_machine(
    fid=9202000, name='Unarmed_Base',
    child_state_fids=[9202001],
    anystate_fids=[9202003],
    entry_fids=[9202002],
    default_fid=9202001,
    child_positions=[(230, 80)]
))

new_blocks.append(make_empty_state(9202001, 'Unarmed_Locomotion', 230, 80))

# Entry → Unarmed_Locomotion (no conditions)
new_blocks.append(make_entry_transition(fid=9202002, dst_state=9202001))

# AnyState → Exit when WeaponType != 6
new_blocks.append(make_anystate_exit(fid=9202003, weapon_type_value=6))

# Base-root Entry: WeaponType==6 → Unarmed_Base machine
new_blocks.append(make_entry_transition(
    fid=9202004, dst_machine=9202000,
    conditions=[(6, 'WeaponType', 6)]
))

# ══════════════════════════════════════════════════════════════════════════════
# B) UNARMED_UPPERBODY
# ══════════════════════════════════════════════════════════════════════════════
#   FIDs: 9202010=machine, 9202011=UB_Empty state,
#         9202012=Entry→state, 9202013=AnyState→Exit(WT!=6)
#         9202014=UB-root Entry WeaponType==6→machine

new_blocks.append(make_state_machine(
    fid=9202010, name='Unarmed_UpperBody',
    child_state_fids=[9202011],
    anystate_fids=[9202013],
    entry_fids=[9202012],
    default_fid=9202011,
    child_positions=[(200, 80)]
))

new_blocks.append(make_empty_state(9202011, 'UB_Empty', 200, 80))

new_blocks.append(make_entry_transition(fid=9202012, dst_state=9202011))

new_blocks.append(make_anystate_exit(fid=9202013, weapon_type_value=6))

new_blocks.append(make_entry_transition(
    fid=9202014, dst_machine=9202010,
    conditions=[(6, 'WeaponType', 6)]
))

# ══════════════════════════════════════════════════════════════════════════════
# C) UNARMED_DEATH
# ══════════════════════════════════════════════════════════════════════════════
#   Death states: FID 9202021=Death_Empty, 9202022-9202027=Death_A-E+Prone
#   AnyState transitions: 9202028-9202032=Die+Index0-4, 9202033=Die+Prone, 9202034=Respawn
#   Entry: 9202035
#   Death-root Entry: 9202036

DEATH_GUIDS = [
    '137155502bade354080c0c9ccdf60d9f',  # Death_A
    '56c88776f884dda4a948b1b65325eefd',  # Death_B
    'd7583335a6287e547afc1f3d264ce9cf',  # Death_C
    'd22c614baf27e7b43bf90f1abc111163',  # Death_D
    '4404c374a950ef546835eb97b753fc5f',  # Death_E
    '99e19cc10d490ce4fb872981f0d6174b',  # Death_Prone
]

death_state_fids  = list(range(9202021, 9202028))  # 9202021..9202027 (7 states: Empty + A-E + Prone)
death_child_fids  = death_state_fids                # all 7 in ChildStates
anystate_trans    = list(range(9202028, 9202035))   # 9202028-9202034

death_positions = [(50,20),(200,100),(400,100),(600,100),(800,100),(1000,100),(1200,100)]

new_blocks.append(make_state_machine(
    fid=9202020, name='Unarmed_Death',
    child_state_fids=death_child_fids,
    anystate_fids=anystate_trans,
    entry_fids=[9202035],
    default_fid=9202021,
    child_positions=death_positions
))

# Death_Empty
new_blocks.append(make_empty_state(9202021, 'Death_Empty', 50, 20))

# Death_A through Death_E  (FIDs 9202022-9202026, guids [0-4])
DEATH_NAMES = ['Death_A','Death_B','Death_C','Death_D','Death_E','Death_Prone']
for i in range(6):
    fid = 9202022 + i
    new_blocks.append(make_motion_state(fid, DEATH_NAMES[i], DEATH_GUIDS[i], death_positions[i+1][0], death_positions[i+1][1]))

# AnyState: Die + DeathIndex==0..4 + !IsProne → Death_A..E
for i in range(5):
    fid = 9202028 + i
    dst = 9202022 + i
    conds = [
        (1, 'Die', 0),
        (6, 'DeathIndex', i),
        (2, 'IsProne', 0),
    ]
    new_blocks.append(make_death_anystate(fid, dst, conds))

# AnyState: Die + IsProne → Death_Prone (9202027)
new_blocks.append(make_death_anystate(
    fid=9202033, dst_fid=9202027,
    conditions=[(1,'Die',0),(1,'IsProne',0)]
))

# AnyState: Respawn → Death_Empty (simple format, same as existing Respawn transitions)
new_blocks.append(
    f'--- !u!1101 &9202034\n'
    f'AnimatorStateTransition:\n'
    f'  m_ObjectHideFlags: 1\n'
    f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
    f'  m_PrefabInstance: {{fileID: 0}}\n'
    f'  m_PrefabAsset: {{fileID: 0}}\n'
    f'  m_Name: \n'
    f'  m_Conditions:\n'
    f'  - m_ConditionMode: 1\n'
    f'    m_ConditionEvent: Respawn\n'
    f'    m_EventTreshold: 0\n'
    f'  m_DstStateMachine: {{fileID: 0}}\n'
    f'  m_DstState: {{fileID: 9202021}}\n'
    f'  m_Solo: 0\n'
    f'  m_Mute: 0\n'
    f'  m_IsExit: 0\n'
)

# Entry → Death_Empty
new_blocks.append(make_entry_transition(fid=9202035, dst_state=9202021))

# Death-root Entry: WeaponType==6 → Unarmed_Death
new_blocks.append(make_entry_transition(
    fid=9202036, dst_machine=9202020,
    conditions=[(6, 'WeaponType', 6)]
))

# ══════════════════════════════════════════════════════════════════════════════
# D) BASE LAYER sub-machine AnyState → Exit(WeaponType != N)
# ══════════════════════════════════════════════════════════════════════════════
# These are MISSING — the fix_weapon_switch_respawn.py did not add them to Base.
BASE_EXIT_DATA = [
    (9202040, 0),  # Handgun_Base      WeaponType != 0
    (9202041, 1),  # Infantry_Base     WeaponType != 1
    (9202042, 2),  # Heavy_Base        WeaponType != 2
    (9202043, 3),  # Knife_Base        WeaponType != 3
    (9202044, 4),  # Machinegun_Base   WeaponType != 4
    (9202045, 5),  # RocketLauncher    WeaponType != 5
]
for fid, wtype in BASE_EXIT_DATA:
    new_blocks.append(make_anystate_exit(fid, wtype))

# ─────────────────────────────────────────────────────────────────────────────
# APPEND ALL NEW BLOCKS TO FILE
# ─────────────────────────────────────────────────────────────────────────────
for b in new_blocks:
    if not b.endswith('\n'):
        b += '\n'
    lines.append(b)

print(f"Appended {len(new_blocks)} new blocks")

# ─────────────────────────────────────────────────────────────────────────────
# MODIFY EXISTING MACHINE BLOCKS
# ─────────────────────────────────────────────────────────────────────────────

# ── BASE LAYER ROOT (&9200768) ──
add_child_statemachine(lines, 9200768, 9202000, 250, 580)
add_entry_transition(lines, 9200768, 9202004)
print("Base root: added Unarmed_Base entry")

# ── UPPERBODY ROOT (&9200776) ──
add_child_statemachine(lines, 9200776, 9202010, -30, 560)

# Clear corrupt AnyState refs in UpperBody root
set_anystate_transitions(lines, 9200776, [])
print("UpperBody root: cleared corrupt AnyState refs, added Unarmed_UpperBody")

add_entry_transition(lines, 9200776, 9202014)

# ── DEATH ROOT (&9200784) ──
add_child_statemachine(lines, 9200784, 9202020, 250, 580)
add_entry_transition(lines, 9200784, 9202036)
print("Death root: added Unarmed_Death entry")

# ── FIX BASE LAYER SUB-MACHINES: AnyState → Exit ──
BASE_SUB_MACHINES = [
    (9200001, 9202040),   # Handgun_Base
    (9200126, 9202041),   # Infantry_Base
    (9200257, 9202042),   # Heavy_Base
    (9200391, 9202043),   # Knife_Base
    (9200522, 9202044),   # Machinegun_Base
    (9200656, 9202045),   # RocketLauncher_Base
]
NAMES = ['Handgun','Infantry','Heavy','Knife','Machinegun','RocketLauncher']
for (machine_fid, trans_fid), name in zip(BASE_SUB_MACHINES, NAMES):
    ok = set_anystate_transitions(lines, machine_fid, [trans_fid])
    if ok:
        print(f"  {name}_Base: AnyState→Exit added ✓")

# ─────────────────────────────────────────────────────────────────────────────
# WRITE OUTPUT
# ─────────────────────────────────────────────────────────────────────────────
with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\nDone! Output lines: {len(lines)}")
print(f"Summary:")
print(f"  Unarmed_Base     machine + 1 state + 3 transitions")
print(f"  Unarmed_UpperBody machine + 1 state + 3 transitions")
print(f"  Unarmed_Death    machine + 7 states + 9 transitions")
print(f"  Base Layer exits  6 new AnyState transitions")
print(f"  Entry transitions 3 new (one per root)")
print(f"  Total new blocks: {len(new_blocks)}")
print(f"\nWeaponType=6 = Unarmed/Holster (press 7 key in tester)")
