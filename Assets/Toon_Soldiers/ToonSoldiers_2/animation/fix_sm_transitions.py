"""
ROOT CAUSE FIX: UpperBody root has m_StateMachineTransitions empty + default state = Handgun_UB.
When ANY sub-machine exits (via WeaponChangedUB), root routes to Handgun_UB (wrong!).

Fix: Add m_StateMachineTransitions for each sub-machine exit -> route to correct SM via WeaponType.
Also do the same for Base Layer root and Death root.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

# SM FIDs per layer
UB_SMS  = [9200048, 9200174, 9200306, 9200441, 9200573, 9200708, 9202210]  # Handgun..Unarmed
BASE_SMS= [9200001, 9200127, 9200259, 9200394, 9200526, 9200661, 9202200]
DEATH_SMS=[9200111, 9200243, 9200378, 9200510, 9200645, 9200762, 9202220]

# WeaponType 0-6 -> SM FID for each layer
UB_WEAPON_TO_SM    = {0:9202210, 1:9200048, 2:9200174, 3:9200306, 4:9200441, 5:9200573, 6:9200708}
BASE_WEAPON_TO_SM  = {0:9202200, 1:9200001, 2:9200127, 3:9200259, 4:9200394, 5:9200526, 6:9200661}
DEATH_WEAPON_TO_SM = {0:9202220, 1:9200111, 2:9200243, 3:9200378, 4:9200510, 5:9200645, 6:9200762}

ROOT_UB    = 9200812
ROOT_BASE  = 9200774
ROOT_DEATH = 9200850

# FID ranges for new transitions
# UB SM transitions: 9206000-9206006 (reused for all UB SM sources)
# Base SM transitions: 9206010-9206016
# Death SM transitions: 9206020-9206026

def make_sm_transition_blocks(start_fid, weapon_to_sm):
    """Create 7 AnimatorTransition (1109) blocks for WeaponType 0-6 routing."""
    blocks = []
    for wt in range(7):
        fid = start_fid + wt
        dst_sm = weapon_to_sm[wt]
        block = f"""--- !u!1109 &{fid}
AnimatorTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 6
    m_ConditionEvent: WeaponType
    m_EventTreshold: {wt}
  m_DstStateMachine: {{fileID: {dst_sm}}}
  m_DstState: {{fileID: 0}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 1
  m_TransitionDuration: 0
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
"""
        blocks.append((fid, block))
    return blocks

def build_sm_transitions_yaml(sm_list, trans_fids):
    """Build m_StateMachineTransitions YAML for a root machine."""
    lines = ['  m_StateMachineTransitions:']
    second_part = '\n'.join(f'    - {{fileID: {fid}}}' for fid in trans_fids)
    for sm_fid in sm_list:
        lines.append(f'  - first: {{fileID: {sm_fid}}}')
        lines.append(f'    second:')
        for fid in trans_fids:
            lines.append(f'    - {{fileID: {fid}}}')
    return '\n'.join(lines)

# Create transition blocks
ub_blocks    = make_sm_transition_blocks(9206000, UB_WEAPON_TO_SM)
base_blocks  = make_sm_transition_blocks(9206010, BASE_WEAPON_TO_SM)
death_blocks = make_sm_transition_blocks(9206020, DEATH_WEAPON_TO_SM)

ub_fids    = [b[0] for b in ub_blocks]
base_fids  = [b[0] for b in base_blocks]
death_fids = [b[0] for b in death_blocks]

# Inject transition blocks at end of file (before EOF)
new_blocks_text = '\n'.join(b[1] for _, b in ub_blocks + base_blocks + death_blocks)

# Replace m_StateMachineTransitions in each root machine
def fix_root_sm_transitions(root_fid, sm_list, trans_fids, layer_name):
    global content
    root_m = re.search(rf'(--- !u!\d+ &{root_fid}\b)(.*?)(m_StateMachineTransitions:.*?)(m_StateMachineBehaviours:)', content, re.DOTALL)
    if not root_m:
        print(f'ERROR: Could not find {layer_name} root &{root_fid}')
        return

    old_smt = root_m.group(3)
    new_smt_lines = ['m_StateMachineTransitions:']
    for sm_fid in sm_list:
        new_smt_lines.append(f'  - first: {{fileID: {sm_fid}}}')
        new_smt_lines.append(f'    second:')
        for tfid in trans_fids:
            new_smt_lines.append(f'    - {{fileID: {tfid}}}')
    # Include fileID:0 entry too for default
    new_smt_lines.append(f'  - first: {{fileID: 0}}')
    new_smt_lines.append(f'    second:')
    for tfid in trans_fids:
        new_smt_lines.append(f'    - {{fileID: {tfid}}}')
    new_smt = '\n  '.join(new_smt_lines) + '\n  '

    content = content[:root_m.start(3)] + new_smt + content[root_m.start(4):]
    print(f'{layer_name} root &{root_fid}: m_StateMachineTransitions updated ({len(sm_list)+1} entries, {len(trans_fids)} transitions each)')

fix_root_sm_transitions(ROOT_UB,    UB_SMS,    ub_fids,    'UpperBody')
fix_root_sm_transitions(ROOT_BASE,  BASE_SMS,  base_fids,  'Base Layer')
fix_root_sm_transitions(ROOT_DEATH, DEATH_SMS, death_fids, 'Death')

# Append new transition blocks before last line
content = content.rstrip() + '\n' + new_blocks_text

# Verify
print(f'\nInserted {len(ub_blocks + base_blocks + death_blocks)} AnimatorTransition (1109) blocks')

# Spot check: find UB_root's m_StateMachineTransitions
ub_root = re.search(rf'--- !u!\d+ &{ROOT_UB}\b(.*?)(?=--- !u!)', content, re.DOTALL)
if ub_root:
    smt = re.search(r'm_StateMachineTransitions:(.*?)m_StateMachineBehaviours:', ub_root.group(1), re.DOTALL)
    if smt:
        print(f'\nUB root m_StateMachineTransitions (first 400 chars):')
        print(smt.group(1)[:400])

with open(F, 'w', encoding='utf-8') as f:
    f.write(content)
print('\nFile saved.')
