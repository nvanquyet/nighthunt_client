"""
Comprehensive fix:
1. Add AnyState → Exit(WeaponType != N) to all 6 Base + 6 UpperBody sub-machines
   → Enables weapon switching at any time
2. Add Respawn trigger parameter
3. Add AnyState → Death_Empty(Respawn) to all 6 Death sub-machines
   → Enables respawn from any death state
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f:
    lines = f.readlines()

print(f"Lines: {len(lines)}")

# ── max fid ──────────────────────────────────────────────────────────────────
max_fid = max(int(re.search(r'&(\d+)',l).group(1))
              for l in lines if re.match(r'^--- !u!\d+ &\d+', l))
print(f"Max fid: {max_fid}")
nf = max_fid + 1

def alloc():
    global nf
    v = str(nf); nf += 1
    return v

# ── helpers ──────────────────────────────────────────────────────────────────
def get_machine(fid):
    """Returns (default_state_fid, any_trans_fids)"""
    default = None
    any_trans = []
    for i,l in enumerate(lines):
        if re.match(r'^--- !u!1107 &' + fid + r'\b', l):
            section = None
            j = i+1
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                if 'm_AnyStateTransitions:' in lines[j]: section='any'
                elif 'm_EntryTransitions:' in lines[j]: section=None
                if section=='any' and '{fileID:' in lines[j]:
                    m = re.search(r'fileID: (\d+)', lines[j])
                    if m and m.group(1)!='0': any_trans.append(m.group(1))
                if 'm_DefaultState:' in lines[j]:
                    m = re.search(r'fileID: (\d+)', lines[j])
                    if m: default = m.group(1)
                j += 1
            break
    return default, any_trans

def make_anystate_block(fid, conditions, dst_state='0', is_exit=False):
    """
    conditions: list of (mode, event, threshold)
    Format matches pre-existing Death AnyState blocks (no extra fields)
    """
    cond_yaml = ''
    for mode, event, thresh in conditions:
        cond_yaml += f'  - m_ConditionMode: {mode}\n'
        cond_yaml += f'    m_ConditionEvent: {event}\n'
        cond_yaml += f'    m_EventTreshold: {thresh}\n'
    exit_flag = 1 if is_exit else 0
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
        f'  m_DstState: {{fileID: {dst_state}}}\n'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: {exit_flag}\n'
    )

def patch_anystate(machine_fid, new_trans_fid, modified):
    """Append new_trans_fid to m_AnyStateTransitions of machine_fid"""
    for i,l in enumerate(modified):
        if re.match(r'^--- !u!1107 &' + machine_fid + r'\b', l):
            j = i+1
            while j < len(modified) and not re.match(r'^--- !u!', modified[j]):
                if 'm_AnyStateTransitions:' in modified[j]:
                    # Insert new ref after the list (find end of list)
                    k = j+1
                    while k < len(modified) and modified[k].strip().startswith('- {fileID:'):
                        k += 1
                    modified.insert(k, f'  - {{fileID: {new_trans_fid}}}\n')
                    print(f"  Patched &{machine_fid} AnyState ← &{new_trans_fid}")
                    return
                j += 1
            break

# ─────────────────────────────────────────────────────────────────────────────
# 1. WEAPON SWITCHING — AnyState → Exit(WeaponType != N)
#    Base sub-machines + UpperBody sub-machines
# ─────────────────────────────────────────────────────────────────────────────
print("\n=== 1. WEAPON SWITCHING ===")

WEAPON_MACHINES = [
    # (machine_fid, weapon_index)
    # Base Layer
    ('9200001', 0),  # Handgun_Base
    ('9200126', 1),  # Infantry_Base
    ('9200257', 2),  # Heavy_Base
    ('9200391', 3),  # Knife_Base
    ('9200522', 4),  # Machinegun_Base
    ('9200656', 5),  # RocketLauncher_Base
    # UpperBody Layer
    ('9200048', 0),  # Handgun_UpperBody
    ('9200173', 1),  # Infantry_UpperBody
    ('9200304', 2),  # Heavy_UpperBody
    ('9200438', 3),  # Knife_UpperBody
    ('9200569', 4),  # Machinegun_UpperBody
    ('9200703', 5),  # RocketLauncher_UpperBody
]

new_blocks = []
modified = list(lines)

for machine_fid, weapon_idx in WEAPON_MACHINES:
    # AnyState → Exit when WeaponType != weapon_idx
    # mode 7 = NotEqual (int)
    fid = alloc()
    conds = [(7, 'WeaponType', weapon_idx)]
    new_blocks.append(make_anystate_block(fid, conds, dst_state='0', is_exit=True))
    patch_anystate(machine_fid, fid, modified)
    print(f"  Machine &{machine_fid} [weapon={weapon_idx}]: AnyState → Exit(WeaponType != {weapon_idx}) fid=&{fid}")

# ─────────────────────────────────────────────────────────────────────────────
# 2. ADD Respawn TRIGGER PARAMETER
# ─────────────────────────────────────────────────────────────────────────────
print("\n=== 2. RESPAWN PARAMETER ===")

respawn_param = (
    '  - m_Name: Respawn\n'
    '    m_Type: 9\n'
    '    m_DefaultFloat: 0\n'
    '    m_DefaultInt: 0\n'
    '    m_DefaultBool: 0\n'
    '    m_Controller: {fileID: 9100000}\n'
)

# Find last parameter entry (Roll) and insert after it
for i in range(len(modified)-1, -1, -1):
    if '  - m_Name: Roll' in modified[i]:
        # Find end of Roll block (next '  - m_Name:' or m_AnimatorLayers)
        j = i+1
        while j < len(modified):
            if modified[j].startswith('  - m_Name:') or 'm_AnimatorLayers:' in modified[j]:
                modified.insert(j, respawn_param)
                print(f"  Inserted Respawn param after Roll at L{j}")
                break
            j += 1
        break

# ─────────────────────────────────────────────────────────────────────────────
# 3. RESPAWN in Death sub-machines — AnyState → Death_Empty (Respawn trigger)
# ─────────────────────────────────────────────────────────────────────────────
print("\n=== 3. DEATH RESPAWN ===")

DEATH_MACHINES = [
    '9200111',  # Handgun_Death
    '9200242',  # Infantry_Death
    '9200376',  # Heavy_Death
    '9200507',  # Knife_Death
    '9200641',  # Machinegun_Death
    '9200757',  # RocketLauncher_Death
]

for machine_fid in DEATH_MACHINES:
    default_fid, _ = get_machine(machine_fid)
    if not default_fid:
        print(f"  ERROR: no default for &{machine_fid}")
        continue
    # AnyState → Death_Empty on Respawn
    fid = alloc()
    conds = [(1, 'Respawn', 0)]  # mode 1 = If (trigger)
    new_blocks.append(make_anystate_block(fid, conds, dst_state=default_fid, is_exit=False))
    patch_anystate(machine_fid, fid, modified)
    print(f"  Machine &{machine_fid}: AnyState → Death_Empty(&{default_fid}) on Respawn fid=&{fid}")

# ─────────────────────────────────────────────────────────────────────────────
# 4. Write
# ─────────────────────────────────────────────────────────────────────────────
modified.append('\n')
for block in new_blocks:
    modified.append(block)

print(f"\nNew blocks appended: {len(new_blocks)}")
print(f"Final lines: {len(modified)}")

with open(F,'w',encoding='utf-8') as f:
    f.writelines(modified)

print("Done!")
