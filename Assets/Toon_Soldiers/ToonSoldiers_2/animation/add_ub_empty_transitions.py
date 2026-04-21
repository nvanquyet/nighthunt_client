"""
Add explicit transitions FROM UB_Empty TO each action state in every UB sub-machine.
Derives trigger + conditions from state names automatically.
New transition FIDs start at 9203000.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# ── UB sub-machine FIDs and their UB_Empty state FID ──────────────────────────
UB_MACHINES = [
    (9200048, 9200049, 'Handgun'),
    (9200174, 9200175, 'Infantry'),
    (9200306, 9200307, 'Heavy'),
    (9200441, 9200442, 'Knife'),
    (9200573, 9200574, 'Machinegun'),
    (9200708, 9200709, 'RocketLauncher'),
    (9202210, 9202211, 'Unarmed'),
]

# ── Derive trigger + extra conditions from state name ─────────────────────────
def get_conditions(state_name):
    n = state_name.lower()
    if n.endswith('_stand'):
        stance_conds = [('IsCrouching','2','0'), ('IsProne','2','0')]
    elif n.endswith('_crouch'):
        stance_conds = [('IsCrouching','1','0')]
    elif n.endswith('_prone'):
        stance_conds = [('IsProne','1','0')]
    else:
        stance_conds = []

    if n.startswith('draw'):
        return [('Draw','1','0')] + stance_conds
    elif n.startswith('shootburst'):
        return [('ShootBurst','1','0')] + stance_conds
    elif n.startswith('shootloop'):
        return [('ShootLoop','1','0')] + stance_conds
    elif n.startswith('shootbolt'):
        return [('Shoot','1','0'), ('ShootBolt','1','0')] + stance_conds
    elif n.startswith('shootshotgun'):
        return [('Shoot','1','0'), ('ShootShotgun','1','0')] + stance_conds
    elif n.startswith('shoot'):
        return [('Shoot','1','0')] + stance_conds
    elif n.startswith('reload'):
        return [('Reload','1','0')] + stance_conds
    elif n.startswith('grenade') or n.startswith('throwgrenade'):
        return [('ThrowGrenade','1','0')] + stance_conds
    elif 'interact_a' in n:
        return [('Interact','1','0'), ('InteractIndex','5','0')]
    elif 'interact_b' in n:
        return [('Interact','1','0'), ('InteractIndex','5','1')]
    elif n.startswith('damage'):
        return [('TakeDamage','1','0')] + stance_conds
    elif 'attack_a' in n:
        return [('Attack','1','0'), ('AttackIndex','5','0')] + stance_conds
    elif 'attack_b' in n:
        return [('Attack','1','0'), ('AttackIndex','5','1')] + stance_conds
    else:
        return None

def get_state_name(fid):
    sb = re.search(rf'--- !u!1102 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
    if not sb: return None
    nm = re.search(r'm_Name: (.+)', sb.group(1))
    return nm.group(1).strip() if nm else None

def get_sm_states(sm_fid):
    sm_block = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)--- !u!', content, re.DOTALL)
    if not sm_block: return []
    # Format: m_ChildStates: ... m_State: {fileID: XXXX}
    states_section = re.search(r'm_ChildStates:(.*?)m_ChildStateMachines:', sm_block.group(1), re.DOTALL)
    if not states_section: return []
    fids = re.findall(r'm_State: \{fileID: (\d+)\}', states_section.group(1))
    result = []
    for fid in fids:
        nm = get_state_name(int(fid))
        if nm:
            result.append((int(fid), nm))
    return result

def make_transition_block(trans_fid, dst_state_fid, conditions):
    cond_lines = ''
    for event, mode, thresh in conditions:
        cond_lines += f'  - m_ConditionMode: {mode}\n    m_ConditionEvent: {event}\n    m_EventTreshold: {thresh}\n'
    return f"""--- !u!1101 &{trans_fid}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
{cond_lines}  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: {dst_state_fid}}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
"""

next_fid = 9203000
new_blocks = []
ub_trans_map = {}

for sm_fid, ub_empty_fid, weapon_name in UB_MACHINES:
    states = get_sm_states(sm_fid)
    trans_fids = []
    print(f'\n{weapon_name} (SM:{sm_fid}, UB_Empty:{ub_empty_fid}):')

    for state_fid, state_name in states:
        if state_fid == ub_empty_fid or state_name == 'UB_Empty':
            continue
        conds = get_conditions(state_name)
        if conds is None:
            print(f'  SKIP: {state_name} ({state_fid})')
            continue
        block = make_transition_block(next_fid, state_fid, conds)
        cond_str = ', '.join([f'{e}' for e,m,t in conds])
        print(f'  &{next_fid}: -> {state_name} | {cond_str}')
        new_blocks.append(block)
        trans_fids.append(next_fid)
        next_fid += 1

    ub_trans_map[ub_empty_fid] = trans_fids

print(f'\nTotal new transitions: {len(new_blocks)}')

new_content = content
patched_states = 0
for ub_empty_fid, trans_fids in ub_trans_map.items():
    if not trans_fids:
        continue
    trans_list = ''.join([f'  - {{fileID: {t}}}\n' for t in trans_fids])
    pattern = rf'(--- !u!1102 &{ub_empty_fid}\b.*?m_Transitions:) \[\](.*?m_StateMachineBehaviours:)'
    replacement = rf'\g<1>\n{trans_list}\g<2>'
    new_content2, n = re.subn(pattern, replacement, new_content, count=1, flags=re.DOTALL)
    if n:
        new_content = new_content2
        print(f'Patched UB_Empty {ub_empty_fid}: {len(trans_fids)} transitions')
        patched_states += 1
    else:
        print(f'WARNING: Could not patch UB_Empty {ub_empty_fid}')

insertion_point = new_content.rfind('--- !u!')
new_content = new_content[:insertion_point] + ''.join(new_blocks) + new_content[insertion_point:]
print(f'Inserted {len(new_blocks)} transition blocks')

with open(F, 'w', encoding='utf-8') as f:
    f.write(new_content)
print('File saved.')
