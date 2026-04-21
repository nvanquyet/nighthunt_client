"""
Add transitions from UB_Empty -> action states in all 6 weapon UpperBody sub-machines.
This is the correct architectural approach: UB_Empty is idle, triggers fire actions,
actions exit back to parent -> Entry re-routes to weapon sub-machine -> UB_Empty.
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Lines: {len(lines)}")

# Find max fid
max_fid = 0
for l in lines:
    m = re.match(r'^--- !u!\d+ &(\d+)', l)
    if m:
        max_fid = max(max_fid, int(m.group(1)))
print(f"Max fid: {max_fid}")
next_fid = [max_fid + 1]  # Use list for mutability in nested function

# Weapon sub-machine fids
MACHINE_FIDS = ['9200048', '9200173', '9200304', '9200438', '9200569', '9200703']

# Cache state names
_name_cache = {}
def get_name(fid):
    if fid in _name_cache:
        return _name_cache[fid]
    for i, l in enumerate(lines):
        if re.match(r'^--- !u!1102 &' + fid + r'\b', l):
            for j in range(i+1, min(i+15, len(lines))):
                if re.match(r'^--- !u!', lines[j]): break
                if re.match(r'\s+m_Name:', lines[j]):
                    name = lines[j].split('m_Name:', 1)[1].strip()
                    _name_cache[fid] = name
                    return name
    return ''

def get_machine_info(machine_fid):
    """Returns (default_state_fid, [child_state_fids])"""
    for i, l in enumerate(lines):
        if re.match(r'^--- !u!1107 &' + machine_fid + r'\b', l):
            child_states = []
            default_state = None
            in_child = False
            j = i + 1
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                if 'm_ChildStates:' in lines[j]:
                    in_child = True
                elif in_child and 'm_ChildStateMachines:' in lines[j]:
                    in_child = False
                if in_child and 'm_State:' in lines[j]:
                    m = re.search(r'fileID: (\d+)', lines[j])
                    if m: child_states.append(m.group(1))
                if 'm_DefaultState:' in lines[j]:
                    m = re.search(r'fileID: (\d+)', lines[j])
                    if m: default_state = m.group(1)
                j += 1
            return default_state, child_states
    return None, []

def get_conditions(state_name):
    """
    Returns list of (conditionMode, conditionEvent, threshold) based on state name.
    conditionMode: 1=If/True, 2=IfNot/False, 6=Equals(int)
    Returns None if state should be skipped.
    """
    n = state_name.lower()
    conds = []

    # Detect action trigger from name
    if 'shootburst' in n:
        conds.append((1, 'ShootBurst', 0))
    elif 'shootloop' in n:
        conds.append((1, 'ShootLoop', 0))   # bool
    elif 'shootbolt' in n:
        conds.append((1, 'ShootBolt', 0))   # bool
    elif 'shootshotgun' in n:
        conds.append((1, 'ShootShotgun', 0)) # bool
    elif 'shoot' in n:
        conds.append((1, 'Shoot', 0))
    elif 'reload' in n:
        conds.append((1, 'Reload', 0))
    elif 'draw' in n:
        conds.append((1, 'Draw', 0))
    elif 'grenade' in n:
        conds.append((1, 'ThrowGrenade', 0))
    elif re.search(r'interact.*_a\b|interact_a', n):
        conds.append((1, 'Interact', 0))
        conds.append((6, 'InteractIndex', 0))   # InteractIndex == 0
    elif re.search(r'interact.*_b\b|interact_b', n):
        conds.append((1, 'Interact', 0))
        conds.append((6, 'InteractIndex', 1))   # InteractIndex == 1
    elif 'interact' in n:
        conds.append((1, 'Interact', 0))
    elif 'damage' in n or 'takedamage' in n:
        conds.append((1, 'TakeDamage', 0))
    elif re.search(r'attack.*_a\b|attack_a', n):
        conds.append((1, 'Attack', 0))
        conds.append((6, 'AttackIndex', 0))
    elif re.search(r'attack.*_b\b|attack_b', n):
        conds.append((1, 'Attack', 0))
        conds.append((6, 'AttackIndex', 1))
    elif 'attack' in n:
        conds.append((1, 'Attack', 0))
    else:
        return None  # Unknown state — skip

    # Stance modifier
    if n.endswith('_crouch'):
        conds.append((1, 'IsCrouching', 0))
    elif n.endswith('_prone'):
        conds.append((1, 'IsProne', 0))
    # Stand variants: no extra stance condition (rely on transition priority ordering)

    return conds

def make_block(fid, dst_fid, conds):
    cond_yaml = ''
    for mode, event, thresh in conds:
        cond_yaml += f'  - m_ConditionMode: {mode}\n'
        cond_yaml += f'    m_ConditionEvent: {event}\n'
        cond_yaml += f'    m_EventTreshold: {thresh}\n'
    return (
        f'--- !u!1101 &{fid}\n'
        f'AnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name:\n'
        f'  m_Conditions:\n'
        f'{cond_yaml}'
        f'  m_DstStateMachine: {{fileID: 0}}\n'
        f'  m_DstState: {{fileID: {dst_fid}}}\n'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: 0\n'
        f'  serializedVersion: 3\n'
        f'  m_TransitionDuration: 0.05\n'
        f'  m_TransitionOffset: 0\n'
        f'  m_ExitTime: 0.75\n'
        f'  m_HasExitTime: 0\n'
        f'  m_HasFixedDuration: 1\n'
        f'  m_InterruptionSource: 0\n'
        f'  m_OrderedInterruption: 1\n'
        f'  m_CanTransitionToSelf: 0\n'
    )

# Collect all new blocks and UB_Empty transition updates
new_blocks = []
# {ub_empty_fid: [new_trans_fids...]}
ub_updates = {}

for machine_fid in MACHINE_FIDS:
    default_fid, child_fids = get_machine_info(machine_fid)
    print(f"\nMachine &{machine_fid}, default=&{default_fid} '{get_name(default_fid)}'")

    if not default_fid:
        print("  ERROR: no default state")
        continue

    ub_name = get_name(default_fid)
    if 'ub_empty' not in ub_name.lower():
        print(f"  WARNING: default '{ub_name}' is not UB_Empty")

    # Sort: crouch/prone first (more specific), stand last (fallback)
    stand_list, crouch_list, prone_list, other_list = [], [], [], []
    for fid in child_fids:
        if fid == default_fid:
            continue
        name = get_name(fid)
        nl = name.lower()
        if nl.endswith('_crouch'):
            crouch_list.append((fid, name))
        elif nl.endswith('_prone'):
            prone_list.append((fid, name))
        else:
            stand_list.append((fid, name))

    ordered = crouch_list + prone_list + stand_list

    new_trans_fids = []
    for state_fid, state_name in ordered:
        conds = get_conditions(state_name)
        if conds is None:
            print(f"  SKIP: &{state_fid} '{state_name}'")
            continue
        fid = str(next_fid[0])
        next_fid[0] += 1
        new_blocks.append(make_block(fid, state_fid, conds))
        new_trans_fids.append(fid)
        cond_str = [(e, t) for _, e, t in conds]
        print(f"  + &{fid} UB_Empty -> '{state_name}' conds={cond_str}")

    ub_updates[default_fid] = new_trans_fids

print(f"\nTotal new transitions: {len(new_blocks)}")

# Update UB_Empty m_Transitions in the lines
modified = list(lines)
for ub_fid, trans_fids in ub_updates.items():
    if not trans_fids:
        continue
    for i, l in enumerate(modified):
        if re.match(r'^--- !u!1102 &' + ub_fid + r'\b', l):
            j = i + 1
            while j < len(modified) and not re.match(r'^--- !u!', modified[j]):
                if re.match(r'\s+m_Transitions: \[\]', modified[j]):
                    refs = ''.join(f'  - {{fileID: {fid}}}\n' for fid in trans_fids)
                    modified[j] = f'  m_Transitions:\n{refs}'
                    print(f"  Updated UB_Empty &{ub_fid}: added {len(trans_fids)} transition refs")
                    break
                j += 1
            break

# Append new blocks
modified.append('\n')
for block in new_blocks:
    modified.append(block)

print(f"\nFinal lines: {len(modified)}")
with open(F, 'w', encoding='utf-8') as f:
    f.writelines(modified)
print("Done! Reimport SoldierAnimatorController in Unity.")
