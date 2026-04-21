"""
Add WeaponChangedDeath trigger + AnyState->Exit transitions in each Death sub-machine.
Same pattern as WeaponChanged (Base) and WeaponChangedUB (UpperBody).
FIDs start at 9205000.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# ── 1. Add WeaponChangedDeath parameter ───────────────────────────────────────
if 'WeaponChangedDeath' in content:
    print('WeaponChangedDeath param already exists')
else:
    insert_after = re.search(
        r'(  - m_Name: WeaponChangedUB\s+m_Type: 9[^\n]*\n(?:    [^\n]+\n)*)',
        content
    )
    new_param = (
        '  - m_Name: WeaponChangedDeath\n'
        '    m_Type: 9\n'
        '    m_DefaultFloat: 0\n'
        '    m_DefaultInt: 0\n'
        '    m_DefaultBool: 0\n'
        '    m_Controller: {fileID: 0}\n'
    )
    if insert_after:
        pos = insert_after.end()
        content = content[:pos] + new_param + content[pos:]
        print('Added WeaponChangedDeath parameter')
    else:
        # fallback: insert before m_AnimatorLayers
        params_end = re.search(r'(m_AnimatorLayers:)', content)
        if params_end:
            content = content[:params_end.start()] + new_param + content[params_end.start():]
            print('Added WeaponChangedDeath parameter (fallback)')
        else:
            print('ERROR: cannot find insertion point')

# ── 2. Add AnyState->Exit transition in each Death sub-machine ────────────────
DEATH_MACHINES = [
    (9200111, 'Handgun_Death'),
    (9200243, 'Infantry_Death'),
    (9200378, 'Heavy_Death'),
    (9200510, 'Knife_Death'),
    (9200645, 'Machinegun_Death'),
    (9200762, 'RL_Death'),
    (9202220, 'Unarmed_Death'),
]

def make_anystate_exit(fid):
    return (
        f'--- !u!1101 &{fid}\n'
        f'AnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: \n'
        f'  m_Conditions:\n'
        f'  - m_ConditionMode: 1\n'
        f'    m_ConditionEvent: WeaponChangedDeath\n'
        f'    m_EventTreshold: 0\n'
        f'  m_DstStateMachine: {{fileID: 0}}\n'
        f'  m_DstState: {{fileID: 0}}\n'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: 1\n'
        f'  serializedVersion: 3\n'
        f'  m_TransitionDuration: 0\n'
        f'  m_TransitionOffset: 0\n'
        f'  m_ExitTime: 0\n'
        f'  m_HasExitTime: 0\n'
        f'  m_HasFixedDuration: 1\n'
        f'  m_InterruptionSource: 0\n'
        f'  m_OrderedInterruption: 1\n'
        f'  m_CanTransitionToSelf: 0\n'
    )

next_fid = 9205000
new_blocks = []

for sm_fid, sm_name in DEATH_MACHINES:
    exit_fid = next_fid; next_fid += 1
    new_blocks.append(make_anystate_exit(exit_fid))

    # Patch m_AnyStateTransitions in this sub-machine to add the new exit ref
    sm_pattern = rf'(--- !u!\d+ &{sm_fid}\b.*?m_AnyStateTransitions:)(.*?)(m_EntryTransitions:)'
    sm_m = re.search(sm_pattern, content, re.DOTALL)
    if not sm_m:
        print(f'WARNING: {sm_name} SM not found')
        continue

    existing_refs = sm_m.group(2)
    new_ref = f'\n  - {{fileID: {exit_fid}}}'

    # Append to existing list
    new_any = existing_refs.rstrip('\n') + new_ref + '\n'
    content = content[:sm_m.start(2)] + new_any + content[sm_m.end(2):]
    print(f'{sm_name}: added AnyState exit &{exit_fid} (WeaponChangedDeath -> Exit)')

# ── 3. Insert new blocks ──────────────────────────────────────────────────────
insertion_point = content.rfind('--- !u!')
content = content[:insertion_point] + ''.join(new_blocks) + content[insertion_point:]

with open(F, 'w', encoding='utf-8') as f:
    f.write(content)
print(f'\nInserted {len(new_blocks)} blocks. File saved.')
